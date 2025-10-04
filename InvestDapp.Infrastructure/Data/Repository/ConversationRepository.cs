using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.DTOs.MessageDTO;
using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models;
using InvestDapp.Shared.Models.Message;
using Microsoft.EntityFrameworkCore;

namespace InvestDapp.Infrastructure.Data.Repository
{
    public class ConversationRepository : IConversationRepository
    {
        private readonly InvestDbContext _context;

        public ConversationRepository(InvestDbContext context)
        {
            _context = context;
        }

        public async Task<Conversation?> GetPrivateConversationAsync(int userId1, int userId2)
        {
            // Tìm cuộc hội thoại private có chính xác 2 thành viên này
            return await _context.Conversations
                .Include(c => c.Participants)
                .Where(c => c.Type == ConversationType.Private &&
                            c.Participants.Count() == 2 &&
                            c.Participants.Any(p => p.UserId == userId1) &&
                            c.Participants.Any(p => p.UserId == userId2))
                .FirstOrDefaultAsync();
        }

        public async Task<Conversation> CreateConversationAsync(Conversation conversation)
        {
            await _context.Conversations.AddAsync(conversation);
            await _context.SaveChangesAsync();
            return conversation;
        }

        public async Task AddParticipantAsync(Participant participant)
        {
            await _context.Participants.AddAsync(participant);
        }

        // File: Infrastructure/Data/Repository/ConversationRepository.cs

        // Trong file: ConversationRepository.cs
        public async Task<IEnumerable<Conversation>> GetUserConversationsAsync(int userId)
        {
            return await _context.Conversations
                .Where(c => c.Participants.Any(p => p.UserId == userId))
                .Include(c => c.Participants)
                    .ThenInclude(p => p.User)
                .Include(c => c.LastMessage)
                    .ThenInclude(m => m.Sender)
                .Include(c => c.Messages)
                .Include(c => c.Campaign)
                .OrderByDescending(c => c.LastMessage != null ? c.LastMessage.SentAt : c.CreatedAt)
                .ToListAsync();
        }

        public async Task<Conversation?> FindByIdAsync(int conversationId)
        {
            return await _context.Conversations
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.ConversationId == conversationId);
        }

        public async Task<Messager> AddMessageAsync(Messager message)
        {
            await _context.Messagers.AddAsync(message);
            return message;
        }

        // File: Infrastructure/Data/Repository/ConversationRepository.cs

        // THAY ĐỔI TOÀN BỘ PHƯƠNG THỨC NÀY
        public async Task<IEnumerable<MessageDto>> GetMessagesForConversationAsync(int conversationId, int pageNumber, int pageSize)
        {
            return await _context.Messagers
                .Where(m => m.ConversationId == conversationId)
                .Include(m => m.Sender)
                // Sắp xếp từ mới nhất -> cũ nhất để lấy trang gần nhất
                .OrderByDescending(m => m.SentAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                // Sắp xếp lại từ cũ -> mới để hiển thị đúng thứ tự trên client
                .OrderBy(m => m.SentAt)
                .Select(m => new MessageDto
                {
                    MessageId = m.MessageId,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    SenderId = m.SenderId,
                    IsRead = m.isRead, 
                    Sender = new UserDto
                    {
                        UserId = m.Sender.ID,
                        FullName = m.Sender.Name,
                        AvatarURL = m.Sender.Avatar
                    }
                })
                .ToListAsync();
        }

        // Trong file ConversationRepository.cs

        public async Task<Conversation> StartPrivateChatRepositoryAsync(int currentUserId, int partnerId)
        {
            // Tìm cuộc hội thoại riêng tư đã tồn tại giữa 2 người
            var existingConversation = await _context.Conversations
                .Include(c => c.Participants) 
                .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(c =>
                    c.Type == ConversationType.Private &&
                    c.Participants.Count == 2 &&
                    c.Participants.All(p => p.UserId == currentUserId || p.UserId == partnerId));

            // Nếu đã có, trả về ngay lập tức
            if (existingConversation != null)
            {
                return existingConversation;
            }

            // Nếu chưa có, tạo mới
            var newConversation = new Conversation
            {
                Type = ConversationType.Private,
                Participants = new List<Participant>
        {
            new Participant { UserId = currentUserId },
            new Participant { UserId = partnerId }
        }
            };

            // Lưu vào cơ sở dữ liệu
            await _context.Conversations.AddAsync(newConversation);
            await _context.SaveChangesAsync();

            // Tải lại cuộc hội thoại vừa tạo để đảm bảo có đủ dữ liệu Participants và User
            var createdConversation = await _context.Conversations
                .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                .FirstAsync(c => c.ConversationId == newConversation.ConversationId);

            return createdConversation;
        }
        public async Task<IEnumerable<User>> GetAllUserByConversationIdRepositoryAsync(int conversationId)
        {
           return await _context.Participants
                .Where(p => p.ConversationId == conversationId)
                .Select(p => p.User)
                .ToListAsync();
        }
    }
}
