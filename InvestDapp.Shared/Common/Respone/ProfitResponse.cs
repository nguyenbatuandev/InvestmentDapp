using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Common.Respone
{
    public class ProfitResponse
    {
        public int Id { get; set; }
        public double Amount { get; set; }
        public string Address { get; set; }
        public DateTime CreatedAt { get; set; }
        public string TransactionHash { get; set; }
    }

}
