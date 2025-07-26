using InvestDapp.Shared.Models.Message;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace InvestDapp.Infrastructure.Data.Config
{
    public class MessageReadStatusConfnig : IEntityTypeConfiguration<MessageReadStatus>
    {
        public void Configure(EntityTypeBuilder<MessageReadStatus> builder)
        {
            builder.HasKey(mrs => new { mrs.MessageId, mrs.UserId });
        }
    }
}
