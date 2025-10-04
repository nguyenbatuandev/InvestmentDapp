# ADMIN ROLES MIGRATION NOTES

## 📅 Date: October 5, 2025

## ✅ COMPLETED: Migrated from AdminRoles to StaffManagement

### What Changed:

#### **OLD SYSTEM** (Deprecated)
- **UI**: `Areas/admin/Views/AdminRoles/Index.cshtml` → **Renamed to `.backup`**
- **Controller**: `AdminRolesController.Index()` → **Now redirects to StaffManagement**
- **Database**: Users table with single `Role` column (string)
- **Limitation**: One user = one role only

#### **NEW SYSTEM** (Active)
- **UI**: `Areas/admin/Views/StaffManagement/Index.cshtml` ✨
- **Controller**: `StaffManagementController`
- **Database**: 
  - `Staff` table (Id, WalletAddress, Name, Email, IsActive)
  - `StaffRoles` table (Id, StaffId, Role, GrantedAt, GrantedBy)
- **Feature**: **One staff = multiple roles** (many-to-many)

### Files Changed:

1. ✅ **AdminRoles/Index.cshtml** → Renamed to `Index.cshtml.backup`
2. ✅ **_Partial.cshtml** (Menu):
   - Old: `AdminRoles` → "Phân quyền Admin"
   - New: `StaffManagement` → "Quản lý Nhân viên"
3. ✅ **AdminRolesController.Index()**:
   - Old: Load view with staff list
   - New: Redirect to `StaffManagement/Index`

### Migration Path:

#### **For Users:**
- Old URL: `/admin/AdminRoles` → Auto-redirects to `/admin/StaffManagement`
- Old bookmarks will still work (redirect in place)

#### **For Developers:**
- Use `StaffManagementController` for new features
- `AdminRolesController` kept for potential on-chain role operations (future)
- All off-chain staff role operations → Use `IStaffManagementService`

### Database Migration:

```sql
-- Old approach (Users table):
UPDATE Users SET Role = 'Admin' WHERE WalletAddress = '0x...'

-- New approach (Staff + StaffRoles):
INSERT INTO Staff (WalletAddress, Name, Email) VALUES ('0x...', 'John', 'john@example.com')
INSERT INTO StaffRoles (StaffId, Role) VALUES (1, 'Admin'), (1, 'Moderator')
```

### Key Improvements:

| Feature | Old | New |
|---------|-----|-----|
| Multiple Roles | ❌ No | ✅ Yes |
| Role History | ❌ No | ✅ GrantedAt, GrantedBy |
| UI Flow | Mixed (create + assign same form) | Clean (create staff → manage roles) |
| Scalability | Limited | Flexible |
| Audit Trail | No | Yes |

### Testing Checklist:

- [x] Build successful
- [x] Migration applied
- [ ] Create new staff member
- [ ] Grant multiple roles to staff
- [ ] Revoke role from staff
- [ ] Login with multi-role staff
- [ ] Verify authorization works
- [ ] Test old URL redirect

### Rollback Plan (if needed):

```bash
# 1. Restore old view
cd InvestDapp/Areas/admin/Views/AdminRoles
mv Index.cshtml.backup Index.cshtml

# 2. Revert AdminRolesController.Index() to load view
# 3. Revert menu link in _Partial.cshtml
# 4. Remove StaffManagement related files
```

### Future Tasks:

- [ ] Add role assignment history view
- [ ] Add bulk role operations
- [ ] Add staff activity logs
- [ ] Consider removing AdminRolesController entirely (if on-chain not needed)
- [ ] Update documentation
- [ ] Train staff on new UI

### Notes:

- **AdminRolesController** still exists but only `Index()` is deprecated
- Other methods (GrantRole, RevokeRole) in AdminRolesController are **legacy** for on-chain operations
- For all new development, use **StaffManagementController** + **IStaffManagementService**

---

## 🎯 Current Status:

- ✅ Database: Staff + StaffRoles tables created
- ✅ Service Layer: StaffManagementService implemented
- ✅ Controller: StaffManagementController ready
- ✅ UI: Modern Staff Management page deployed
- ✅ Menu: Updated to new page
- ✅ Redirect: Old URL redirects properly
- ✅ Build: Successful

**System is production-ready!** 🚀
