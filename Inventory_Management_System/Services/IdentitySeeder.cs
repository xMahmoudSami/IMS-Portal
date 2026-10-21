using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace Inventory_Management_System.Services
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration)
        {
            // ── 1. Seed Enterprise Roles ───────────────────────────────────
            foreach (var roleName in AppRoles.AllRoles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // ── 2. Seed Role Claims / Permissions ─────────────────────────
            await SeedRoleClaimsAsync(roleManager);

            // ── 3. Seed Default SuperAdmin Account ────────────────────────
            var adminUsername = configuration["DefaultAdmin:Username"] ?? "admin";
            var adminEmail = configuration["DefaultAdmin:Email"] ?? "admin@ims.local";
            var adminFullName = configuration["DefaultAdmin:FullName"] ?? "System Administrator";
            var adminPassword = configuration["DefaultAdmin:Password"] ?? "Admin@12345";
            var defaultAdminRecoveryKey = configuration["DefaultAdmin:RecoveryKey"] ?? "IMS-ADMIN-RECOVERY-KEY-2026";

            var existingAdmin = await userManager.FindByNameAsync(adminUsername);
            if (existingAdmin == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = adminUsername,
                    Email = adminEmail,
                    FullName = adminFullName,
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    RecoveryKeyGeneratedAt = DateTime.UtcNow
                };

                // Hash the Master Recovery Key
                var hasher = new PasswordHasher<ApplicationUser>();
                adminUser.RecoveryKeyHash = hasher.HashPassword(adminUser, NormalizeRecoveryKey(defaultAdminRecoveryKey));

                var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, AppRoles.SuperAdmin);
                }
            }
            else
            {
                // Ensure existing admin is in SuperAdmin role
                if (!await userManager.IsInRoleAsync(existingAdmin, AppRoles.SuperAdmin))
                {
                    await userManager.AddToRoleAsync(existingAdmin, AppRoles.SuperAdmin);
                }

                bool adminNeedsUpdate = false;
                var hasher = new PasswordHasher<ApplicationUser>();

                // If Master Recovery Key is missing, set default
                if (string.IsNullOrEmpty(existingAdmin.RecoveryKeyHash))
                {
                    existingAdmin.RecoveryKeyHash = hasher.HashPassword(existingAdmin, NormalizeRecoveryKey(defaultAdminRecoveryKey));
                    existingAdmin.RecoveryKeyGeneratedAt = DateTime.UtcNow;
                    adminNeedsUpdate = true;
                }

                if (adminNeedsUpdate)
                {
                    await userManager.UpdateAsync(existingAdmin);
                }
            }

            // ── 4. Seed Standard Role Sample Accounts For Demonstration / Offline Testing ──
            var sampleUsers = new (string Username, string Email, string FullName, string Password, string Role, string RecoveryKey)[]
            {
                ("warehouse", "warehouse@ims.local", "Warehouse Lead", "Warehouse@12345", AppRoles.WarehouseManager, "IMS-WAREHOUSE-KEY-2026"),
                ("cashier", "cashier@ims.local", "Front Cashier", "Cashier@12345", AppRoles.Cashier, "IMS-CASHIER-KEY-2026"),
                ("purchaser", "purchaser@ims.local", "Procurement Officer", "Purchaser@12345", AppRoles.ProcurementSpecialist, "IMS-PURCHASER-KEY-2026"),
                ("auditor", "auditor@ims.local", "Compliance Auditor", "Auditor@12345", AppRoles.Auditor, "IMS-AUDITOR-KEY-2026")
            };

            var sampleHasher = new PasswordHasher<ApplicationUser>();
            foreach (var (username, email, fullName, password, role, sampleKey) in sampleUsers)
            {
                var user = await userManager.FindByNameAsync(username);
                if (user == null)
                {
                    var newUser = new ApplicationUser
                    {
                        UserName = username,
                        Email = email,
                        FullName = fullName,
                        EmailConfirmed = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        RecoveryKeyGeneratedAt = DateTime.UtcNow
                    };
                    newUser.RecoveryKeyHash = sampleHasher.HashPassword(newUser, NormalizeRecoveryKey(sampleKey));
                    var result = await userManager.CreateAsync(newUser, password);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(newUser, role);
                    }
                }
                else if (string.IsNullOrEmpty(user.RecoveryKeyHash))
                {
                    user.RecoveryKeyHash = sampleHasher.HashPassword(user, NormalizeRecoveryKey(sampleKey));
                    user.RecoveryKeyGeneratedAt = DateTime.UtcNow;
                    await userManager.UpdateAsync(user);
                }
            }
        }

        private static async Task SeedRoleClaimsAsync(RoleManager<IdentityRole> roleManager)
        {
            var rolePermissions = new Dictionary<string, List<string>>
            {
                // 1. SuperAdmin (Unrestricted)
                [AppRoles.SuperAdmin] = new List<string>
                {
                    AppPermissions.ProductsView, AppPermissions.ProductsCreate, AppPermissions.ProductsEdit, AppPermissions.ProductsDelete,
                    AppPermissions.CategoriesView, AppPermissions.CategoriesCreate, AppPermissions.CategoriesEdit, AppPermissions.CategoriesDelete,
                    AppPermissions.SuppliersView, AppPermissions.SuppliersCreate, AppPermissions.SuppliersEdit, AppPermissions.SuppliersDelete,
                    AppPermissions.SupplierProductsView, AppPermissions.SupplierProductsManage,
                    AppPermissions.PurchasesView, AppPermissions.PurchasesCreate, AppPermissions.PurchasesDelete,
                    AppPermissions.SalesView, AppPermissions.SalesCreate, AppPermissions.SalesDelete,
                    AppPermissions.DashboardView, AppPermissions.ReportsStockHealth, AppPermissions.ReportsSales, AppPermissions.ReportsExport,
                    AppPermissions.UsersView, AppPermissions.UsersManage
                },

                // 2. WarehouseManager (Inventory lead: Products, Categories, Suppliers, SupplierProducts, Purchases. Strictly blocked from Sales and Users)
                [AppRoles.WarehouseManager] = new List<string>
                {
                    AppPermissions.ProductsView, AppPermissions.ProductsCreate, AppPermissions.ProductsEdit, AppPermissions.ProductsDelete,
                    AppPermissions.CategoriesView, AppPermissions.CategoriesCreate, AppPermissions.CategoriesEdit, AppPermissions.CategoriesDelete,
                    AppPermissions.SuppliersView, AppPermissions.SuppliersCreate, AppPermissions.SuppliersEdit, AppPermissions.SuppliersDelete,
                    AppPermissions.SupplierProductsView, AppPermissions.SupplierProductsManage,
                    AppPermissions.PurchasesView, AppPermissions.PurchasesCreate, AppPermissions.PurchasesDelete,
                    AppPermissions.DashboardView, AppPermissions.ReportsStockHealth, AppPermissions.ReportsExport
                },

                // 3. Cashier (POS checkout & basic product lookup. Blocked from supplier prices, purchases, reports, catalog edits)
                [AppRoles.Cashier] = new List<string>
                {
                    AppPermissions.ProductsView,
                    AppPermissions.SalesView, AppPermissions.SalesCreate
                },

                // 4. ProcurementSpecialist (Suppliers, SupplierProducts, Purchases, Product view. Blocked from sales & users)
                [AppRoles.ProcurementSpecialist] = new List<string>
                {
                    AppPermissions.ProductsView,
                    AppPermissions.SuppliersView, AppPermissions.SuppliersCreate, AppPermissions.SuppliersEdit, AppPermissions.SuppliersDelete,
                    AppPermissions.SupplierProductsView, AppPermissions.SupplierProductsManage,
                    AppPermissions.PurchasesView, AppPermissions.PurchasesCreate, AppPermissions.PurchasesDelete,
                    AppPermissions.DashboardView
                },

                // 5. Auditor (Strictly Read-Only access across Dashboard, Reports, History. Blocked from all mutations)
                [AppRoles.Auditor] = new List<string>
                {
                    AppPermissions.DashboardView,
                    AppPermissions.ReportsStockHealth, AppPermissions.ReportsSales, AppPermissions.ReportsExport,
                    AppPermissions.ProductsView,
                    AppPermissions.CategoriesView,
                    AppPermissions.SuppliersView,
                    AppPermissions.PurchasesView,
                    AppPermissions.SalesView
                }
            };

            foreach (var (roleName, permissions) in rolePermissions)
            {
                var role = await roleManager.FindByNameAsync(roleName);
                if (role == null) continue;

                var existingClaims = await roleManager.GetClaimsAsync(role);
                var existingValues = existingClaims
                    .Where(c => c.Type == AppPermissions.ClaimType)
                    .Select(c => c.Value)
                    .ToHashSet();

                foreach (var permission in permissions)
                {
                    if (!existingValues.Contains(permission))
                    {
                        await roleManager.AddClaimAsync(role, new Claim(AppPermissions.ClaimType, permission));
                    }
                }
            }
        }

        private static string NormalizeRecoveryKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            return key.Replace("IMS-", "", StringComparison.OrdinalIgnoreCase)
                      .Replace("-", "")
                      .Replace(" ", "")
                      .Trim()
                      .ToUpperInvariant();
        }
    }
}
