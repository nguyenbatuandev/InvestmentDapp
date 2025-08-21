using InvestDapp.Infrastructure.Data;
using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.DTOs.MessageDTO;
using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models;
using InvestDapp.Shared.Models.Message;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace InvestDapp.Application.MessageService
{
    public class ConversationService : IConversationService
    {
        private readonly IConversationRepository _convoRepo;
        private readonly InvestDbContext _context; // Để SaveChanges
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ILogger<ConversationService> _logger;
        private readonly IUserConnectionManager _userConnectionManager;
        public ConversationService(IConversationRepository convoRepo, InvestDbContext context, IHubContext<ChatHub> hubContext, ILogger<ConversationService> logger, IUserConnectionManager userConnectionManager)
        {
            _convoRepo = convoRepo;
            _context = context;
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userConnectionManager = userConnectionManager;
        }


        // Trong file ConversationService.cs

        public async Task<Conversation> StartPrivateChatAsync(int currentUserId, int partnerId)
        {
            _logger.LogInformation("Bắt đầu StartPrivateChatAsync cho User {CurrentUserId} và Partner {PartnerId}", currentUserId, partnerId);

            // 1. Lấy dữ liệu từ Repository
            var conversation = await _convoRepo.StartPrivateChatRepositoryAsync(currentUserId, partnerId);

            if (conversation == null)
            {
                _logger.LogWarning("Repository đã trả về một conversation NULL.");
                return null;
            }

            // 2. Ghi log để kiểm tra số lượng thành viên
            int participantCount = conversation.Participants?.Count ?? 0;
            _logger.LogInformation("Conversation ID {ConversationId} có {Count} thành viên.", conversation.ConversationId, participantCount);

            if (participantCount == 0)
            {
                _logger.LogError("LỖI NGHIÊM TRỌNG: Danh sách thành viên bị rỗng. Logic thêm vào group sẽ bị bỏ qua.");
                return conversation; 
            }

            // 3. Vòng lặp thêm tất cả thành viên vào group SignalR
            foreach (var participant in conversation.Participants)
            {
                var userIdString = participant.UserId.ToString();
                var userConnections = _userConnectionManager.GetConnections(userIdString);

                _logger.LogInformation("Đang xử lý Participant ID {ParticipantId}, tìm thấy {ConnectionCount} kết nối.", participant.UserId, userConnections.Count);

                foreach (var connectionId in userConnections)
                {
                    await _hubContext.Groups.AddToGroupAsync(connectionId, conversation.ConversationId.ToString());
                    _logger.LogInformation("Đã thêm Connection ID {ConnectionId} của User {UserId} vào Group {GroupId}", connectionId, userIdString, conversation.ConversationId);
                }
            }

            return conversation;
        }

        public async Task<Conversation> CreateGroupAsync(int creatorId, string name, List<int> participantIds)
        {
            var newGroup = new Conversation { Type = ConversationType.Group, Name = name };
            await _convoRepo.CreateConversationAsync(newGroup);
            await _context.SaveChangesAsync();

            // Thêm người tạo nhóm vào với vai trò admin
            await _convoRepo.AddParticipantAsync(new Participant { ConversationId = newGroup.ConversationId, UserId = creatorId, Role = ParticipantRole.Admin });

            // Thêm các thành viên khác
            foreach (var userId in participantIds.Where(id => id != creatorId))
            {
                await _convoRepo.AddParticipantAsync(new Participant { ConversationId = newGroup.ConversationId, UserId = userId });
            }

            await _context.SaveChangesAsync();
            return newGroup;
        }

        public async Task<Participant> AddMemberToGroupAsync(int conversationId, int userIdToAdd, int addedByUserId)
        {
            var group = await _convoRepo.FindByIdAsync(conversationId);
            if (group == null || group.Type != ConversationType.Group)
            {
                throw new Exception("Nhóm không tồn tại.");
            }

            // Kiểm tra quyền: người thêm có phải là admin không?
            var adder = group.Participants.FirstOrDefault(p => p.UserId == addedByUserId);
            if (adder == null || adder.Role != ParticipantRole.Admin)
            {
                throw new Exception("Bạn không có quyền thêm thành viên.");
            }

            var newParticipant = new Participant { ConversationId = conversationId, UserId = userIdToAdd };
            await _convoRepo.AddParticipantAsync(newParticipant);
            await _context.SaveChangesAsync();
            return newParticipant;
        }

        // ✅ THÊM HÀM HOÀN CHỈNH ĐANG BỊ THIẾU
        public async Task CreateAndSendMessageAsync(int conversationId, int senderId, string content)
        {

            // 1. Tạo đối tượng tin nhắn mới
            var message = new Messager
            {
                ConversationId = conversationId,
                SenderId = senderId,
                Content = content,
                SentAt = DateTime.Now,
                MessageType = MessageType.Text
            };

            // 2. Lưu tin nhắn vào database
            await _convoRepo.AddMessageAsync(message);
            // 2. TÌM VÀ TĂNG UNREADCOUNT CHO NGƯỜI NHẬN
            var otherParticipants = await _context.Participants
                .Where(p => p.ConversationId == conversationId && p.UserId != senderId)
                .ToListAsync();

            foreach (var participant in otherParticipants)
            {
                participant.UnreadCount++;
            }

            // 3. LƯU TẤT CẢ THAY ĐỔI VÀO DATABASE TRONG MỘT LẦN
            await _context.SaveChangesAsync();

            var a= await _convoRepo.FindByIdAsync(conversationId);

            if (a == null)
            {
                throw new Exception("Conversation not found.");
            }   
            
            a.LastMessageId = message.MessageId;
            await _context.SaveChangesAsync();

            // Lấy lại tin nhắn với thông tin người gửi để gửi về client
            var messageWithSender = await _context.Messagers
                .Include(m => m.Sender)
                .FirstAsync(m => m.MessageId == message.MessageId);

            // 3. Chuẩn bị DTO để gửi đi, tránh lộ thông tin không cần thiết
            var messageDto = new
            {
                messageWithSender.MessageId,
                messageWithSender.ConversationId,
                messageWithSender.Content,
                messageWithSender.SentAt,
                SenderId = messageWithSender.SenderId,

                Sender = new
                {
                    id = messageWithSender.Sender.ID,
                    userId = messageWithSender.Sender.ID,
                    fullName = messageWithSender.Sender.Name,
                    avatarURL = messageWithSender.Sender.Avatar
                }
            };

            // 4. Dùng Hub Context để gửi tin nhắn real-time đến các client trong nhóm
            await _hubContext.Clients
                .Group(conversationId.ToString())
                .SendAsync("ReceiveMessage", messageDto);

            await _context.SaveChangesAsync();
                string json = JsonSerializer.Serialize(messageDto, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json); // Hoặc logger.LogInformation(json);


        }

        public async Task<IEnumerable<Conversation>> GetUserConversationsServiceAsync(int userId)
        {
            return await _convoRepo.GetUserConversationsAsync(userId);
        }

        public Task<IEnumerable<MessageDto>> GetMessagesForConversationAsync(int conversationId, int pageNumber, int pageSize)
        {
            if (conversationId <= 0)
            {
                throw new ArgumentException("Conversation ID must be greater than zero.", nameof(conversationId));
            }
            // Truyền tham số pagination xuống repo
            return _convoRepo.GetMessagesForConversationAsync(conversationId, pageNumber, pageSize);
        }


        public async Task MarkConversationAsReadAsync(int conversationId, int readerUserId)
        {
            var participant = await _context.Participants
           .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == readerUserId);
            var unreadMessages = await _context.Messagers
                .Where(m => m.ConversationId == conversationId &&
                            m.SenderId != readerUserId && 
                            !m.isRead)
                .ToListAsync();

            // 2. Nếu có tin nhắn nào thỏa mãn, cập nhật và lưu
            if (unreadMessages.Any())
            {
                foreach (var message in unreadMessages)
                {
                    message.isRead = true;
                }

                await _context.SaveChangesAsync();
            }
            if (participant != null)
            {
                participant.UnreadCount = 0;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<User>> GetAllUserByConversationIdServiceAsync(int conversationId)
        {
            return await _convoRepo.GetAllUserByConversationIdRepositoryAsync(conversationId);
        }

        public Task<IEnumerable<ConversationDto>> MapConversationsToDtosAsync(IEnumerable<Conversation> conversations, int currentUserId)
        {
            var conversationDtos = conversations.Select(c => new ConversationDto
            {
                ConversationId = c.ConversationId,
                Type = c.Type,
                Name = c.Name,
                AvatarURL = c.AvatarURL,

                UnreadCount = c.Messages?.Count(m => !m.isRead && m.SenderId != currentUserId) ?? 0,

                Participants = c.Participants.Select(p => new ParticipantDto
                {
                    UserId = p.UserId,
                    User = new UserDto
                    {
                        UserId = p.User.ID,
                        FullName = p.User.Name,
                        AvatarURL = p.User.Avatar,
                    }
                }).ToList(),

                LastMessage = c.LastMessage == null ? null : new MessageDto
                {
                    MessageId = c.LastMessage.MessageId,
                    Content = c.LastMessage.Content,
                    SentAt = c.LastMessage.SentAt,
                    SenderId = c.LastMessage.SenderId,
                    Sender = c.LastMessage.Sender == null ? null : new UserDto
                    {
                        UserId = c.LastMessage.Sender.ID,
                        FullName = c.LastMessage.Sender.Name,
                        AvatarURL = c.LastMessage.Sender.Avatar
                    }
                }
            });

            return Task.FromResult(conversationDtos);
        }

        public async Task<int> GetTotalUnreadCountAsync(int userId)
        {
            var totalUnreadCount = await _context.Participants
                       .Where(p => p.UserId == userId)
                       .SumAsync(p => p.UnreadCount);

            return totalUnreadCount;
        }
    }
}
