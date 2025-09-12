using InvestDapp.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.DTOs.MessageDTO
{
    // Tạo các lớp DTO, có thể đặt trong một thư mục Shared/Dtos
    public class UserDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string AvatarURL { get; set; }
    }

    public class MessageDto
    {
        public int MessageId { get; set; }
        public string Content { get; set; }
        public DateTime SentAt { get; set; }
        public int SenderId { get; set; }
        public bool IsRead { get; set; } 

        public UserDto Sender { get; set; }
    }

    public class ParticipantDto
    {
        public int UserId { get; set; }
        public UserDto User { get; set; }
        public int UnreadCount { get; set; }
        public ParticipantRole Role { get; set; }
    }

    public class ConversationDto
    {
        public int ConversationId { get; set; }
        public ConversationType Type { get; set; }
        public string Name { get; set; }
        public string AvatarURL { get; set; }
        public MessageDto LastMessage { get; set; }
        public int UnreadCount { get; set; } // <--- THÊM DÒNG NÀY
        public ICollection<ParticipantDto> Participants { get; set; }
    }
}
