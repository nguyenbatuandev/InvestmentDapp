using System.ComponentModel.DataAnnotations;

namespace InvestDapp.Shared.Common.Request
{
    public class KycAdminFilterRequest
    {
        private const int DefaultPageSize = 10;

        public string? Status { get; set; }

        public string? AccountType { get; set; }

        public string? Search { get; set; }

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 200)]
        public int PageSize { get; set; } = DefaultPageSize;
    }
}
