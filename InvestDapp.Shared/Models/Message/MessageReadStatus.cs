using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Models.Message
{
    public class MessageReadStatus
    {
        // --- Composite Primary Key (cấu hình trong DbContext) ---
        public int MessageId { get; set; }
        public int UserId { get; set; }

        public DateTime ReadAt { get; set; } = DateTime.Now;

        // --- Navigation Properties ---
        public virtual Messager Message { get; set; }
        public virtual User User { get; set; }
    }
}
