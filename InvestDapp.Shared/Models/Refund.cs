using InvestDapp.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Models
{
    public class Refund
    {
        [Key]
        [ForeignKey("Campaign")]
        public int Id { get; set; }
        public string TransactionHash { get; set; }
        public double Amount { get; set; }
        public DateTime CreatedAt { get; set; }
        public virtual Campaign Campaign { get; set; }

    }
}
