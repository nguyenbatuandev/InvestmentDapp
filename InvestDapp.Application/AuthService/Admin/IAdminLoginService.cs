namespace InvestDapp.Application.AuthService.Admin;

public interface IAdminLoginService
{
    Task<AdminNonceResult> GenerateNonceAsync(string walletAddress);
    Task<AdminSignInResult> SignInWithSignatureAsync(string walletAddress, string signature);
    Task SignOutAsync();
}

public record AdminNonceResult(bool Success, string? Nonce, string? Error);

public record AdminSignInResult(bool Success, string? RedirectUrl, string? ErrorMessage);
