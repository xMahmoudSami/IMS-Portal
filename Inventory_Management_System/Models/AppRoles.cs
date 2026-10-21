namespace Inventory_Management_System.Models
{
    public static class AppRoles
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string WarehouseManager = "WarehouseManager";
        public const string Cashier = "Cashier";
        public const string ProcurementSpecialist = "ProcurementSpecialist";
        public const string Auditor = "Auditor";

        public static readonly IReadOnlyList<string> AllRoles = new[]
        {
            SuperAdmin,
            WarehouseManager,
            Cashier,
            ProcurementSpecialist,
            Auditor
        };
    }

    public static class AppPermissions
    {
        public const string ClaimType = "Permission";

        // Products
        public const string ProductsView = "Permissions.Products.View";
        public const string ProductsCreate = "Permissions.Products.Create";
        public const string ProductsEdit = "Permissions.Products.Edit";
        public const string ProductsDelete = "Permissions.Products.Delete";

        // Categories
        public const string CategoriesView = "Permissions.Categories.View";
        public const string CategoriesCreate = "Permissions.Categories.Create";
        public const string CategoriesEdit = "Permissions.Categories.Edit";
        public const string CategoriesDelete = "Permissions.Categories.Delete";

        // Suppliers & Mappings
        public const string SuppliersView = "Permissions.Suppliers.View";
        public const string SuppliersCreate = "Permissions.Suppliers.Create";
        public const string SuppliersEdit = "Permissions.Suppliers.Edit";
        public const string SuppliersDelete = "Permissions.Suppliers.Delete";
        public const string SupplierProductsView = "Permissions.SupplierProducts.View";
        public const string SupplierProductsManage = "Permissions.SupplierProducts.Manage";

        // Purchases
        public const string PurchasesView = "Permissions.Purchases.View";
        public const string PurchasesCreate = "Permissions.Purchases.Create";
        public const string PurchasesDelete = "Permissions.Purchases.Delete";

        // Sales
        public const string SalesView = "Permissions.Sales.View";
        public const string SalesCreate = "Permissions.Sales.Create";
        public const string SalesDelete = "Permissions.Sales.Delete";

        // Reports & Dashboard
        public const string DashboardView = "Permissions.Dashboard.View";
        public const string ReportsStockHealth = "Permissions.Reports.StockHealth";
        public const string ReportsSales = "Permissions.Reports.Sales";
        public const string ReportsExport = "Permissions.Reports.Export";

        // Users & Settings
        public const string UsersView = "Permissions.Users.View";
        public const string UsersManage = "Permissions.Users.Manage";
    }
}
