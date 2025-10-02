using System;
using System.Linq;
using InvestDapp.Application.NotificationService;
using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Shared.Common;
using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.DTOs.Admin;
using InvestDapp.Shared.Models.Kyc;


namespace InvestDapp.Application.KycService
{
    public class KycService : IKycService
    {
        private readonly IKyc _kycRepository;
        private readonly IUser _userRepository;
        private readonly INotificationService _notificationService;
        public KycService(IKyc kycRepository, IUser userRepository, INotificationService notificationService)
        {
            _kycRepository = kycRepository;
            _userRepository = userRepository;
            _notificationService = notificationService;
        }

        public async Task<BaseResponse<FundraiserKyc>> CheckKycAsync(string walletAddress)
        {
            var latestFundraiserKyc = await _kycRepository.GetLatestFundraiserKycByWalletAsync(walletAddress); 
            if (latestFundraiserKyc != null)
            {
                if (latestFundraiserKyc.IsApproved == null)
                {
                    return new BaseResponse<FundraiserKyc>
                    {
                        Success = true,
                        Message = "1",
                        Data = null,
                        Errors = null
                    };
                }

                if (latestFundraiserKyc.IsApproved == true)
                {
                    return new BaseResponse<FundraiserKyc>
                    {
                        Success = true,
                        Message = "2",
                        Data = null
                    };
                }

                if (latestFundraiserKyc.IsApproved == false)
                {
                    return new BaseResponse<FundraiserKyc>
                    {
                        Success = true,
                        Message = "3",
                        Data = null
                    };
                }
            }
            return new BaseResponse<FundraiserKyc>
            {
                Success = true,
                Data = null,
                Message = "0",
                
            };
        }

        public async Task<BaseResponse<FundraiserKyc>> SubmitKycAsync(FundraiserKycRequest kycRequest, string walletAddress)
        {
            // 1. Tìm kiếm người dùng bằng địa chỉ ví

            var latestFundraiserKyc =await _kycRepository.GetLatestFundraiserKycByWalletAsync(walletAddress);

            var user = await _userRepository.GetUserByWalletAddressAsync(walletAddress);

            if (latestFundraiserKyc != null)
            {
                if (latestFundraiserKyc.IsApproved == null)
                {
                    return new BaseResponse<FundraiserKyc>
                    {
                        Success = false,
                        Message = "Bạn đã gửi yêu cầu KYC trước đó và đang chờ duyệt.",
                        Data = null
                    };
                }

                if (latestFundraiserKyc.IsApproved == true)
                {
                    return new BaseResponse<FundraiserKyc>
                    {
                        Success = false,
                        Message = "Bạn đã được phê duyệt KYC trước đó.",
                        Data = null
                    };
                }
            }

            if (user == null)
            {
                return new BaseResponse<FundraiserKyc>
                {
                    Success = false,
                    Message = "Không tìm thấy người dùng với địa chỉ ví này.",
                    Data = null
                };
            }

            await _kycRepository.SubmitKycAsync(kycRequest, user.ID);
            return new BaseResponse<FundraiserKyc>
            {
                Success = true,
                Message = "Yêu cầu KYC đã được gửi thành công.",
                Data = null
            };
        }

