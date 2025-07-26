using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Common.Respone
{
    public class CampaignBasicResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? ShortDescription { get; set; } // Mô tả ngắn gọn
        public string? Description { get; set; } // Mô tả chi tiết
        public string? ImageUrl { get; set; }
    }
}
