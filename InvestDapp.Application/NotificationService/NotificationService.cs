using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Common;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using InvestDapp.Application.UserService;

namespace InvestDapp.Application.NotificationService
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _repo;
        private readonly ILogger<NotificationService> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ICampaign _campaignRepo;
        private readonly IUserService _userService;

        public NotificationService(INotificationRepository repo, ILogger<NotificationService> logger, IHubContext<NotificationHub> hubContext, InvestDapp.Infrastructure.Data.interfaces.ICampaign campaignRepo, IUserService userService)
        {
            _repo = repo;
            _logger = logger;
            _hubContext = hubContext;
            _campaignRepo = campaignRepo;
            _userService = userService;
        }

        public async Task<BaseResponse<object>> CreateNotificationAsync(CreateNotificationRequest req)
        {
            if (req == null) return new BaseResponse<object> { Success = false, Message = "Request is null" };
            if (req.UserId <= 0) return new BaseResponse<object> { Success = false, Message = "Invalid UserId" };
            if (string.IsNullOrWhiteSpace(req.Title) && string.IsNullOrWhiteSpace(req.Message))
                return new BaseResponse<object> { Success = false, Message = "Either Title or Message must be provided" };

            var type = req.Type;
            if (!string.IsNullOrEmpty(type) && type.Length > 50) type = type.Substring(0, 50);

            var title = req.Title;
            if (!string.IsNullOrEmpty(title) && title.Length > 255) title = title.Substring(0, 255);

            var message = req.Message ?? string.Empty;
            var data = req.Data ?? string.Empty;

            var n = new Notification
            {
                UserID = req.UserId,
                Type = type ?? string.Empty,
                Title = title ?? string.Empty,
                Message = message,
                Data = data,
                IsRead = false,
                CreatedAt = System.DateTime.UtcNow
            };

            try
            {
                _logger?.LogDebug("CreateNotificationAsync called with payload: {@Request}", new { req.UserId, req.Type, req.Title, req.Message });
                var created = await _repo.CreateAsync(n);
                if (created == null)
                {
                    _logger?.LogWarning("Notification repository returned null when creating notification for UserId={UserId}", req.UserId);
                    return new BaseResponse<object> { Success = false, Message = "Failed to create notification" };
                }

                _logger?.LogInformation("Notification created successfully. Id={NotificationId} UserId={UserId}", created.ID, req.UserId);
                
                try
                {
                    await _hubContext.Clients.Group($"User_{req.UserId}").SendAsync("NewNotification", new
                    {
                        id = created.ID,
                        title = created.Title,
                        message = created.Message,
                        type = created.Type,
                        createdAt = created.CreatedAt,
                        isRead = created.IsRead
                    });
                    
                    var unreadCount = await _repo.GetUnreadCountAsync(req.UserId);
                    await _hubContext.Clients.Group($"User_{req.UserId}").SendAsync("UnreadNotificationCountChanged", unreadCount);
                    
                    _logger?.LogDebug("Real-time notification and count update sent via SignalR for UserId={UserId}", req.UserId);
                }
                catch (System.Exception signalREx)
                {
                    _logger?.LogWarning(signalREx, "Failed to send real-time notification via SignalR for UserId={UserId}", req.UserId);
                }
                
                return new BaseResponse<object> { Success = true, Data = new { created.ID } };
            }
            catch (System.Exception ex)
            {
                _logger?.LogError(ex, "Exception while creating notification for UserId={UserId} Request={Request}", req.UserId, req);
                return new BaseResponse<object> { Success = false, Message = "Exception while creating notification: " + ex.Message };
            }
        }

        public async Task SendUnreadCountUpdateAsync(string userId, int unreadCount)
        {
            try
            {
                await _hubContext.Clients.Group($"User_{userId}").SendAsync("UnreadNotificationCountChanged", unreadCount);
                _logger?.LogDebug("Sent unread count update to User_{UserId}: {Count}", userId, unreadCount);
            }
            catch (System.Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to send unread count update for User_{UserId}", userId);
            }
        }

        public async Task<BaseResponse<object>> CreateNotificationForCampaignInvestorsAsync(CreateNotificationToCampaignRequest req)
        {
            if (req == null) return new BaseResponse<object> { Success = false, Message = "Request is null" };

            try
            {
                _logger?.LogInformation("CreateNotificationForCampaignInvestorsAsync called for CampaignId={CampaignId} Title={Title}", req.CampaignId, req.Title);

                var campaign = await _campaignRepo.GetCampaignByIdAsync(req.CampaignId);
                if (campaign == null)
                {
                    _logger?.LogWarning("Campaign {CampaignId} not found", req.CampaignId);
                    return new BaseResponse<object> { Success = false, Message = "Campaign not found" };
                }

                var investments = campaign.Investments ?? new List<Investment>();

                var investorAddresses = investments
                    .Select(i => i.InvestorAddress?.ToLowerInvariant())
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Distinct()
                    .ToList();

                _logger?.LogInformation("Campaign {CampaignId} has {InvestmentCount} investments and {InvestorCount} unique investor addresses", req.CampaignId, investments.Count, investorAddresses.Count);

                const int maxConcurrency = 20;
                using var throttler = new SemaphoreSlim(maxConcurrency);

                var tasks = investorAddresses.Select(async addr =>
                {
                    await throttler.WaitAsync();
                    try
                    {
                        try
                        {
                            var userResp = await _userService.GetUserByWalletAddressAsync(addr!);
                            if (userResp == null || userResp.Data == null) return false;

                            var n = new Notification
                            {
                                UserID = userResp.Data.ID,
                                Type = req.Type,
                                Title = req.Title,
                                Message = req.Message,
                                Data = req.Data,
                                IsRead = false,
                                CreatedAt = DateTime.UtcNow
                            };

                            var created = await _repo.CreateAsync(n);
                            if (created == null) return false;

                            try
                            {
                                await _hubContext.Clients.Group($"User_{created.UserID}").SendAsync("NewNotification", new
                                {
                                    id = created.ID,
                                    title = created.Title,
                                    message = created.Message,
                                    type = created.Type,
                                    createdAt = created.CreatedAt,
                                    isRead = created.IsRead
                                });

                                var unreadCount = await _repo.GetUnreadCountAsync(created.UserID);
                                await _hubContext.Clients.Group($"User_{created.UserID}").SendAsync("UnreadNotificationCountChanged", unreadCount);

                                _logger?.LogDebug("Real-time notification and count update sent via SignalR for UserId={UserId}", created.UserID);
                            }
                            catch (System.Exception signalREx)
                            {
                                _logger?.LogWarning(signalREx, "Failed to send real-time notification via SignalR for UserId={UserId}", created.UserID);
                            }

                            return true;
                        }
                        catch (System.Exception innerEx)
                        {
                            _logger?.LogWarning(innerEx, "Failed to notify investor {InvestorAddress} for campaign {CampaignId}", addr, req.CampaignId);
                            return false;
                        }
                    }
                    finally
                    {
                        throttler.Release();
                    }
                }).ToList();

                var results = await Task.WhenAll(tasks);
                var sentCount = results.Count(r => r);

                return new BaseResponse<object> { Success = true, Data = new { Sent = sentCount } };
            }
            catch (System.Exception ex)
            {
                _logger?.LogError(ex, "Exception while sending campaign notifications for CampaignId={CampaignId}", req.CampaignId);
                return new BaseResponse<object> { Success = false, Message = "Exception: " + ex.Message };
            }
        }
    }
}
