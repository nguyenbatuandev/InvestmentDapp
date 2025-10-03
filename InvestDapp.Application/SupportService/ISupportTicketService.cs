using InvestDapp.Shared.Common;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.Common.Respone;
using Microsoft.AspNetCore.Http;

namespace InvestDapp.Application.SupportService
{
    public interface ISupportTicketService
    {
        Task<BaseResponse<SupportTicketDetailResponse>> CreateTicketAsync(CreateSupportTicketRequest request, int userId, IEnumerable<IFormFile>? attachments, CancellationToken cancellationToken = default);
        Task<BaseResponse<SupportTicketListResult>> GetTicketsForUserAsync(int userId, SupportTicketFilterRequest filter, CancellationToken cancellationToken = default);
        Task<BaseResponse<SupportTicketDetailResponse>> GetTicketDetailForUserAsync(int ticketId, int userId, CancellationToken cancellationToken = default);
        Task<BaseResponse<SupportTicketDetailResponse>> AddMessageFromUserAsync(AddSupportTicketMessageRequest request, int userId, IEnumerable<IFormFile>? attachments, CancellationToken cancellationToken = default);
        Task<BaseResponse<SupportTicketListResult>> GetTicketsForAdminAsync(SupportTicketFilterRequest filter, CancellationToken cancellationToken = default);
        Task<BaseResponse<SupportTicketDetailResponse>> GetTicketDetailForAdminAsync(int ticketId, CancellationToken cancellationToken = default);
        Task<BaseResponse<bool>> AddMessageFromStaffAsync(AddSupportTicketMessageRequest request, int staffUserId, bool markAsResolved, IEnumerable<IFormFile>? attachments, CancellationToken cancellationToken = default);
        Task<BaseResponse<bool>> AssignTicketAsync(AssignSupportTicketRequest request, int assignedByUserId, CancellationToken cancellationToken = default);
        Task<BaseResponse<bool>> UpdateStatusAsync(int ticketId, int staffUserId, bool resolve, bool close, CancellationToken cancellationToken = default);
        Task<BaseResponse<IReadOnlyList<SupportStaffSummaryResponse>>> GetAssignableStaffAsync(CancellationToken cancellationToken = default);
        Task EvaluateSlaAsync(CancellationToken cancellationToken = default);
    }
}
