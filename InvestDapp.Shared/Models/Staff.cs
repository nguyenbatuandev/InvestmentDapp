using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InvestDapp.Shared.Models
{
    /// <summary>
    /// Represents a staff member in the admin system
    /// </summary>
    [Table("Staff")]
    public class Staff
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Wallet address of the staff member (42 chars, lowercase)
        /// </summary>
        [Required]
        [MaxLength(42)]
        [Column(TypeName = "varchar(42)")]
        public string WalletAddress { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the staff member
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Email of the staff member
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Optional avatar URL
        /// </summary>
        [MaxLength(500)]
        public string? Avatar { get; set; }

        /// <summary>
        /// Whether the staff account is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// When the staff member was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the staff member was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Soft delete timestamp
        /// </summary>
        public DateTime? DeletedAt { get; set; }

        /// <summary>
        /// Navigation property: All roles assigned to this staff member
        /// </summary>
        public virtual ICollection<StaffRole> StaffRoles { get; set; } = new List<StaffRole>();
    }
}
