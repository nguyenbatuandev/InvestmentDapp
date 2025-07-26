using InvestDapp.Shared.Models.Message;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace InvestDapp.Infrastructure.Data.Config
{
    public class ParticipantConfig : IEntityTypeConfiguration<Participant>
    {
        public void Configure(EntityTypeBuilder<Participant> builder)
        {
            builder.HasKey(p => new { p.UserId, p.ConversationId });
        }
    }
}
