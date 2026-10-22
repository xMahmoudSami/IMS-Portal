using Microsoft.AspNetCore.Authorization;

namespace Inventory_Management_System.Controllers
{
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.ProcurementSpecialist},{AppRoles.Auditor}")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Dashboard/Analytics
        [HttpGet]
        public async Task<IActionResult> Analytics()
        {
            // Instantiating using the updated ViewModel class name
            var viewModel = new DashboardViewModels();

            // 1. Calculate Summary KPIs
            viewModel.TotalProducts = await _context.Products.CountAsync();
            viewModel.TotalCategories = await _context.Categories.CountAsync();
            viewModel.TotalSuppliers = await _context.Suppliers.CountAsync();
            viewModel.TotalStockQuantity = await _context.Products.SumAsync(p => (int?)p.StockQuantity) ?? 0;

            // Low stock condition: StockQuantity <= LowStockThreshold
            viewModel.LowStockCount = await _context.Products
                .CountAsync(p => p.StockQuantity <= p.LowStockThreshold);

            // Purchases Statistics
            viewModel.TotalPurchasesCount = await _context.Purchases.CountAsync();
            viewModel.TotalPurchasesValue = await _context.Purchases.SumAsync(p => (decimal?)p.TotalAmount) ?? 0m;

            // Sales Statistics
            viewModel.TotalSalesCount = await _context.Sales.CountAsync();
            viewModel.TotalSalesRevenue = await _context.Sales.SumAsync(s => (decimal?)s.TotalAmount) ?? 0m;

            // 2. Low Stock Products Warning List
            viewModel.LowStockProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.StockQuantity <= p.LowStockThreshold)
                .OrderBy(p => p.StockQuantity)
                .Take(10)
                .Select(p => new LowStockProductViewModel
                {
                    ProductID = p.ProductID,
                    SKU = p.SKU,
                    ProductName = p.ProductName,
                    CategoryName = p.Category != null ? p.Category.CategoryName : "N/A",
                    StockQuantity = p.StockQuantity,
                    LowStockThreshold = p.LowStockThreshold
                })
                .ToListAsync();

            // 3. Top Selling Products
            viewModel.TopSellingProducts = await _context.SaleItems
                .GroupBy(si => new { si.ProductID, si.Product.ProductName })
                .Select(g => new TopSellingProductViewModel
                {
                    ProductID = g.Key.ProductID,
                    ProductName = g.Key.ProductName,
                    TotalQuantitySold = g.Sum(x => x.Quantity),
                    TotalRevenue = g.Sum(x => x.Quantity * (decimal)x.UnitPrice)
                })
                .OrderByDescending(x => x.TotalQuantitySold)
                .Take(5)
                .ToListAsync();

            // 4. Combined Recent Activity Feed (Recent Sales + Recent Purchases)
            var recentSales = await _context.Sales
                .OrderByDescending(s => s.SaleDate)
                .Take(5)
                .Select(s => new RecentActivityViewModel
                {
                    ActivityType = "Sale",
                    ReferenceID = s.SaleID,
                    ActivityDate = s.SaleDate,
                    TotalAmount = s.TotalAmount,
                    EntityInfo = s.CustomerInfo ?? "General Customer"
                })
                .ToListAsync();

            var recentPurchases = await _context.Purchases
                .Include(p => p.Supplier)
                .OrderByDescending(p => p.PurchaseDate)
                .Take(5)
                .Select(p => new RecentActivityViewModel
                {
                    ActivityType = "Purchase",
                    ReferenceID = p.PurchaseID,
                    ActivityDate = p.PurchaseDate,
                    TotalAmount = p.TotalAmount,
                    EntityInfo = p.Supplier != null ? p.Supplier.SupplierName : "N/A"
                })
                .ToListAsync();

            viewModel.RecentActivities = recentSales
                .Concat(recentPurchases)
                .OrderByDescending(a => a.ActivityDate)
                .Take(7)
                .ToList();

            return View(viewModel);
        }

        #region Chart JSON API Endpoints

        // GET: /Dashboard/GetSalesVsPurchasesChartData
        [HttpGet]
        public async Task<IActionResult> GetSalesVsPurchasesChartData()
        {
            var currentYear = DateTime.Now.Year;

            // Fetch monthly sales for current year
            var salesData = await _context.Sales
                .Where(s => s.SaleDate.Year == currentYear)
                .GroupBy(s => s.SaleDate.Month)
                .Select(g => new { Month = g.Key, Total = g.Sum(s => s.TotalAmount) })
                .ToListAsync();

            // Fetch monthly purchases for current year
            var purchasesData = await _context.Purchases
                .Where(p => p.PurchaseDate.Year == currentYear)
                .GroupBy(p => p.PurchaseDate.Month)
                .Select(g => new { Month = g.Key, Total = g.Sum(p => p.TotalAmount) })
                .ToListAsync();

            var result = new SalesVsPurchasesChartViewModel();
            var currentCulture = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            string[] monthNames = currentCulture switch
            {
                "ar" => new[] { "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر" },
                "fr" => new[] { "Janv", "Févr", "Mars", "Avr", "Mai", "Juin", "Juil", "Août", "Sept", "Oct", "Nov", "Déc" },
                "es" => new[] { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" },
                "it" => new[] { "Gen", "Feb", "Mar", "Apr", "Mag", "Giu", "Lug", "Ago", "Set", "Ott", "Nov", "Dic" },
                "de" => new[] { "Jan", "Feb", "Mär", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dez" },
                _    => new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }
            };

            for (int m = 1; m <= 12; m++)
            {
                result.Labels.Add(monthNames[m - 1]);
                result.SalesData.Add(salesData.FirstOrDefault(s => s.Month == m)?.Total ?? 0m);
                result.PurchasesData.Add(purchasesData.FirstOrDefault(p => p.Month == m)?.Total ?? 0m);
            }

            return Json(result);
        }

        // GET: /Dashboard/GetCategoryDistributionChartData
        [HttpGet]
        public async Task<IActionResult> GetCategoryDistributionChartData()
        {
            var data = await _context.Categories
                .Select(c => new
                {
                    CategoryName = c.CategoryName,
                    ProductCount = c.Products.Count()
                })
                .Where(c => c.ProductCount > 0)
                .ToListAsync();

            var result = new CategoryDistributionChartViewModel
            {
                CategoryNames = data.Select(d => d.CategoryName).ToList(),
                ProductCounts = data.Select(d => d.ProductCount).ToList()
            };

            return Json(result);
        }

        #endregion

        [HttpGet]
        public async Task<IActionResult> GlobalSearch(string term)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Length < 1)
            {
                return Json(new { products = new object[] { }, categories = new object[] { }, suppliers = new object[] { } });
            }

            term = term.Trim().ToLower();

            var products = await _context.Products
                .Where(p => p.ProductName.ToLower().Contains(term) || p.SKU.ToLower().Contains(term))
                .Take(5)
                .Select(p => new { p.ProductID, p.ProductName, p.SKU })
                .ToListAsync();

            var categories = await _context.Categories
                .Where(c => c.CategoryName.ToLower().Contains(term))
                .Take(3)
                .Select(c => new { c.CategoryID, c.CategoryName })
                .ToListAsync();

            var suppliers = await _context.Suppliers
                .Where(s => s.SupplierName.ToLower().Contains(term) ||
                      (s.ContactName != null && s.ContactName.ToLower().Contains(term)))
                .Take(3)
                .Select(s => new { s.SupplierID, s.SupplierName })
                .ToListAsync();

            return Json(new { products, categories, suppliers });
        }
    }
}