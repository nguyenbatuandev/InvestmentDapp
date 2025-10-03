using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Enums;
using InvestDapp.Shared.Models.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvestDapp.Infrastructure.Data.Repository
{
    public class SupportTicketRepository : ISupportTicketRepository
    {
        private readonly InvestDbContext _context;
        private readonly ILogger<SupportTicketRepository> _logger;

        public SupportTicketRepository(InvestDbContext context, ILogger<SupportTicketRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SupportTicket> CreateTicketAsync(SupportTicket ticket, SupportTicketMessage firstMessage, IEnumerable<SupportTicketAttachment>? attachments, CancellationToken cancellationToken = default)
        {
            if (ticket == null) throw new ArgumentNullException(nameof(ticket));
            if (firstMessage == null) throw new ArgumentNullException(nameof(firstMessage));

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                _context.SupportTickets.Add(ticket);
                await _context.SaveChangesAsync(cancellationToken);

                firstMessage.TicketId = ticket.Id;
                firstMessage.CreatedAt = firstMessage.CreatedAt == default ? DateTime.UtcNow : firstMessage.CreatedAt;
                _context.SupportTicketMessages.Add(firstMessage);
                await _context.SaveChangesAsync(cancellationToken);

                if (attachments != null)
                {
                    foreach (var attachment in attachments)
                    {
                        attachment.MessageId = firstMessage.Id;
                        _context.SupportTicketAttachments.Add(attachment);
                    }
                    await _context.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return ticket;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to create support ticket for user {UserId}", ticket.UserId);
                throw;
            }
        }

        public async Task<SupportTicket?> GetTicketByIdAsync(int ticketId, bool includeDetails = false, CancellationToken cancellationToken = default)
        {
            IQueryable<SupportTicket> query = _context.SupportTickets.AsQueryable();

            if (includeDetails)
            {
                query = query
                    .Include(t => t.User)
                    .Include(t => t.AssignedToUser)
                    .Include(t => t.Messages)
                        .ThenInclude(m => m.SenderUser)
                    .Include(t => t.Messages)
                        .ThenInclude(m => m.Attachments)
                    .Include(t => t.Assignments)
                        .ThenInclude(a => a.AssignedToUser)
                    .Include(t => t.Assignments)
                        .ThenInclude(a => a.AssignedByUser);
            }

            return await query.FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
        }

        public async Task<(IReadOnlyList<SupportTicket> Items, int Total)> QueryTicketsAsync(SupportTicketQuery query, CancellationToken cancellationToken = default)
        {
            IQueryable<SupportTicket> baseQuery = _context.SupportTickets
                .Include(t => t.User)
                .Include(t => t.AssignedToUser)
                .AsNoTracking();

            if (query.UserId.HasValue)
            {
                baseQuery = baseQuery.Where(t => t.UserId == query.UserId.Value);
            }

            if (query.AssignedToUserId.HasValue)
            {
                if (query.AssignedToUserId.Value == -1)
                {
                    baseQuery = baseQuery.Where(t => t.AssignedToUserId == null);
                }
                else
                {
                    baseQuery = baseQuery.Where(t => t.AssignedToUserId == query.AssignedToUserId.Value);
                }
            }

            if (query.Status.HasValue)
            {
                baseQuery = baseQuery.Where(t => t.Status == query.Status.Value);
            }

            if (query.Priority.HasValue)
            {
                baseQuery = baseQuery.Where(t => t.Priority == query.Priority.Value);
            }

            if (query.SlaStatus.HasValue)
            {
                baseQuery = baseQuery.Where(t => t.SlaStatus == query.SlaStatus.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var keyword = query.Keyword.Trim().ToLower();
                baseQuery = baseQuery.Where(t => t.TicketCode.ToLower().Contains(keyword)
                                                 || t.Subject.ToLower().Contains(keyword)
                                                 || (t.Category != null && t.Category.ToLower().Contains(keyword)));
            }

            baseQuery = baseQuery.OrderByDescending(t => t.CreatedAt);

            var total = await baseQuery.CountAsync(cancellationToken);

            if (query.Skip > 0)
            {
                baseQuery = baseQuery.Skip(query.Skip);
            }

            if (query.Take > 0)
            {
                baseQuery = baseQuery.Take(query.Take);
            }

            if (query.IncludeMessages)
            {
                baseQuery = baseQuery
                    .Include(t => t.Messages)
                        .ThenInclude(m => m.Attachments)
                    .Include(t => t.Messages)
                        .ThenInclude(m => m.SenderUser);
            }

            var items = await baseQuery.ToListAsync(cancellationToken);
            return (items, total);
        }

        public async Task AddMessageAsync(SupportTicketMessage message, IEnumerable<SupportTicketAttachment>? attachments, CancellationToken cancellationToken = default)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            _context.SupportTicketMessages.Add(message);
            await _context.SaveChangesAsync(cancellationToken);

            if (attachments != null)
            {
                foreach (var attachment in attachments)
                {
                    attachment.MessageId = message.Id;
                    _context.SupportTicketAttachments.Add(attachment);
                }
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task AssignAsync(int ticketId, int assignedToUserId, int assignedByUserId, string? notes, CancellationToken cancellationToken = default)
        {
            var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
            if (ticket == null)
            {
                throw new InvalidOperationException($"Ticket {ticketId} not found");
            }

            ticket.AssignedToUserId = assignedToUserId;
            ticket.AssignedAt = DateTime.UtcNow;
            ticket.UpdatedAt = DateTime.UtcNow;

            var history = new SupportTicketAssignment
            {
                TicketId = ticketId,
                AssignedAt = ticket.AssignedAt.Value,
                AssignedToUserId = assignedToUserId,
                AssignedByUserId = assignedByUserId,
                Notes = notes
            };

            _context.SupportTicketAssignments.Add(history);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateTicketAsync(SupportTicket ticket, CancellationToken cancellationToken = default)
        {
            _context.SupportTickets.Update(ticket);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<SupportTicket>> GetOpenTicketsAsync(CancellationToken cancellationToken = default)
        {
            var openStatuses = new[]
            {
                SupportTicketStatus.New,
                SupportTicketStatus.InProgress,
                SupportTicketStatus.WaitingForCustomer,
                SupportTicketStatus.Escalated
            };

            return await _context.SupportTickets
                .Where(t => openStatuses.Contains(t.Status))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<SupportStaffSummary>> GetAssignableStaffAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(u => u.Role != null && (u.Role.ToLower().Contains("admin") || u.Role.ToLower().Contains("support")))
                .OrderBy(u => u.Name)
                .Select(u => new SupportStaffSummary(u.ID, string.IsNullOrWhiteSpace(u.Name) ? u.WalletAddress : u.Name, u.Email))
                .ToListAsync(cancellationToken);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
