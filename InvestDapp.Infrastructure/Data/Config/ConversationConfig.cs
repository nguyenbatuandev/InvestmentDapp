using InvestDapp.Shared.Models.Message;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace InvestDapp.Infrastructure.Data.Config
{
    public class ConversationConfig : IEntityTypeConfiguration<Conversation>
    {
        public void Configure(EntityTypeBuilder<Conversation> builder)
        {
            builder
                .HasOne(c => c.LastMessage)
                .WithMany()
                .HasForeignKey(c => c.LastMessageId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
