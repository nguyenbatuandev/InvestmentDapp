using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InvestDapp.Shared.Models.Kyc
{
    public class IndividualKycInfo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; }

        public string IdNumber { get; set; }

        public string Nationality { get; set; }

        public string IdFrontImagePath { get; set; }

        public string SelfieWithIdPath { get; set; }

        [ForeignKey("FundraiserKyc")]
        public int FundraiserKycId { get; set; }
        public FundraiserKyc FundraiserKyc { get; set; }
    }

}
