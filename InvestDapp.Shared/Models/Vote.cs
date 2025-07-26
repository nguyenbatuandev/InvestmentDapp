using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Models
{
    public class Vote
    {
        [Key]
        public int Id { get; set; } // ID tự tăng
        public string TransactionHash { get; set; } // Hash giao dịch trên blockchain

        // --- Foreign Key đến WithdrawalRequest ---
        public int WithdrawalRequestId { get; set; }
        public virtual WithdrawalRequest WithdrawalRequest { get; set; }

        // --- Chi tiết lá phiếu ---
        [Required]
        [StringLength(42)]
        public string VoterAddress { get; set; }

        public bool Agreed { get; set; } // true = đồng ý, false = không đồng ý

        [Required]
        public double VoteWeight { get; set; } // Số tiền nhà đầu tư đã góp

        public DateTime CreatedAt { get; set; } // Ngày tạo lá phiếu
    }
}
