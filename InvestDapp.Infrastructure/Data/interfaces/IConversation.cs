using InvestDapp.Shared.DTOs.MessageDTO;
using InvestDapp.Shared.Models;
using InvestDapp.Shared.Models.Message;


namespace InvestDapp.Infrastructure.Data.interfaces
{
    public interface IConversationRepository
    {
        Task<Conversation?> GetPrivateConversationAsync(int userId1, int userId2);
        Task<Conversation> CreateConversationAsync(Conversation conversation);
        Task AddParticipantAsync(Participant participant);
        Task<IEnumerable<Conversation>> GetUserConversationsAsync(int userId);
        Task<Conversation?> FindByIdAsync(int conversationId); // Thêm hàm tìm kiếm
        Task<Messager> AddMessageAsync(Messager message);
        Task<IEnumerable<MessageDto>> GetMessagesForConversationAsync(int conversationId, int pageNumber, int pageSize);
        Task<Conversation> StartPrivateChatRepositoryAsync(int currentUserId, int partnerId);

        Task<IEnumerable<User>> GetAllUserByConversationIdRepositoryAsync(int conversationId);

    }
}
