using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Models
{
    public class Notification
    {
        [Key]
        public uint ID { get; set; }

        // Foreign key
        [ForeignKey("User")]
        public int UserID { get; set; }

        // Navigation property
        public User User { get; set; }

        [MaxLength(50)]
        public string Type { get; set; }  // e.g. donation, withdrawal, etc.

        [MaxLength(255)]
        public string Title { get; set; }

        public string Message { get; set; }

        // JSON data — lưu kiểu string
        public string Data { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
