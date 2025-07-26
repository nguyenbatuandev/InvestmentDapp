using InvestDapp.Shared.Models.Message;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestDapp.Infrastructure.Data.Config
{
    public class MessageConfig : IEntityTypeConfiguration<Messager>
    {
        public void Configure(EntityTypeBuilder<Messager> builder)
        {
            builder
            .HasOne(m => m.Sender)
            .WithMany(u => u.SentMessages)
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

            builder
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Restrict); // 👈 Thêm dòng này để tránh cascade vòng lặp
        }
    }
}
