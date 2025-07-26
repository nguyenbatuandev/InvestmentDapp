using InvestDapp.Shared.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Models.Message
{
    /// <summary>
    /// Đại diện cho một tin nhắn duy nhất được gửi trong một cuộc hội thoại.
    /// Bảng này có tác dụng lưu trữ nội dung, người gửi, thời gian gửi,
    /// và liên kết chặt chẽ với cuộc hội thoại mà nó thuộc về.
    /// </summary>
    public class Messager
    {
        [Key]
        public int MessageId { get; set; }

        [Required]
        public string Content { get; set; }

        public MessageType MessageType { get; set; } = MessageType.Text;

        public DateTime SentAt { get; set; } = DateTime.Now;

        // --- Foreign Keys ---
        public int ConversationId { get; set; }
        public int SenderId { get; set; }
        public bool isRead { get; set; } = false;

        // --- Navigation Properties ---
        [ForeignKey("ConversationId")]
        public virtual Conversation Conversation { get; set; }

        [ForeignKey("SenderId")]
        public virtual User Sender { get; set; }

        public virtual ICollection<MessageReadStatus> ReadByUsers { get; set; } = new List<MessageReadStatus>();
    }
}
