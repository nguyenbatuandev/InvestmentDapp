using InvestDapp.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvestDapp.Shared.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        // Navigation property: Một Category có thể có nhiều Campaign
        public virtual ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();
    }
}
