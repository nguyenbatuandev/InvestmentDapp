using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using InvestDapp.Shared.Enums;

namespace InvestDapp.Shared.Models
{
    /// <summary>
    /// Junction table for many-to-many relationship between Staff and Roles
    /// </summary>
    [Table("StaffRoles")]
    public class StaffRole
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to Staff
        /// </summary>
        [Required]
        public int StaffId { get; set; }

        /// <summary>
        /// The role assigned (stored as enum)
        /// </summary>
        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public RoleType Role { get; set; }

        /// <summary>
        /// When this role was granted
        /// </summary>
        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Who granted this role (wallet address of admin)
        /// </summary>
        [MaxLength(42)]
        [Column(TypeName = "varchar(42)")]
        public string? GrantedBy { get; set; }

        /// <summary>
        /// Navigation property to Staff
        /// </summary>
        [ForeignKey(nameof(StaffId))]
        public virtual Staff Staff { get; set; } = null!;
    }
}
