using InvestDapp.Application.NotificationService;
using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Common;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Common.Respone;
using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models.Support;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace InvestDapp.Application.SupportService
{
    public class SupportTicketService : ISupportTicketService
    {
        private readonly ISupportTicketRepository _ticketRepository;
        private readonly INotificationService _notificationService;
    private readonly IHostEnvironment _environment;
        private readonly ILogger<SupportTicketService> _logger;

        private static readonly string[] AllowedAttachmentExtensions = { ".png", ".jpg", ".jpeg", ".pdf", ".docx", ".xlsx", ".txt" };
        private const long MaxAttachmentSize = 10 * 1024 * 1024; // 10 MB

        public SupportTicketService(
            ISupportTicketRepository ticketRepository,
            INotificationService notificationService,
            IHostEnvironment environment,
            ILogger<SupportTicketService> logger)
        {
            _ticketRepository = ticketRepository;
            _notificationService = notificationService;
            _environment = environment;
            _logger = logger;
        }

        public async Task<BaseResponse<SupportTicketDetailResponse>> CreateTicketAsync(CreateSupportTicketRequest request, int userId, IEnumerable<IFormFile>? attachments, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return new BaseResponse<SupportTicketDetailResponse> { Success = false, Message = "Yêu cầu không hợp lệ" };
            }

            try
            {
                var ticketCode = GenerateTicketCode();
                var now = DateTime.UtcNow;
                var dueAt = now + GetSlaTarget(request.Priority);

                var ticket = new SupportTicket
                {
                    TicketCode = ticketCode,
                    Subject = request.Subject,
                    Category = request.Category,
                    Priority = request.Priority,
                    Status = SupportTicketStatus.New,
                    SlaStatus = SupportTicketSlaStatus.OnTrack,
                    CreatedAt = now,
                    UpdatedAt = now,
                    DueAt = dueAt,
                    UserId = userId,
                    LastCustomerReplyAt = now
                };

                var firstMessage = new SupportTicketMessage
                {
                    SenderUserId = userId,
                    IsFromStaff = false,
                    Content = request.Message,
                    CreatedAt = now
                };

                var storedAttachments = await SaveAttachmentsAsync(ticketCode, attachments, cancellationToken);

                await _ticketRepository.CreateTicketAsync(ticket, firstMessage, storedAttachments, cancellationToken);

                await NotifyAdminsNewTicketAsync(ticket, cancellationToken);

                var detail = await BuildDetailResponseAsync(ticket.Id, true, cancellationToken) ?? MapDetail(ticket);
                return new BaseResponse<SupportTicketDetailResponse>
                {
                    Success = true,
                    Data = detail,
                    Message = "Tạo ticket thành công"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể tạo ticket hỗ trợ cho user {UserId}", userId);
                return new BaseResponse<SupportTicketDetailResponse>
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<BaseResponse<SupportTicketListResult>> GetTicketsForUserAsync(int userId, SupportTicketFilterRequest filter, CancellationToken cancellationToken = default)
        {
            filter ??= new SupportTicketFilterRequest();
            var query = new SupportTicketQuery
            {
                UserId = userId,
                Status = filter.Status,
                Priority = filter.Priority,
                SlaStatus = filter.SlaStatus,
                Keyword = filter.Keyword,
                Skip = Math.Max(0, (filter.Page - 1) * filter.PageSize),
                Take = filter.PageSize
            };

            var result = await _ticketRepository.QueryTicketsAsync(query, cancellationToken);
            var summaries = result.Items.Select(MapSummary).ToList();

            return new BaseResponse<SupportTicketListResult>
            {
                Success = true,
                Data = new SupportTicketListResult
                {
                    Items = summaries,
                    Total = result.Total,
                    Page = filter.Page,
                    PageSize = filter.PageSize
                }
            };
        }

        public async Task<BaseResponse<SupportTicketDetailResponse>> GetTicketDetailForUserAsync(int ticketId, int userId, CancellationToken cancellationToken = default)
        {
            var detail = await BuildDetailResponseAsync(ticketId, true, cancellationToken);
            if (detail == null || detail.Id == 0)
            {
                return new BaseResponse<SupportTicketDetailResponse> { Success = false, Message = "Không tìm thấy ticket" };
            }

            if (!await IsTicketOwnedByUserAsync(ticketId, userId, cancellationToken))
            {
                return new BaseResponse<SupportTicketDetailResponse> { Success = false, Message = "Bạn không có quyền truy cập" };
            }

            return new BaseResponse<SupportTicketDetailResponse> { Success = true, Data = detail };
        }

        public async Task<BaseResponse<SupportTicketDetailResponse>> AddMessageFromUserAsync(AddSupportTicketMessageRequest request, int userId, IEnumerable<IFormFile>? attachments, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return new BaseResponse<SupportTicketDetailResponse> { Success = false, Message = "Yêu cầu không hợp lệ" };
            }

            try
            {
                var ticket = await _ticketRepository.GetTicketByIdAsync(request.TicketId, includeDetails: false, cancellationToken);
                if (ticket == null || ticket.UserId != userId)
                {
                    return new BaseResponse<SupportTicketDetailResponse> { Success = false, Message = "Không tìm thấy ticket hoặc không có quyền" };
                }

                var now = DateTime.UtcNow;
                ticket.Status = SupportTicketStatus.InProgress;
                ticket.LastCustomerReplyAt = now;
                ticket.UpdatedAt = now;
                ticket.SlaStatus = ComputeSlaStatus(ticket, now);

                var message = new SupportTicketMessage
                {
                    TicketId = ticket.Id,
                    SenderUserId = userId,
                    IsFromStaff = false,
                    Content = request.Message,
                    CreatedAt = now
                };

                var storedAttachments = await SaveAttachmentsAsync(ticket.TicketCode, attachments, cancellationToken);
                await _ticketRepository.AddMessageAsync(message, storedAttachments, cancellationToken);
                await _ticketRepository.UpdateTicketAsync(ticket, cancellationToken);

                if (ticket.AssignedToUserId.HasValue)
                {
                    await NotifyStaffTicketUpdatedAsync(ticket, ticket.AssignedToUserId.Value, "Nhà đầu tư đã phản hồi", cancellationToken);
                }

                var detail = await BuildDetailResponseAsync(ticket.Id, true, cancellationToken) ?? MapDetail(ticket);
                return new BaseResponse<SupportTicketDetailResponse> { Success = true, Data = detail };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể thêm tin nhắn của user {UserId} cho ticket {TicketId}", userId, request.TicketId);
                return new BaseResponse<SupportTicketDetailResponse> { Success = false, Message = ex.Message };
            }
        }

        public async Task<BaseResponse<SupportTicketListResult>> GetTicketsForAdminAsync(SupportTicketFilterRequest filter, CancellationToken cancellationToken = default)
        {
            filter ??= new SupportTicketFilterRequest();
            var query = new SupportTicketQuery
            {
                Status = filter.Status,
                Priority = filter.Priority,
                SlaStatus = filter.SlaStatus,
                Keyword = filter.Keyword,
                AssignedToUserId = filter.AssignedToUserId,
                Skip = Math.Max(0, (filter.Page - 1) * filter.PageSize),
                Take = filter.PageSize
            };

            var result = await _ticketRepository.QueryTicketsAsync(query, cancellationToken);
            var summaries = result.Items.Select(MapSummary).ToList();
            return new BaseResponse<SupportTicketListResult>
            {
                Success = true,
                Data = new SupportTicketListResult
                {
                    Items = summaries,
                    Total = result.Total,
                    Page = filter.Page,
                    PageSize = filter.PageSize
                }
            };
        }

        public async Task<BaseResponse<SupportTicketDetailResponse>> GetTicketDetailForAdminAsync(int ticketId, CancellationToken cancellationToken = default)
        {
            var detail = await BuildDetailResponseAsync(ticketId, true, cancellationToken);
            if (detail == null)
            {
                return new BaseResponse<SupportTicketDetailResponse> { Success = false, Message = "Không tìm thấy ticket" };
            }

            return new BaseResponse<SupportTicketDetailResponse> { Success = true, Data = detail };
        }

        public async Task<BaseResponse<bool>> AddMessageFromStaffAsync(AddSupportTicketMessageRequest request, int staffUserId, bool markAsResolved, IEnumerable<IFormFile>? attachments, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return new BaseResponse<bool> { Success = false, Message = "Yêu cầu không hợp lệ" };
            }

            try
            {
                var ticket = await _ticketRepository.GetTicketByIdAsync(request.TicketId, includeDetails: false, cancellationToken);
                if (ticket == null)
                {
                    return new BaseResponse<bool> { Success = false, Message = "Không tìm thấy ticket" };
                }

                var now = DateTime.UtcNow;
                if (ticket.FirstResponseAt == null)
                {
                    ticket.FirstResponseAt = now;
                }

                ticket.LastAgentReplyAt = now;
                ticket.UpdatedAt = now;

                if (markAsResolved)
                {
                    ticket.Status = SupportTicketStatus.Resolved;
                    ticket.ResolvedAt = now;
                }
                else if (request.TransitionToCustomerWaiting)
                {
                    ticket.Status = SupportTicketStatus.WaitingForCustomer;
                }
                else if (ticket.Status == SupportTicketStatus.New)
                {
                    ticket.Status = SupportTicketStatus.InProgress;
                }

                ticket.SlaStatus = ComputeSlaStatus(ticket, now);

                var message = new SupportTicketMessage
                {
                    TicketId = ticket.Id,
                    SenderUserId = staffUserId,
                    IsFromStaff = true,
                    Content = request.Message,
                    CreatedAt = now
                };

                var storedAttachments = await SaveAttachmentsAsync(ticket.TicketCode, attachments, cancellationToken);
                await _ticketRepository.AddMessageAsync(message, storedAttachments, cancellationToken);
                await _ticketRepository.UpdateTicketAsync(ticket, cancellationToken);

                await NotifyUserTicketUpdatedAsync(ticket, "Nhân sự đã phản hồi ticket của bạn", cancellationToken);

                return new BaseResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nhân sự {StaffId} không thể gửi phản hồi cho ticket {TicketId}", staffUserId, request?.TicketId);
                return new BaseResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<BaseResponse<bool>> AssignTicketAsync(AssignSupportTicketRequest request, int assignedByUserId, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return new BaseResponse<bool> { Success = false, Message = "Yêu cầu không hợp lệ" };
            }

            try
            {
                await _ticketRepository.AssignAsync(request.TicketId, request.AssignedToUserId, assignedByUserId, request.Notes, cancellationToken);

                var ticket = await _ticketRepository.GetTicketByIdAsync(request.TicketId, includeDetails: false, cancellationToken);
                if (ticket != null)
                {
                    await NotifyStaffTicketUpdatedAsync(ticket, request.AssignedToUserId, "Bạn được giao ticket mới", cancellationToken);
                }

                return new BaseResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể gán ticket {TicketId} cho user {AssignedTo}", request?.TicketId, request?.AssignedToUserId);
                return new BaseResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<BaseResponse<bool>> UpdateStatusAsync(int ticketId, int staffUserId, bool resolve, bool close, CancellationToken cancellationToken = default)
        {
            try
            {
                var ticket = await _ticketRepository.GetTicketByIdAsync(ticketId, includeDetails: false, cancellationToken);
                if (ticket == null)
                {
                    return new BaseResponse<bool> { Success = false, Message = "Ticket không tồn tại" };
                }

                var now = DateTime.UtcNow;
                if (resolve)
                {
                    ticket.Status = SupportTicketStatus.Resolved;
                    ticket.ResolvedAt = now;
                }

                if (close)
                {
                    ticket.Status = SupportTicketStatus.Closed;
                    ticket.ClosedAt = now;
                }

                ticket.UpdatedAt = now;
                ticket.SlaStatus = ComputeSlaStatus(ticket, now);

                await _ticketRepository.UpdateTicketAsync(ticket, cancellationToken);
                await NotifyUserTicketUpdatedAsync(ticket, "Ticket của bạn đã được cập nhật trạng thái", cancellationToken);

                return new BaseResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể cập nhật trạng thái ticket {TicketId} bởi user {StaffId}", ticketId, staffUserId);
                return new BaseResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task EvaluateSlaAsync(CancellationToken cancellationToken = default)
        {
            var openTickets = await _ticketRepository.GetOpenTicketsAsync(cancellationToken);
            var now = DateTime.UtcNow;
            foreach (var ticket in openTickets)
            {
                var previousStatus = ticket.SlaStatus;
                var newStatus = ComputeSlaStatus(ticket, now);
                if (newStatus != previousStatus)
                {
                    ticket.SlaStatus = newStatus;
                    ticket.UpdatedAt = now;
                    await _ticketRepository.UpdateTicketAsync(ticket, cancellationToken);

                    if (ticket.AssignedToUserId.HasValue)
                    {
                        var message = newStatus switch
                        {
                            SupportTicketSlaStatus.AtRisk => "Ticket sắp đến hạn SLA, vui lòng xử lý sớm",
                            SupportTicketSlaStatus.Breached => "Ticket đã trễ SLA! Cần xử lý khẩn cấp",
                            _ => ""
                        };

                        if (!string.IsNullOrEmpty(message))
                        {
                            await NotifyStaffTicketUpdatedAsync(ticket, ticket.AssignedToUserId.Value, message, cancellationToken);
                        }
                    }
                }
            }
        }

        private async Task<SupportTicketDetailResponse?> BuildDetailResponseAsync(int ticketId, bool includeDetails, CancellationToken cancellationToken)
        {
            var ticket = await _ticketRepository.GetTicketByIdAsync(ticketId, includeDetails, cancellationToken);
            if (ticket == null)
            {
                return null;
            }

            return MapDetail(ticket);
        }

        private SupportTicketSummaryResponse MapSummary(SupportTicket ticket)
        {
            return new SupportTicketSummaryResponse
            {
                Id = ticket.Id,
                TicketCode = ticket.TicketCode,
                Subject = ticket.Subject,
                Category = ticket.Category,
                Priority = ticket.Priority,
                Status = ticket.Status,
                SlaStatus = ticket.SlaStatus,
                CreatedAt = ticket.CreatedAt,
                DueAt = ticket.DueAt,
                AssignedToUserId = ticket.AssignedToUserId,
                AssignedToName = ticket.AssignedToUser?.Name ?? ticket.AssignedToUser?.WalletAddress,
                RequesterName = ticket.User?.Name ?? ticket.User?.WalletAddress ?? "--"
            };
        }

        private SupportTicketDetailResponse MapDetail(SupportTicket ticket)
        {
            var detail = new SupportTicketDetailResponse
            {
                Id = ticket.Id,
                TicketCode = ticket.TicketCode,
                Subject = ticket.Subject,
                Category = ticket.Category,
                RequesterName = ticket.User?.Name ?? ticket.User?.WalletAddress ?? "--",
                RequesterWallet = ticket.User?.WalletAddress ?? string.Empty,
                Priority = ticket.Priority,
                Status = ticket.Status,
                SlaStatus = ticket.SlaStatus,
                CreatedAt = ticket.CreatedAt,
                DueAt = ticket.DueAt,
                FirstResponseAt = ticket.FirstResponseAt,
                ResolvedAt = ticket.ResolvedAt,
                ClosedAt = ticket.ClosedAt,
                AssignedToUserId = ticket.AssignedToUserId,
                AssignedToName = ticket.AssignedToUser?.Name ?? ticket.AssignedToUser?.WalletAddress
            };

            if (ticket.Messages != null && ticket.Messages.Count > 0)
            {
                detail.Messages = ticket.Messages
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => new SupportTicketMessageResponse
                    {
                        Id = m.Id,
                        SenderUserId = m.SenderUserId,
                        SenderName = m.SenderUser?.Name ?? m.SenderUser?.WalletAddress ?? "--",
                        IsFromStaff = m.IsFromStaff,
                        Content = m.Content,
                        CreatedAt = m.CreatedAt,
                        Attachments = m.Attachments?.Select(a => new SupportTicketAttachmentResponse
                        {
                            Id = a.Id,
                            FileName = a.FileName,
                            FilePath = a.FilePath,
                            ContentType = a.ContentType,
                            FileSize = a.FileSize
                        }).ToList() ?? new List<SupportTicketAttachmentResponse>()
                    })
                    .ToList();
            }

            if (ticket.Assignments != null && ticket.Assignments.Count > 0)
            {
                detail.Assignments = ticket.Assignments
                    .OrderByDescending(a => a.AssignedAt)
                    .Select(a => new SupportTicketAssignmentHistoryResponse
                    {
                        Id = a.Id,
                        AssignedAt = a.AssignedAt,
                        AssignedToName = a.AssignedToUser?.Name ?? a.AssignedToUser?.WalletAddress ?? "--",
                        AssignedByName = a.AssignedByUser?.Name ?? a.AssignedByUser?.WalletAddress ?? "--",
                        Notes = a.Notes
                    })
                    .ToList();
            }

            return detail;
        }

        private async Task<bool> IsTicketOwnedByUserAsync(int ticketId, int userId, CancellationToken cancellationToken)
        {
            var ticket = await _ticketRepository.GetTicketByIdAsync(ticketId, includeDetails: false, cancellationToken);
            return ticket != null && ticket.UserId == userId;
        }

        private static string GenerateTicketCode()
        {
            return $"ST-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..20];
        }

        private TimeSpan GetSlaTarget(SupportTicketPriority priority)
        {
            return priority switch
            {
                SupportTicketPriority.Low => TimeSpan.FromHours(72),
                SupportTicketPriority.Normal => TimeSpan.FromHours(24),
                SupportTicketPriority.High => TimeSpan.FromHours(8),
                SupportTicketPriority.Critical => TimeSpan.FromHours(2),
                _ => TimeSpan.FromHours(24)
            };
        }

        private SupportTicketSlaStatus ComputeSlaStatus(SupportTicket ticket, DateTime referenceTime)
        {
            if (!ticket.DueAt.HasValue)
            {
                return SupportTicketSlaStatus.OnTrack;
            }

            var dueAt = ticket.DueAt.Value;
            if (referenceTime > dueAt)
            {
                return SupportTicketSlaStatus.Breached;
            }

            var totalDuration = dueAt - ticket.CreatedAt;
            if (totalDuration.TotalMinutes <= 0)
            {
                return SupportTicketSlaStatus.OnTrack;
            }

            var remaining = dueAt - referenceTime;
            var ratio = remaining.TotalMinutes / totalDuration.TotalMinutes;

            if (ratio <= 0.15)
            {
                return SupportTicketSlaStatus.AtRisk;
            }

            return SupportTicketSlaStatus.OnTrack;
        }

        private async Task<List<SupportTicketAttachment>> SaveAttachmentsAsync(string ticketCode, IEnumerable<IFormFile>? attachments, CancellationToken cancellationToken)
        {
            var stored = new List<SupportTicketAttachment>();
            if (attachments == null)
            {
                return stored;
            }

            var root = Directory.Exists(Path.Combine(_environment.ContentRootPath, "wwwroot"))
                ? Path.Combine(_environment.ContentRootPath, "wwwroot")
                : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

            var ticketFolder = Path.Combine(root, "uploads", "support", ticketCode);
            Directory.CreateDirectory(ticketFolder);

            foreach (var file in attachments.Where(a => a.Length > 0))
            {
                if (file.Length > MaxAttachmentSize)
                {
                    throw new InvalidOperationException($"Tệp {file.FileName} vượt quá giới hạn 10MB");
                }

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedAttachmentExtensions.Contains(extension))
                {
                    throw new InvalidOperationException($"Định dạng tệp {extension} không được hỗ trợ");
                }

                var safeFileName = SanitizeFileName(Path.GetFileNameWithoutExtension(file.FileName));
                var finalName = $"{safeFileName}_{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(ticketFolder, finalName);

                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }

                var relativePath = $"/uploads/support/{ticketCode}/{finalName}";
                stored.Add(new SupportTicketAttachment
                {
                    FileName = file.FileName,
                    FilePath = relativePath,
                    ContentType = file.ContentType,
                    FileSize = file.Length
                });
            }

            return stored;
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var safe = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
            return string.IsNullOrWhiteSpace(safe) ? "attachment" : safe;
        }

        private async Task NotifyAdminsNewTicketAsync(SupportTicket ticket, CancellationToken cancellationToken)
        {
            try
            {
                var staffs = await _ticketRepository.GetAssignableStaffAsync(cancellationToken);
                foreach (var staff in staffs)
                {
                    await _notificationService.CreateNotificationAsync(new Shared.Common.Request.CreateNotificationRequest
                    {
                        UserId = staff.UserId,
                        Title = "Ticket hỗ trợ mới",
                        Message = $"Ticket {ticket.TicketCode} vừa được tạo với mức ưu tiên {ticket.Priority}",
                        Type = "SupportTicket",
                        Data = ticket.TicketCode
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi thông báo ticket mới");
            }
        }

        private async Task NotifyStaffTicketUpdatedAsync(SupportTicket ticket, int staffId, string message, CancellationToken cancellationToken)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(new Shared.Common.Request.CreateNotificationRequest
                {
                    UserId = staffId,
                    Title = ticket.TicketCode,
                    Message = message,
                    Type = "SupportTicket",
                    Data = ticket.TicketCode
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi thông báo cho staff ticket {TicketCode}", ticket.TicketCode);
            }
        }

        private async Task NotifyUserTicketUpdatedAsync(SupportTicket ticket, string message, CancellationToken cancellationToken)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(new Shared.Common.Request.CreateNotificationRequest
                {
                    UserId = ticket.UserId,
                    Title = ticket.TicketCode,
                    Message = message,
                    Type = "SupportTicket",
                    Data = ticket.TicketCode
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi thông báo cho nhà đầu tư ticket {TicketCode}", ticket.TicketCode);
            }
        }

        public async Task<BaseResponse<IReadOnlyList<SupportStaffSummaryResponse>>> GetAssignableStaffAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var staffs = await _ticketRepository.GetAssignableStaffAsync(cancellationToken);
                var mapped = staffs
                    .Select(s => new SupportStaffSummaryResponse
                    {
                        UserId = s.UserId,
                        DisplayName = s.DisplayName,
                        Email = s.Email
                    })
                    .ToList();

                return new BaseResponse<IReadOnlyList<SupportStaffSummaryResponse>>
                {
                    Success = true,
                    Data = mapped
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể tải danh sách nhân sự hỗ trợ");
                return new BaseResponse<IReadOnlyList<SupportStaffSummaryResponse>>
                {
                    Success = false,
                    Message = "Không thể tải danh sách nhân sự hỗ trợ"
                };
            }
        }
    }
}
