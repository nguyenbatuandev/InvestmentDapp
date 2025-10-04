using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using InvestDapp.Infrastructure.Data.interfaces;

namespace InvestDapp.Application.MessageService
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IConversationService _chatService;
        private readonly IUserConnectionManager _userConnectionManager;
        private readonly ILogger<ChatHub> _logger;
        private readonly IUser _userRepository;

        public ChatHub(IConversationService chatService, ILogger<ChatHub> logger, IUserConnectionManager userConnectionManager, IUser userRepository)
        {
            _chatService = chatService;
            _logger = logger;
            _userConnectionManager = userConnectionManager;
            _userRepository = userRepository;
        }

        /// <summary>
        /// Helper method to resolve Context.UserIdentifier to a numeric user ID.
        /// Handles both numeric IDs (regular users) and wallet addresses (admin users).
        /// </summary>
        private async Task<int?> GetCurrentUserIdAsync()
        {
            var userIdString = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userIdString))
            {
                _logger.LogWarning("Context.UserIdentifier is null or empty");
                return null;
            }

            // Try to parse as numeric ID first (regular users)
            if (int.TryParse(userIdString, out int userId))
            {
                return userId;
            }

            // If not numeric, it's probably a wallet address (admin users)
            _logger.LogInformation("UserIdentifier is wallet address: {Wallet}", userIdString);
            var user = await _userRepository.GetUserByWalletAddressAsync(userIdString);
            
            if (user == null)
            {
                _logger.LogWarning("User not found for wallet: {Wallet}", userIdString);
                return null;
            }

            _logger.LogInformation("Resolved wallet {Wallet} to UserId {UserId}", userIdString, user.ID);
            return user.ID;
        }

        public async Task SendMessage(int conversationId, int userId, string content)
        {
            await _chatService.CreateAndSendMessageAsync(conversationId, userId, content);
        }

        public async Task SendFileMessage(int conversationId, int userId, string fileUrl, string fileType, string fileName)
        {
            // Tạo nội dung message dạng JSON để chứa thông tin file
            var messageContent = System.Text.Json.JsonSerializer.Serialize(new
            {
                type = fileType,
                url = fileUrl,
                name = fileName
            });
            
            await _chatService.CreateAndSendMessageAsync(conversationId, userId, messageContent);
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
            var startTime = DateTime.UtcNow;
            var readerUserId = await GetCurrentUserIdAsync();
            if (readerUserId == null)
            {
                _logger.LogWarning("MarkConversationAsRead: Could not resolve user ID");
                return;
            }

            _logger.LogInformation($"🟢 [T+0ms] ChatHub.MarkConversationAsRead: ConversationId={conversationId}, UserId={readerUserId}");

            // 1. Đánh dấu đã đọc trong DB
            await _chatService.MarkConversationAsReadAsync(conversationId, readerUserId.Value);
            _logger.LogInformation($"⏱️ [T+{(DateTime.UtcNow - startTime).TotalMilliseconds}ms] Marked as read in DB");

            // 2. ✅ Lấy tổng số tin nhắn chưa đọc mới và emit UnreadChanged
            var newUnreadCount = await _chatService.GetTotalUnreadCountAsync(readerUserId.Value);
            _logger.LogInformation($"⏱️ [T+{(DateTime.UtcNow - startTime).TotalMilliseconds}ms] Got new unread count: {newUnreadCount}");
            
            _logger.LogInformation($"🟢 Emitting UnreadChanged: UserId={readerUserId}, NewCount={newUnreadCount}");
            
            await Clients.User(readerUserId.Value.ToString()).SendAsync("UnreadChanged", newUnreadCount);
            _logger.LogInformation($"⏱️ [T+{(DateTime.UtcNow - startTime).TotalMilliseconds}ms] Emitted UnreadChanged event");

            // 3. Lấy những người còn lại trong conversation
            var participants = await _chatService.GetAllUserByConversationIdServiceAsync(conversationId);

            // 4. Tạo payload thông báo
            var data = new
            {
                ConversationId = conversationId,
                ReaderUserId = readerUserId.Value
            };

            // 5. Gửi thông báo đến các user còn lại
            foreach (var user in participants)
            {
                if (user.ID != readerUserId.Value)
                {
                    await Clients.User(user.ID.ToString())
                        .SendAsync("MessagesRead", data);
                }
            }
            
            _logger.LogInformation($"✅ ChatHub.MarkConversationAsRead completed");
        }




        // Trong file ChatHub.cs

        // ✅ HÀM ONCONNECTEDASYNC PHIÊN BẢN SỬA LỖI HOÀN CHỈNH
        // Trong file ChatHub.cs

        // HÀM ONCONNECTEDASYNC PHIÊN BẢN CÓ GỠ LỖI
        public override async Task OnConnectedAsync()
        {
            var userId = await GetCurrentUserIdAsync();
            var connectionId = Context.ConnectionId;

            if (userId == null)
            {
                _logger.LogWarning("OnConnectedAsync: Could not resolve user ID");
                await base.OnConnectedAsync();
                return;
            }

            // ✅ Dùng numeric userId làm key thống nhất
            var userIdString = userId.Value.ToString();

            // 1. Quản lý kết nối
            bool isFirstConnection = !_userConnectionManager.GetConnections(userIdString).Any();
            _userConnectionManager.AddConnection(userIdString, connectionId);

            // 2. Tự động tham gia các group chat
            var conversations = await _chatService.GetUserConversationsServiceAsync(userId.Value);
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
                            if (participant.UserId != userId.Value)
                            {
                                partnerUserIds.Add(participant.UserId.ToString());
                            }
                        }
                    }

                    if (partnerUserIds.Any())
                    {
                        var partnerIdList = partnerUserIds.ToList();
                        _logger.LogInformation("[BROADCAST CHECK] Chuẩn bị gửi 'UserOnline' của User {UserId} đến các partner: {PartnerIds}", userId.Value, string.Join(", ", partnerIdList));

                        // GỬI MỘT EVENT RIÊNG ĐỂ GỠ LỖI
                        await Clients.Users(partnerIdList).SendAsync("ReceiveDebug", $"Server đang cố gửi thông báo UserOnline cho user ID: {userId.Value}");

                        // GỬI EVENT CHÍNH
                        await Clients.Users(partnerIdList).SendAsync("UserOnline", userId.Value.ToString());
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
            var userId = await GetCurrentUserIdAsync();
            var connectionId = Context.ConnectionId;

            if (userId != null)
            {
                var userIdString = userId.Value.ToString();
                
                // 1. Xóa kết nối khỏi service quản lý
                _userConnectionManager.RemoveConnection(userIdString, connectionId);

                // 2. Kiểm tra xem user còn kết nối nào khác không
                bool isLastConnection = !_userConnectionManager.GetConnections(userIdString).Any();

                // 3. Nếu đây là kết nối cuối cùng, thông báo user đã offline
                if (isLastConnection)
                {
                    _logger.LogInformation("User {UserId} is now OFFLINE.", userId.Value);
                    try
                    {
                        var conversations = await _chatService.GetUserConversationsServiceAsync(userId.Value);
                        foreach (var convo in conversations)
                        {
                            // Gửi sự kiện "UserOffline" đến những người khác trong cùng group chat
                            await Clients.OthersInGroup(convo.ConversationId.ToString()).SendAsync("UserOffline", userId.Value.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error broadcasting UserOffline status for user {UserId}", userId.Value);
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
        public async Task GoOffline()
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
            {
                _logger.LogWarning("GoOffline: Could not resolve user ID");
                return;
            }

            var userIdString = userId.Value.ToString();
            
            var connections = _userConnectionManager.GetConnections(userIdString);
            if (connections.Any())
            {
                // Remove tất cả connections của user này
                foreach (var connId in connections.ToList())
                {
                    _userConnectionManager.RemoveConnection(userIdString, connId);
                }
                
                _logger.LogInformation("User {UserId} proactively went OFFLINE.", userId.Value);
                try
                {
                    var conversations = await _chatService.GetUserConversationsServiceAsync(userId.Value);
                    foreach (var convo in conversations)
                    {
                        await Clients.OthersInGroup(convo.ConversationId.ToString()).SendAsync("UserOffline", userIdString);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in GoOffline for user {UserId}", userId.Value);
                }
            }
        }
    }
}