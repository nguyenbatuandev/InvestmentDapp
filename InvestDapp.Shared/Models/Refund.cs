using InvestDapp.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace InvestDapp.Shared.Models
{
    public class Refund
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        [Required]
        public int CampaignId { get; set; }
        
        [Required]
        public string InvestorAddress { get; set; } = string.Empty;
        
        [Required]
        public string AmountInWei { get; set; } = string.Empty;
        
        public DateTime? ClaimedAt { get; set; }
        
        public string? TransactionHash { get; set; }
        
        public string? RefundReason { get; set; }
        
        [ForeignKey("CampaignId")]
        public virtual Campaign? Campaign { get; set; }
    }
}
