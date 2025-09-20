using System;

namespace InvestDapp.Shared.Common.Request
{
    public class CreateNotificationRequest
    {
        public int UserId { get; set; }
        public string? Type { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }
        public string? Data { get; set; }
    }

    public class CreateNotificationToCampaignRequest
    {
        public int CampaignId { get; set; }
        public string? Type { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }
        public string? Data { get; set; }
    }
}
