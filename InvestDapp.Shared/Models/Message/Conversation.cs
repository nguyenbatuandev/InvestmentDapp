using InvestDapp.Shared.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace InvestDapp.Shared.Models.Message
{
    public class Conversation
    {
        [Key]
        public int ConversationId { get; set; }

        [Required]
        public ConversationType Type { get; set; }

        [StringLength(100)]
        public string? Name { get; set; }

        [StringLength(255)]
        public string? AvatarURL { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Foreign Key for Last Message
        public int? LastMessageId { get; set; }

        [ForeignKey("LastMessageId")]
        public virtual Messager? LastMessage { get; set; }

        [JsonIgnore]
        public virtual ICollection<Messager> Messages { get; set; } = new List<Messager>();

        [JsonIgnore]
        public virtual ICollection<Participant?> Participants { get; set; } = new List<Participant>();
    }
}
