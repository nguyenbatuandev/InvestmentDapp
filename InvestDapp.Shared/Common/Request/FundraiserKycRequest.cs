using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InvestDapp.Shared.Common.Request
{
    public class FundraiserKycRequest : IValidatableObject
    {
        [Required(ErrorMessage = "Vui lòng chọn loại tài khoản.")]
        public string AccountType { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email liên hệ.")]
        [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ.")]
        public string ContactEmail { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn phải đồng ý với điều khoản và chính sách.")]
        public bool AcceptedTerms { get; set; }

        public string? WebsiteOrLinkedIn { get; set; }

        // --- Các trường cho Cá nhân (có thể null) ---
        public string? FullName { get; set; }
        public string? IdNumber { get; set; }
        public string? Nationality { get; set; } // THÊM MỚI
        public IFormFile? IdFrontImage { get; set; }
        public IFormFile? SelfieWithIdImage { get; set; } // THÊM MỚI

        // --- Các trường cho Công ty (có thể null) ---
        public string? CompanyName { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? RegisteredCountry { get; set; } // THÊM MỚI
        public IFormFile? BusinessLicensePdf { get; set; }
        public IFormFile? DirectorIdImage { get; set; } // THÊM MỚI

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (AccountType == "individual")
            {
                if (string.IsNullOrWhiteSpace(FullName))
                    yield return new ValidationResult("Vui lòng nhập họ và tên.", new[] { nameof(FullName) });
                if (string.IsNullOrWhiteSpace(IdNumber))
                    yield return new ValidationResult("Vui lòng nhập số CCCD.", new[] { nameof(IdNumber) });
                if (string.IsNullOrWhiteSpace(Nationality)) // THÊM MỚI
                    yield return new ValidationResult("Vui lòng nhập quốc tịch.", new[] { nameof(Nationality) });
                if (IdFrontImage == null)
                    yield return new ValidationResult("Vui lòng tải lên ảnh mặt trước CCCD.", new[] { nameof(IdFrontImage) });
                if (SelfieWithIdImage == null) // THÊM MỚI
                    yield return new ValidationResult("Vui lòng tải lên ảnh selfie cùng CCCD.", new[] { nameof(SelfieWithIdImage) });
            }
            else if (AccountType == "company")
            {
                if (string.IsNullOrWhiteSpace(CompanyName))
                    yield return new ValidationResult("Vui lòng nhập tên công ty.", new[] { nameof(CompanyName) });
                if (string.IsNullOrWhiteSpace(RegistrationNumber))
                    yield return new ValidationResult("Vui lòng nhập mã số đăng ký kinh doanh.", new[] { nameof(RegistrationNumber) });
                if (string.IsNullOrWhiteSpace(RegisteredCountry)) // THÊM MỚI
                    yield return new ValidationResult("Vui lòng nhập quốc gia đăng ký.", new[] { nameof(RegisteredCountry) });
                if (BusinessLicensePdf == null)
                    yield return new ValidationResult("Vui lòng tải lên giấy phép kinh doanh.", new[] { nameof(BusinessLicensePdf) });
                if (DirectorIdImage == null) // THÊM MỚI
                    yield return new ValidationResult("Vui lòng tải lên CCCD/Passport của giám đốc.", new[] { nameof(DirectorIdImage) });
            }
        }
    }
}