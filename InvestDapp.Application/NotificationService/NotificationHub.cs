using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System;

namespace InvestDapp.Application.NotificationService
{
    [Authorize]
    public class NotificationHub : Hub
    {
        public async Task JoinUserGroup(string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
        }

        public async Task LeaveUserGroup(string userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst("UserId")?.Value
                                  ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                  ?? Context.User?.FindFirst("sub")?.Value;

                if (!string.IsNullOrEmpty(userIdClaim))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userIdClaim}");
                }

                var walletClaim = Context.User?.FindFirst("WalletAddress")?.Value;
                if (!string.IsNullOrEmpty(walletClaim))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Wallet_{walletClaim.ToLowerInvariant()}");
                }
            }
            catch (Exception)
            {
                // Swallow; do not prevent connection if group join fails.
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendUnreadCountUpdate(string userId, int unreadCount)
        {
            await Clients.Group($"User_{userId}").SendAsync("UnreadNotificationCountChanged", unreadCount);
        }
    }
}