        public async Task<BaseResponse<PagedResult<AdminKycItemDto>>> QueryKycsAsync(KycAdminFilterRequest filterRequest)
        {
            filterRequest ??= new KycAdminFilterRequest();

            var page = filterRequest.Page < 1 ? 1 : filterRequest.Page;
            var pageSize = filterRequest.PageSize <= 0 ? 10 : Math.Min(filterRequest.PageSize, 200);

            var (items, totalCount) = await _kycRepository.QueryKycsAsync(filterRequest.Status, filterRequest.AccountType, filterRequest.Search, page, pageSize);
            var mapped = items.Select(MapToAdminDto).ToList();

            var pagedResult = new PagedResult<AdminKycItemDto>
            {
                Items = mapped,
                Total = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return new BaseResponse<PagedResult<AdminKycItemDto>>
            {
                Success = true,
                Message = "Tải danh sách KYC thành công.",
                Data = pagedResult
            };
        }

        public async Task<BaseResponse<bool>> ApproveKycAsync(int kycId)
        {
            var kyc = await _kycRepository.GetKycByIdAsync(kycId);
            if (kyc == null)
            {
                return new BaseResponse<bool>
                {
                    Success = false,
                    Message = "Không tìm thấy hồ sơ KYC.",
                    Data = false
                };
            }

            if (kyc.IsApproved == true)
            {
                return new BaseResponse<bool>
                {
                    Success = true,
                    Message = "Hồ sơ KYC đã được phê duyệt trước đó.",
                    Data = true
                };
            }

            var updated = await _kycRepository.UpdateKycStatusAsync(kycId, true);
            if (!updated)
            {
                return new BaseResponse<bool>
                {
                    Success = false,
                    Message = "Không thể cập nhật trạng thái KYC.",
                    Data = false
                };
            }

            return new BaseResponse<bool>
            {
                Success = true,
                Message = "Đã phê duyệt hồ sơ KYC.",
                Data = true
            };
        }

        public async Task<BaseResponse<bool>> RejectKycAsync(int kycId)
        {
            var kyc = await _kycRepository.GetKycByIdAsync(kycId);
            if (kyc == null)
            {
                return new BaseResponse<bool>
                {
                    Success = false,
                    Message = "Không tìm thấy hồ sơ KYC.",
                    Data = false
                };
            }

            if (kyc.IsApproved == false)
            {
                return new BaseResponse<bool>
                {
                    Success = true,
                    Message = "Hồ sơ KYC đã ở trạng thái từ chối.",
                    Data = true
                };
            }

            var updated = await _kycRepository.UpdateKycStatusAsync(kycId, false);
            if (!updated)
            {
                return new BaseResponse<bool>
                {
                    Success = false,
                    Message = "Không thể cập nhật trạng thái KYC.",
                    Data = false
                };
            }

            return new BaseResponse<bool>
            {
                Success = true,
                Message = "Đã từ chối hồ sơ KYC.",
                Data = true
            };
        }

        public async Task<BaseResponse<bool>> RevokeKycAsync(int kycId)
        {
            var kyc = await _kycRepository.GetKycByIdAsync(kycId);
            if (kyc == null)
            {
                return new BaseResponse<bool>
                {
                    Success = false,
                    Message = "Không tìm thấy hồ sơ KYC.",
                    Data = false
                };
            }

            if (kyc.IsApproved != true)
            {
                return new BaseResponse<bool>
                {
                    Success = false,
                    Message = "Chỉ có thể hủy tư cách KYC đối với hồ sơ đã được phê duyệt.",
                    Data = false
                };
            }

            var updated = await _kycRepository.UpdateKycStatusAsync(kycId, false);
            if (!updated)
            {
                return new BaseResponse<bool>
                {
                    Success = false,
                    Message = "Không thể cập nhật trạng thái KYC.",
                    Data = false
                };
            }

            return new BaseResponse<bool>
            {
                Success = true,
                Message = "Đã hủy tư cách KYC của người dùng.",
                Data = true
            };
        }

        private static AdminKycItemDto MapToAdminDto(FundraiserKyc kyc)
        {
            return new AdminKycItemDto
            {
                Id = kyc.Id,
                AccountType = kyc.AccountType,
                Status = MapStatus(kyc.IsApproved),
                SubmittedAt = kyc.SubmittedAt,
                ContactEmail = kyc.ContactEmail,
                WebsiteOrLinkedIn = kyc.WebsiteOrLinkedIn,
                User = kyc.User == null ? null : new AdminKycUserDto
                {
                    Id = kyc.User.ID,
                    Name = kyc.User.Name,
                    Email = kyc.User.Email,
                    WalletAddress = kyc.User.WalletAddress,
                    Avatar = kyc.User.Avatar
                },
                Individual = kyc.IndividualInfo == null ? null : new AdminKycIndividualDto
                {
                    FullName = kyc.IndividualInfo.FullName,
                    IdNumber = kyc.IndividualInfo.IdNumber,
                    Nationality = kyc.IndividualInfo.Nationality,
                    IdFrontImagePath = kyc.IndividualInfo.IdFrontImagePath,
                    SelfieWithIdPath = kyc.IndividualInfo.SelfieWithIdPath
                },
                Company = kyc.CompanyInfo == null ? null : new AdminKycCompanyDto
                {
                    CompanyName = kyc.CompanyInfo.CompanyName,
                    RegistrationNumber = kyc.CompanyInfo.RegistrationNumber,
                    RegisteredCountry = kyc.CompanyInfo.RegisteredCountry,
                    BusinessLicensePdfPath = kyc.CompanyInfo.BusinessLicensePdfPath,
                    DirectorIdImagePath = kyc.CompanyInfo.DirectorIdImagePath
                }
            };
        }

        private static string MapStatus(bool? status) => status switch
        {
            true => "approved",
            false => "rejected",
            _ => "pending"
        };
    }
}
