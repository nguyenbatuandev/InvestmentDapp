using System;

namespace InvestDapp.Shared.DTOs.Admin
{
    public class AdminKycItemDto
    {
        public int Id { get; set; }
        public string AccountType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public string? ContactEmail { get; set; }
        public string? WebsiteOrLinkedIn { get; set; }
        public AdminKycUserDto? User { get; set; }
        public AdminKycIndividualDto? Individual { get; set; }
        public AdminKycCompanyDto? Company { get; set; }
    }

    public class AdminKycUserDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string WalletAddress { get; set; } = string.Empty;
        public string? Avatar { get; set; }
    }

    public class AdminKycIndividualDto
    {
        public string? FullName { get; set; }
        public string? IdNumber { get; set; }
        public string? Nationality { get; set; }
        public string? IdFrontImagePath { get; set; }
        public string? SelfieWithIdPath { get; set; }
    }

    public class AdminKycCompanyDto
    {
        public string? CompanyName { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? RegisteredCountry { get; set; }
        public string? BusinessLicensePdfPath { get; set; }
        public string? DirectorIdImagePath { get; set; }
    }
}
