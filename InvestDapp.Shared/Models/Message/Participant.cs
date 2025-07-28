using InvestDapp.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Models.Message
{
    /// <summary>
    /// Bảng trung gian (junction table) quan trọng, xác định người dùng nào (`UserId`)
    /// là thành viên của cuộc hội thoại nào (`ConversationId`).
    /// Khóa chính của bảng này là sự kết hợp của UserId và ConversationId.
    /// </summary>
    /// <summary>
    /// Bảng trung gian, xác định người dùng nào thuộc về cuộc hội thoại nào.
    /// </summary>
    public class Participant
    {
        // --- Composite Primary Key (cấu hình trong DbContext) ---
        public int UserId { get; set; }
        public int ConversationId { get; set; }
        public int UnreadCount { get; set; }

        public ParticipantRole Role { get; set; } = ParticipantRole.Member;

        public DateTime JoinedAt { get; set; } = DateTime.Now;

        // --- Navigation Properties ---
        public virtual User User { get; set; }
        public virtual Conversation Conversation { get; set; }
    }
}
