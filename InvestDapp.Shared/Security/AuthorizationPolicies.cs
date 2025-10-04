namespace InvestDapp.Shared.Security;

public static class AuthorizationPolicies
{
    public const string RequireSuperAdmin = "RequireSuperAdmin";
    public const string RequireAdmin = "RequireAdmin";
    public const string RequireModerator = "RequireModerator";
    public const string RequireSupportAgent = "RequireSupportAgent";
    public const string RequireFundraiser = "RequireFundraiser";
    public const string RequireStaffAccess = "RequireStaffAccess";

    public const string AdminSessionClaim = "AdminSession";
    public const string AdminSessionVerified = "true";
}
