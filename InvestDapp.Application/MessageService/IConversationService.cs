using InvestDapp.Shared.DTOs.MessageDTO;
using InvestDapp.Shared.Models;
using InvestDapp.Shared.Models.Message;

namespace InvestDapp.Application.MessageService
{
    public interface IConversationService
    {
        Task CreateAndSendMessageAsync(int conversationId, int senderId, string content);
        Task SendPostNotificationToCampaignGroupAsync(int campaignId, int authorUserId, string postTitle, string postUrl);
        Task CreateCampaignGroupAsync(int campaignId, string groupName, int ownerId);

        Task<Conversation> StartPrivateChatAsync(int currentUserId, int partnerId);
        Task<Conversation> CreateGroupAsync(int creatorId, string name, List<int> participantIds);
        Task<Participant> AddMemberToGroupAsync(int conversationId, int userIdToAdd, int addedByUserId);
        Task<IEnumerable<Conversation>> GetUserConversationsServiceAsync(int userId);

        Task<IEnumerable<MessageDto>> GetMessagesForConversationAsync(int conversationId, int pageNumber, int pageSize);

        Task<IEnumerable<User>> GetAllUserByConversationIdServiceAsync(int conversationId);
        Task<IEnumerable<ConversationDto>> MapConversationsToDtosAsync(IEnumerable<Conversation> conversations, int currentUserId);

        Task MarkConversationAsReadAsync(int conversationId, int readerUserId);

        Task <int> GetTotalUnreadCountAsync(int userId);

    }
}
