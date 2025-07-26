using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace InvestDapp.Application.MessageService // Thay bằng namespace của bạn
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IConversationService _chatService;
        private readonly IUserConnectionManager _userConnectionManager; // Thêm quản lý kết nối
        private readonly ILogger<ChatHub> _logger;
        private static readonly ConcurrentDictionary<string, HashSet<string>> UserConnections = new ConcurrentDictionary<string, HashSet<string>>();

        public ChatHub(IConversationService chatService, ILogger<ChatHub> logger, IUserConnectionManager userConnectionManager)
        {
            _chatService = chatService;
            _logger = logger;
            _userConnectionManager = userConnectionManager;
        }

        public async Task SendMessage(int conversationId, int userId, string content)
        {
            await _chatService.CreateAndSendMessageAsync(conversationId, userId, content);
        }

        public List<string> GetOnlineUsers(List<string> userIds)
        {
            if (userIds == null) return new List<string>();
            return userIds.Where(userId => _userConnectionManager.GetConnections(userId).Any()).ToList();
        }

        public async Task JoinConversation(int conversationId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, conversationId.ToString());
            _logger.LogInformation("Client {ConnectionId} joined group {GroupId}", Context.ConnectionId, conversationId);
        }

        public async Task LeaveConversation(int conversationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId.ToString());
            _logger.LogInformation("Client {ConnectionId} left group {GroupId}", Context.ConnectionId, conversationId);
        }

        // ✅ HÀM BỊ THIẾU MÀ BẠN CẦN THÊM VÀO
        public async Task MarkConversationAsRead(int conversationId)
        {
            var readerUserIdString = Context.UserIdentifier;
            if (string.IsNullOrEmpty(readerUserIdString)) return;

            if (!int.TryParse(readerUserIdString, out int readerUserId))
            {
                _logger.LogWarning("Invalid UserIdentifier: {UserIdentifier}", readerUserIdString);
                return;
            }

            // 1. Đánh dấu đã đọc trong DB
            await _chatService.MarkConversationAsReadAsync(conversationId, readerUserId);

            // 2. Lấy những người còn lại trong conversation
            var participants = await _chatService.GetAllUserByConversationIdServiceAsync(conversationId);

            // 3. Tạo payload thông báo
            var data = new
            {
                ConversationId = conversationId,
                ReaderUserId = readerUserId
            };

            // 4. Gửi thông báo đến các user còn lại
            foreach (var user in participants)
            {
                if (user.ID != readerUserId)
                {
                    await Clients.User(user.ID.ToString())
                        .SendAsync("MessagesRead", data);
                }
            }
        }




        // Trong file ChatHub.cs

        // ✅ HÀM ONCONNECTEDASYNC PHIÊN BẢN SỬA LỖI HOÀN CHỈNH
        // Trong file ChatHub.cs

        // HÀM ONCONNECTEDASYNC PHIÊN BẢN CÓ GỠ LỖI
        public override async Task OnConnectedAsync()
        {
            var userIdString = Context.UserIdentifier;
            var connectionId = Context.ConnectionId;

            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                await base.OnConnectedAsync();
                return;
            }

            // 1. Quản lý kết nối
            bool isFirstConnection = !_userConnectionManager.GetConnections(userIdString).Any();
            _userConnectionManager.AddConnection(userIdString, connectionId);

            // 2. Tự động tham gia các group chat
            var conversations = await _chatService.GetUserConversationsServiceAsync(userId);
            try
            {
                foreach (var convo in conversations)
                {
                    await Groups.AddToGroupAsync(connectionId, convo.ConversationId.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user {UserId} to conversation groups.", userId);
            }

            // 3. LOGIC GỬI TIN NHẮN GỠ LỖI
            if (isFirstConnection)
            {
                try
                {
                    var partnerUserIds = new HashSet<string>();
                    foreach (var convo in conversations)
                    {
                        foreach (var participant in convo.Participants)
                        {
                            if (participant.UserId != userId)
                            {
                                partnerUserIds.Add(participant.UserId.ToString());
                            }
                        }
                    }

                    if (partnerUserIds.Any())
                    {
                        var partnerIdList = partnerUserIds.ToList();
                        _logger.LogInformation("[BROADCAST CHECK] Chuẩn bị gửi 'UserOnline' của User {UserId} đến các partner: {PartnerIds}", userId, string.Join(", ", partnerIdList));

                        // GỬI MỘT EVENT RIÊNG ĐỂ GỠ LỖI
                        await Clients.Users(partnerIdList).SendAsync("ReceiveDebug", $"Server đang cố gửi thông báo UserOnline cho user ID: {userIdString}");

                        // GỬI EVENT CHÍNH
                        await Clients.Users(partnerIdList).SendAsync("UserOnline", userIdString);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error broadcasting UserOnline status for user {UserId}", userId);
                }
            }

            await base.OnConnectedAsync();
        }
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userIdString = Context.UserIdentifier;
            var connectionId = Context.ConnectionId;

            if (!string.IsNullOrEmpty(userIdString) && int.TryParse(userIdString, out int userId))
            {
                // 1. Xóa kết nối khỏi service quản lý
                _userConnectionManager.RemoveConnection(userIdString, connectionId);

                // 2. Kiểm tra xem user còn kết nối nào khác không
                bool isLastConnection = !_userConnectionManager.GetConnections(userIdString).Any();

                // 3. Nếu đây là kết nối cuối cùng, thông báo user đã offline
                if (isLastConnection)
                {
                    _logger.LogInformation("User {UserId} is now OFFLINE.", userId);
                    try
                    {
                        var conversations = await _chatService.GetUserConversationsServiceAsync(userId);
                        foreach (var convo in conversations)
                        {
                            // Gửi sự kiện "UserOffline" đến những người khác trong cùng group chat
                            await Clients.OthersInGroup(convo.ConversationId.ToString()).SendAsync("UserOffline", userIdString);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error broadcasting UserOffline status for user {UserId}", userId);
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
        public async Task GoOffline()
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            if (UserConnections.TryRemove(userId, out _))
            {
                _logger.LogInformation("User {UserId} proactively went OFFLINE.", userId);
                try
                {
                    var conversations = await _chatService.GetUserConversationsServiceAsync(Convert.ToInt32(userId));
                    foreach (var convo in conversations)
                    {
                        await Clients.OthersInGroup(convo.ConversationId.ToString()).SendAsync("UserOffline", userId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in GoOffline for user {UserId}", userId);
                }
            }
        }
    }
}