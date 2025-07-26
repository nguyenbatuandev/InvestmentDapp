using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Models.Kyc
{
    public class CompanyKycInfo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string CompanyName { get; set; }

        public string RegistrationNumber { get; set; }

        public string RegisteredCountry { get; set; }

        public string BusinessLicensePdfPath { get; set; }

        public string DirectorIdImagePath { get; set; }

        [ForeignKey("FundraiserKyc")]
        public int FundraiserKycId { get; set; }
        public FundraiserKyc FundraiserKyc { get; set; }
    }

}
