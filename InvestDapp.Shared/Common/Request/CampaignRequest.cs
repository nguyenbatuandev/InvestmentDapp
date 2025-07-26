using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Common.Request
{
    public class UpdateCampaignRequest
    {
        public int Id { get; set; } // Khóa chính
        public string? ShortDescription { get; set; } // Mô tả ngắn gọn
        public string? Description { get; set; } // Mô tả chi tiết
        public string? ImageUrl { get; set; }    // URL ảnh bìa
        public int? categoryId { get; set; } // Thêm khóa ngoại categoryId, dấu ? cho phép nó null
    }
}
