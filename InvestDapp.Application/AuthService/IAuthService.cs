using InvestDapp.Shared.Models;

namespace InvestDapp.Application.AuthService
{
    public interface IAuthService
    {
        Task<bool> GenerateAndStoreNonceAsync(string walletAddress);
        Task<bool> VerifySignatureAsync(string walletAddress, string signature, string nonce);
        Task SignInUser(User user);

        // NEW: User wallet authentication with signature
        Task<UserNonceResult> GenerateUserNonceAsync(string walletAddress);
        Task<UserSignInResult> VerifyUserSignatureAsync(string walletAddress, string signature);
    }

    public record UserNonceResult(bool Success, string? Nonce, string? Error);
    public record UserSignInResult(bool Success, string? ErrorMessage);
}
