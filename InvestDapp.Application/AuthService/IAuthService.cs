using InvestDapp.Shared.Models;

namespace InvestDapp.Application.AuthService
{
    public interface IAuthService
    {
        Task<bool> GenerateAndStoreNonceAsync(string walletAddress);
        Task<bool> VerifySignatureAsync(string walletAddress, string signature, string nonce);
        Task SignInUser(User user);

    }
}
