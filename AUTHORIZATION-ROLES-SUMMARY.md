# Authorization & Role-Based Access Control Summary

## Fixed Issues
1. ✅ Off-chain roles not loading from database during admin login
2. ✅ `SupportAgent`, `Moderator`, `Fundraiser` roles not allowed to access admin panel
3. ✅ Authorization policies too restrictive for staff roles

## Role Definitions & Permissions

### 🔴 SuperAdmin (Highest Level)
**Access:** Full system control
- ✅ Dashboard
- ✅ Support/Tickets (view all)
- ✅ KYC Management (view + approve/reject)
- ✅ Campaign Management (view + approve/reject)
- ✅ Transactions/Reports
- ✅ Admin Roles Management (grant/revoke roles)

### 🟠 Admin
**Access:** System administration
- ✅ Dashboard
- ✅ Support/Tickets (view all)
- ✅ KYC Management (view + approve/reject)
- ✅ Campaign Management (view + approve/reject)
- ✅ Transactions/Reports
- ❌ Admin Roles Management (only SuperAdmin)

### 🟡 Moderator
**Access:** Content moderation
- ✅ Dashboard
- ✅ Support/Tickets (view all)
- ✅ KYC Management (view only, cannot approve/reject)
- ✅ Campaign Management (view + edit, cannot approve/reject)
- ✅ Transactions/Reports (view only)
- ❌ Admin Roles Management

### 🟢 SupportAgent
**Access:** Customer support
- ✅ Dashboard
- ✅ Support/Tickets (full access to handle tickets)
- ❌ KYC Management
- ❌ Campaign Management
- ❌ Transactions/Reports
- ❌ Admin Roles Management

### 🔵 Fundraiser
**Access:** Campaign management
- ✅ Dashboard
- ❌ Support/Tickets (unless specifically granted)
- ❌ KYC Management
- ✅ Campaign Management (view + edit own campaigns, cannot approve)
- ❌ Transactions/Reports
- ❌ Admin Roles Management

## Controller Authorization Policies

| Controller | Class-Level Policy | Method-Level Restrictions |
|------------|-------------------|---------------------------|
| `DashboardController` | `RequireStaffAccess` | None |
| `SupportController` | `RequireSupportAgent` | None |
| `KycManagementController` | `RequireModerator` | Approve/Reject: `RequireAdmin` |
| `Manage_CampaignsController` | `RequireModerator` | Approve/Reject: `RequireAdmin` |
| `TransactionsController` | `RequireModerator` | None |
| `TransactionsApiController` | `RequireModerator` | None |
| `AdminRolesController` | `RequireSuperAdmin` | None |

## Policy Definitions (Program.cs)

```csharp
RequireSuperAdmin:
  - SuperAdmin only

RequireAdmin:
  - Admin, SuperAdmin

RequireModerator:
  - Moderator, Admin, SuperAdmin

RequireSupportAgent:
  - SupportAgent, Admin, SuperAdmin

RequireFundraiser:
  - Fundraiser, Admin, SuperAdmin

RequireStaffAccess:
  - SuperAdmin, Admin, Moderator, SupportAgent, Fundraiser
  - Used for Dashboard and general staff areas
```

## Key Fixes Applied

### 1. AdminLoginService.cs
**Problem:** `GetOffchainRolesAsync()` used `EF.Functions.Like()` with `ToLower()` which EF couldn't translate properly
**Fix:** Load all users with roles from DB, filter in C# memory using `StringComparison.OrdinalIgnoreCase`

```csharp
var allUsersWithRoles = await _dbContext.Users
    .AsNoTracking()
    .Where(u => u.Role != null && u.Role != "")
    .ToListAsync();

var user = allUsersWithRoles
    .FirstOrDefault(u => string.Equals(u.WalletAddress, normalizedWallet, StringComparison.OrdinalIgnoreCase));
```

### 2. AdminRoles Array
**Problem:** Only `SuperAdmin` and `Admin` allowed to login
**Fix:** Added `Moderator`, `SupportAgent`, `Fundraiser`

```csharp
private static readonly RoleType[] AdminRoles = { 
    RoleType.SuperAdmin, 
    RoleType.Admin, 
    RoleType.Moderator, 
    RoleType.SupportAgent, 
    RoleType.Fundraiser 
};
```

### 3. Authorization Policies
**Problem:** Controllers used `RequireAdmin` everywhere, blocking staff roles
**Fix:** 
- Created `RequireStaffAccess` for general areas (Dashboard)
- Used `RequireModerator` for view-only areas
- Used `RequireSupportAgent` for Support
- Used `RequireAdmin` for sensitive operations (Approve/Reject)

### 4. Method-Level Authorization
**Problem:** Moderators could approve KYC/Campaigns despite only having view access
**Fix:** Added `[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]` on Approve/Reject actions

## Testing Checklist

### SupportAgent Role Testing
- [x] Can login with MetaMask
- [x] Can access Dashboard
- [x] Can access Support/Tickets
- [ ] Cannot access KYC Management (should redirect to 403)
- [ ] Cannot access Campaign Management (should redirect to 403)
- [ ] Cannot access Transactions (should redirect to 403)
- [ ] Cannot access Admin Roles (should redirect to 403)

### Moderator Role Testing
- [ ] Can login with MetaMask
- [ ] Can access Dashboard
- [ ] Can access Support/Tickets
- [ ] Can view KYC Management
- [ ] Cannot approve/reject KYC (button should be hidden or return 403)
- [ ] Can view/edit Campaign Management
- [ ] Cannot approve/reject Campaigns (button should be hidden or return 403)
- [ ] Can view Transactions/Reports

## Database Schema
```sql
-- Users table
WalletAddress VARCHAR(42) PRIMARY KEY (stored as lowercase)
Role VARCHAR(50) -- Values: 'SuperAdmin', 'Admin', 'Moderator', 'SupportAgent', 'Fundraiser', 'User'
Email VARCHAR(255)
Name NVARCHAR(255)
CreatedAt DATETIME2
UpdatedAt DATETIME2
```

## Next Steps
1. Test all roles with different wallets
2. Update UI to hide buttons based on roles
3. Remove `[AllowAnonymous]` and `[IgnoreAntiforgeryToken]` from AdminRolesController after testing
4. Add role-based menu visibility in admin layout
5. Consider adding audit logging for sensitive operations
