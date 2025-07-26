using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Models.Kyc
{
    public class FundraiserKyc
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string AccountType { get; set; } // "individual" hoặc "company"

        // Cá nhân
        public IndividualKycInfo IndividualInfo { get; set; }

        // Công ty
        public CompanyKycInfo CompanyInfo { get; set; }

        // Thông tin bổ sung
        [Required, EmailAddress]
        public string ContactEmail { get; set; }

        public string WebsiteOrLinkedIn { get; set; }

        [Required]
        public bool AcceptedTerms { get; set; }

        public bool? IsApproved { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("User")]
        public int UserId { get; set; }
        public User User { get; set; }
    }
}
