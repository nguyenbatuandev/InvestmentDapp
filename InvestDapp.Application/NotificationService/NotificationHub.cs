using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

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
            // Auto-join user to their personal notification group
            var userIdClaim = Context.User?.FindFirst("UserId")?.Value;
            if (!string.IsNullOrEmpty(userIdClaim))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userIdClaim}");
            }
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Groups are automatically cleaned up when connection is removed
            await base.OnDisconnectedAsync(exception);
        }

        // Method to send unread count update to specific user
        public async Task SendUnreadCountUpdate(string userId, int unreadCount)
        {
            await Clients.Group($"User_{userId}").SendAsync("UnreadNotificationCountChanged", unreadCount);
        }
    }
}