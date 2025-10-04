using InvestDapp.Infrastructure.Data;
using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.DTOs.MessageDTO;
using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models;
using InvestDapp.Shared.Models.Message;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
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
            //var adder = group.Participants.FirstOrDefault(p => p.UserId == addedByUserId);
            //if (adder == null || adder.Role != ParticipantRole.Admin)
            //{
            //    throw new Exception("Bạn không có quyền thêm thành viên.");
            //}


            var newParticipant = new Participant { ConversationId = conversationId, UserId = userIdToAdd, Role =  ParticipantRole.Member};
            await _convoRepo.AddParticipantAsync(newParticipant);
            await _context.SaveChangesAsync();
            return newParticipant;
        }

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

            // 2. Lưu tin nhắn vào database VÀ SAVECHANGES ĐỂ CÓ MessageId
            await _convoRepo.AddMessageAsync(message);
            await _context.SaveChangesAsync(); // ✅ SAVE NGAY ĐỂ CÓ message.MessageId
            
            // 3. Tìm và tăng UnreadCount cho người nhận
            var otherParticipants = await _context.Participants
                .Where(p => p.ConversationId == conversationId && p.UserId != senderId)
                .ToListAsync();

            foreach (var participant in otherParticipants)
            {
                participant.UnreadCount++;
            }

            // 4. Tìm conversation và set LastMessageId (BÂY GIỜ message.MessageId đã có giá trị)
            var conversation = await _convoRepo.FindByIdAsync(conversationId);
            if (conversation == null)
            {
                throw new Exception("Conversation not found.");
            }   
            conversation.LastMessageId = message.MessageId;

            // 5. ✅ LƯU THAY ĐỔI UnreadCount VÀ LastMessageId
            await _context.SaveChangesAsync();

            // 6. Lấy lại tin nhắn với thông tin người gửi để gửi về client
            var messageWithSender = await _context.Messagers
                .Include(m => m.Sender)
                .FirstAsync(m => m.MessageId == message.MessageId);

            // 7. Chuẩn bị DTO để gửi đi
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

            // 8. Gửi tin nhắn real-time đến các client trong nhóm
            await _hubContext.Clients
                .Group(conversationId.ToString())
                .SendAsync("ReceiveMessage", messageDto);

            // 9. ✅ Emit UnreadChanged cho mỗi người nhận (SAU KHI ĐÃ SAVECHANGES)
            foreach (var participant in otherParticipants)
            {
                var newUnreadCount = await GetTotalUnreadCountAsync(participant.UserId);
                await _hubContext.Clients
                    .User(participant.UserId.ToString())
                    .SendAsync("UnreadChanged", newUnreadCount);
            }
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
            Console.WriteLine($"🔵 MarkConversationAsReadAsync called: ConversationId={conversationId}, UserId={readerUserId}");
            
            // ✅ Bắt đầu transaction để đảm bảo atomic
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var participant = await _context.Participants
                   .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == readerUserId);
                
                if (participant == null)
                {
                    Console.WriteLine($"⚠️ Participant not found for ConversationId={conversationId}, UserId={readerUserId}");
                    await transaction.RollbackAsync();
                    return;
                }
                
                Console.WriteLine($"🔵 Before: Participant.UnreadCount = {participant.UnreadCount}");
                
                var unreadMessages = await _context.Messagers
                    .Where(m => m.ConversationId == conversationId &&
                                m.SenderId != readerUserId && 
                                !m.isRead)
                    .ToListAsync();

                Console.WriteLine($"🔵 Found {unreadMessages.Count} unread messages");

                // Đánh dấu tất cả tin nhắn đã đọc
                if (unreadMessages.Any())
                {
                    foreach (var message in unreadMessages)
                    {
                        message.isRead = true;
                    }
                }
                
                // Reset UnreadCount của participant
                participant.UnreadCount = 0;
                
                Console.WriteLine($"🔵 After: Participant.UnreadCount = {participant.UnreadCount}");
                
                // ✅ LƯU TẤT CẢ THAY ĐỔI
                var saveResult = await _context.SaveChangesAsync();
                Console.WriteLine($"✅ SaveChanges: {saveResult} rows affected");
                
                // ✅ Commit transaction
                await transaction.CommitAsync();
                
                Console.WriteLine($"✅ MarkConversationAsReadAsync completed. Committed to DB.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in MarkConversationAsReadAsync: {ex.Message}");
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<IEnumerable<User>> GetAllUserByConversationIdServiceAsync(int conversationId)
        {
            return await _convoRepo.GetAllUserByConversationIdRepositoryAsync(conversationId);
        }

        public Task<IEnumerable<ConversationDto>> MapConversationsToDtosAsync(IEnumerable<Conversation> conversations, int currentUserId)
        {
            Console.WriteLine($"📋 MapConversationsToDtosAsync: CurrentUserId={currentUserId}, Conversations={conversations.Count()}");
            
            var conversationDtos = conversations.Select(c =>
            {
                var participant = c.Participants?.FirstOrDefault(p => p.UserId == currentUserId);
                var unreadCount = participant?.UnreadCount ?? 0;
                
                Console.WriteLine($"   - ConversationId={c.ConversationId}, Participant.UnreadCount={unreadCount}");
                
                return new ConversationDto
                {
                    ConversationId = c.ConversationId,
                    Type = c.Type,
                    Name = c.Name,
                    AvatarURL = c.AvatarURL,

                    // Dùng Participant.UnreadCount làm nguồn chân lý
                    UnreadCount = unreadCount,

                    Participants = c.Participants?.Select(p => new ParticipantDto
                    {
                        UserId = p?.UserId ?? 0,
                        Role = p?.Role ?? 0,
                        User = new UserDto
                        {
                            UserId = p?.User?.ID ?? 0,
                            FullName = p?.User?.Name ?? "",
                            AvatarURL = p?.User?.Avatar ?? "",
                        }
                    }).ToList() ?? new List<ParticipantDto>(),

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
                    },

                    Campaign = c.Campaign == null ? null : new CampaignDto
                    {
                        Id = c.Campaign.Id,
                        Name = c.Campaign.Name,
                        ImageUrl = c.Campaign.ImageUrl
                    }
                };
            });

            return Task.FromResult(conversationDtos);
        }

        public async Task<int> GetTotalUnreadCountAsync(int userId)
        {
            var participants = await _context.Participants
                       .Where(p => p.UserId == userId)
                       .ToListAsync();
            
            var totalUnreadCount = participants.Sum(p => p.UnreadCount);
            
            Console.WriteLine($"📊 GetTotalUnreadCountAsync: UserId={userId}, Conversations={participants.Count}, TotalUnread={totalUnreadCount}");
            foreach (var p in participants)
            {
                Console.WriteLine($"   - ConversationId={p.ConversationId}, UnreadCount={p.UnreadCount}");
            }

            return totalUnreadCount;
        }

        public async Task SendPostNotificationToCampaignGroupAsync(int campaignId, int authorUserId, string postTitle, string postUrl)
        {
            try
            {
                _logger.LogInformation("Bắt đầu gửi thông báo post cho campaign {CampaignId}, author {AuthorUserId}", campaignId, authorUserId);

                var conversation = await _context.Conversations
                    .FirstOrDefaultAsync(c => c.CampaignId == campaignId && c.Type == ConversationType.Group);

                if (conversation == null)
                {
                    _logger.LogWarning("Không tìm thấy group chat cho campaign ID {CampaignId}", campaignId);
                    
                    var allConversations = await _context.Conversations
                        .Where(c => c.CampaignId == campaignId)
                        .ToListAsync();
                    
                    _logger.LogInformation("Tìm thấy {Count} conversations cho campaign {CampaignId}: {ConversationIds}", 
                        allConversations.Count, campaignId, string.Join(", ", allConversations.Select(c => $"ID:{c.ConversationId},Type:{c.Type}")));
                    return;
                }

                _logger.LogInformation("Tìm thấy group chat ID {ConversationId} cho campaign {CampaignId}", conversation.ConversationId, campaignId);

                var participant = await _context.Participants
                    .FirstOrDefaultAsync(p => p.ConversationId == conversation.ConversationId && p.UserId == authorUserId);

                if (participant == null)
                {
                    _logger.LogWarning("User {UserId} không có trong group chat {ConversationId}", authorUserId, conversation.ConversationId);
                    return;
                }

                var message = $"📝 Bài viết mới:\n\"{postTitle}\"\n🔗 Xem chi tiết: {postUrl}";

                _logger.LogInformation("Đang gửi link bài post vào group {ConversationId}: {Message}", conversation.ConversationId, message);

                await CreateAndSendMessageAsync(conversation.ConversationId, authorUserId, message);

                _logger.LogInformation("Đã gửi thông báo bài post mới vào group chat campaign {CampaignId}", campaignId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi thông báo bài post vào group chat campaign {CampaignId}", campaignId);
            }
        }

        public async Task CreateCampaignGroupAsync(int campaignId, string groupName, int ownerId)
        {
            try
            {
                // Kiểm tra xem group đã tồn tại chưa
                var existingGroup = await _context.Conversations
                    .FirstOrDefaultAsync(c => c.CampaignId == campaignId && c.Type == ConversationType.Group);

                if (existingGroup != null)
                {
                    _logger.LogInformation("Group chat cho campaign {CampaignId} đã tồn tại", campaignId);
                    return;
                }

                // Tạo group chat mới
                var newGroup = new Conversation 
                { 
                    Type = ConversationType.Group, 
                    Name = groupName,
                    CampaignId = campaignId,
                    CreatedAt = DateTime.UtcNow
                };

                await _convoRepo.CreateConversationAsync(newGroup);
                await _context.SaveChangesAsync();

                // Thêm owner vào group với vai trò admin
                await _convoRepo.AddParticipantAsync(new Participant 
                { 
                    ConversationId = newGroup.ConversationId, 
                    UserId = ownerId,
                    Role = ParticipantRole.Admin
                });

                await _context.SaveChangesAsync();

                _logger.LogInformation("Đã tạo group chat thành công cho campaign {CampaignId} với ConversationId {ConversationId}", 
                    campaignId, newGroup.ConversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo group chat cho campaign {CampaignId}", campaignId);
                // Không throw exception để không ảnh hưởng đến việc tạo campaign
            }
        }
    }
}
