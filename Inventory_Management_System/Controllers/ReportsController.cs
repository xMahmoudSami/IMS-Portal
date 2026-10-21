using Microsoft.AspNetCore.Authorization;

namespace Inventory_Management_System.Controllers
{
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Auditor},{AppRoles.WarehouseManager}")]
    [Route("Reports")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(ApplicationDbContext context, ILogger<ReportsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /Reports, /Reports/Index, or /Reports/StockHealth (Stock Valuation & Inventory Health)
        [HttpGet("")]
        [HttpGet("Index")]
        [HttpGet("StockHealth")]
        public async Task<IActionResult> StockHealth(int? categoryId, string? status, string? search)
        {
            var allProducts = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .OrderBy(p => p.ProductName)
                .ToListAsync();

            // Overall Catalog KPIs
            int totalItemsCount = allProducts.Count;
            int lowStockCount = allProducts.Count(p => p.StockQuantity <= p.LowStockThreshold && p.StockQuantity > 0);
            int outOfStockCount = allProducts.Count(p => p.StockQuantity == 0);
            decimal totalValuation = allProducts.Sum(p => p.StockQuantity * p.UnitPrice);

            // Filtering
            var filteredQuery = allProducts.AsEnumerable();

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                filteredQuery = filteredQuery.Where(p => p.CategoryID == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string searchLower = search.Trim().ToLower();
                filteredQuery = filteredQuery.Where(p =>
                    p.ProductName.ToLower().Contains(searchLower) ||
                    p.SKU.ToLower().Contains(searchLower));
            }

            string normalizedStatus = status?.Trim() ?? "All";
            if (!string.IsNullOrEmpty(normalizedStatus) && !normalizedStatus.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (normalizedStatus.Equals("InStock", StringComparison.OrdinalIgnoreCase))
                {
                    filteredQuery = filteredQuery.Where(p => p.StockQuantity > p.LowStockThreshold);
                }
                else if (normalizedStatus.Equals("LowStock", StringComparison.OrdinalIgnoreCase))
                {
                    filteredQuery = filteredQuery.Where(p => p.StockQuantity <= p.LowStockThreshold && p.StockQuantity > 0);
                }
                else if (normalizedStatus.Equals("OutOfStock", StringComparison.OrdinalIgnoreCase))
                {
                    filteredQuery = filteredQuery.Where(p => p.StockQuantity == 0);
                }
            }

            var items = filteredQuery.Select(p => new StockHealthItemViewModel
            {
                ProductID = p.ProductID,
                SKU = p.SKU,
                ProductName = p.ProductName,
                CategoryName = p.Category?.CategoryName ?? "Uncategorized",
                StockQuantity = p.StockQuantity,
                Threshold = p.LowStockThreshold,
                UnitPrice = p.UnitPrice
            }).ToList();

            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            var categorySelectList = categories.Select(c => new SelectListItem
            {
                Value = c.CategoryID.ToString(),
                Text = c.CategoryName,
                Selected = categoryId.HasValue && categoryId.Value == c.CategoryID
            }).ToList();

            var viewModel = new StockHealthReportViewModel
            {
                TotalItemsCount = totalItemsCount,
                LowStockCount = lowStockCount,
                OutOfStockCount = outOfStockCount,
                TotalValuationAmount = totalValuation,
                Items = items,
                Categories = categorySelectList,
                CategoryID = categoryId,
                StatusFilter = normalizedStatus,
                Search = search
            };

            return View(viewModel);
        }

        // GET: /Reports/ExportStockHealthCsv
        [HttpGet("ExportStockHealthCsv")]
        public async Task<IActionResult> ExportStockHealthCsv(int? categoryId, string? status, string? search)
        {
            var allProducts = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .OrderBy(p => p.ProductName)
                .ToListAsync();

            var filteredQuery = allProducts.AsEnumerable();

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                filteredQuery = filteredQuery.Where(p => p.CategoryID == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string searchLower = search.Trim().ToLower();
                filteredQuery = filteredQuery.Where(p =>
                    p.ProductName.ToLower().Contains(searchLower) ||
                    p.SKU.ToLower().Contains(searchLower));
            }

            string normalizedStatus = status?.Trim() ?? "All";
            if (!string.IsNullOrEmpty(normalizedStatus) && !normalizedStatus.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (normalizedStatus.Equals("InStock", StringComparison.OrdinalIgnoreCase))
                {
                    filteredQuery = filteredQuery.Where(p => p.StockQuantity > p.LowStockThreshold);
                }
                else if (normalizedStatus.Equals("LowStock", StringComparison.OrdinalIgnoreCase))
                {
                    filteredQuery = filteredQuery.Where(p => p.StockQuantity <= p.LowStockThreshold && p.StockQuantity > 0);
                }
                else if (normalizedStatus.Equals("OutOfStock", StringComparison.OrdinalIgnoreCase))
                {
                    filteredQuery = filteredQuery.Where(p => p.StockQuantity == 0);
                }
            }

            var csv = new StringBuilder();
            csv.AppendLine("Product ID,SKU,Product Name,Category,Unit Price ($),Stock Quantity,Threshold,Total Valuation ($),Status");

            foreach (var p in filteredQuery)
            {
                string itemStatus = p.StockQuantity == 0 ? "Out of Stock" : (p.StockQuantity <= p.LowStockThreshold ? "Low Stock" : "In Stock");
                decimal valuation = p.StockQuantity * p.UnitPrice;
                csv.AppendLine($"\"{p.ProductID}\",\"{EscapeCsv(p.SKU)}\",\"{EscapeCsv(p.ProductName)}\",\"{EscapeCsv(p.Category?.CategoryName ?? "N/A")}\",{p.UnitPrice:F2},{p.StockQuantity},{p.LowStockThreshold},{valuation:F2},\"{itemStatus}\"");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"Stock_Valuation_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }

        // GET: /Reports/Sales (Sales Performance Report)
        [HttpGet("Sales")]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Auditor}")]
        public async Task<IActionResult> Sales(string? preset = "ThisMonth", DateTime? fromDate = null, DateTime? toDate = null, string? search = null)
        {
            DateTime now = DateTime.Now;
            DateTime startDate;
            DateTime endDate;

            string normalizedPreset = string.IsNullOrWhiteSpace(preset) ? "ThisMonth" : preset;

            switch (normalizedPreset)
            {
                case "Today":
                    startDate = DateTime.Today;
                    endDate = DateTime.Today.AddDays(1).AddTicks(-1);
                    break;
                case "ThisWeek":
                    int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                    startDate = now.AddDays(-1 * diff).Date;
                    endDate = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "Custom":
                    startDate = fromDate.HasValue ? fromDate.Value.Date : new DateTime(now.Year, now.Month, 1);
                    endDate = toDate.HasValue ? toDate.Value.Date.AddDays(1).AddTicks(-1) : now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "ThisMonth":
                default:
                    normalizedPreset = "ThisMonth";
                    startDate = new DateTime(now.Year, now.Month, 1);
                    endDate = startDate.AddMonths(1).AddTicks(-1);
                    break;
            }

            var query = _context.Sales
                .AsNoTracking()
                .Include(s => s.SaleItems)
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string searchLower = search.Trim().ToLower();
                if (int.TryParse(search.Trim().TrimStart('#'), out int parsedId))
                {
                    query = query.Where(s => s.SaleID == parsedId || (s.CustomerInfo != null && s.CustomerInfo.ToLower().Contains(searchLower)));
                }
                else
                {
                    query = query.Where(s => s.CustomerInfo != null && s.CustomerInfo.ToLower().Contains(searchLower));
                }
            }

            var salesList = await query
                .OrderByDescending(s => s.SaleDate)
                .ThenByDescending(s => s.SaleID)
                .ToListAsync();

            decimal totalRevenue = salesList.Sum(s => s.TotalAmount);
            int totalSalesCount = salesList.Count;
            int totalUnitsSold = salesList.Sum(s => s.SaleItems.Sum(si => si.Quantity));
            decimal avgOrderValue = totalSalesCount > 0 ? totalRevenue / totalSalesCount : 0;

            var salesRecords = salesList.Select(s => new SalesSummaryItemViewModel
            {
                SaleID = s.SaleID,
                SaleDate = s.SaleDate,
                CustomerInfo = string.IsNullOrWhiteSpace(s.CustomerInfo) ? "Walk-in Customer" : s.CustomerInfo,
                ItemsCount = s.SaleItems.Count,
                TotalUnitsSold = s.SaleItems.Sum(si => si.Quantity),
                TotalAmount = s.TotalAmount
            }).ToList();

            var viewModel = new SalesReportViewModel
            {
                StartDate = startDate,
                EndDate = endDate,
                DatePreset = normalizedPreset,
                Search = search,
                TotalRevenue = totalRevenue,
                TotalSalesCount = totalSalesCount,
                TotalUnitsSold = totalUnitsSold,
                AverageOrderValue = avgOrderValue,
                SalesRecords = salesRecords
            };

            return View(viewModel);
        }

        // GET: /Reports/ExportSalesCsv
        [HttpGet("ExportSalesCsv")]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Auditor}")]
        public async Task<IActionResult> ExportSalesCsv(string? preset = "ThisMonth", DateTime? fromDate = null, DateTime? toDate = null, string? search = null)
        {
            DateTime now = DateTime.Now;
            DateTime startDate;
            DateTime endDate;

            string normalizedPreset = string.IsNullOrWhiteSpace(preset) ? "ThisMonth" : preset;

            switch (normalizedPreset)
            {
                case "Today":
                    startDate = DateTime.Today;
                    endDate = DateTime.Today.AddDays(1).AddTicks(-1);
                    break;
                case "ThisWeek":
                    int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                    startDate = now.AddDays(-1 * diff).Date;
                    endDate = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "Custom":
                    startDate = fromDate.HasValue ? fromDate.Value.Date : new DateTime(now.Year, now.Month, 1);
                    endDate = toDate.HasValue ? toDate.Value.Date.AddDays(1).AddTicks(-1) : now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "ThisMonth":
                default:
                    startDate = new DateTime(now.Year, now.Month, 1);
                    endDate = startDate.AddMonths(1).AddTicks(-1);
                    break;
            }

            var query = _context.Sales
                .AsNoTracking()
                .Include(s => s.SaleItems)
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string searchLower = search.Trim().ToLower();
                if (int.TryParse(search.Trim().TrimStart('#'), out int parsedId))
                {
                    query = query.Where(s => s.SaleID == parsedId || (s.CustomerInfo != null && s.CustomerInfo.ToLower().Contains(searchLower)));
                }
                else
                {
                    query = query.Where(s => s.CustomerInfo != null && s.CustomerInfo.ToLower().Contains(searchLower));
                }
            }

            var salesList = await query
                .OrderByDescending(s => s.SaleDate)
                .ThenByDescending(s => s.SaleID)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Sale ID,Date,Customer Info,Item Lines,Total Units Sold,Total Amount ($)");

            foreach (var s in salesList)
            {
                string customer = string.IsNullOrWhiteSpace(s.CustomerInfo) ? "Walk-in Customer" : s.CustomerInfo;
                int units = s.SaleItems.Sum(si => si.Quantity);
                csv.AppendLine($"\"{s.SaleID}\",\"{s.SaleDate:yyyy-MM-dd}\",\"{EscapeCsv(customer)}\",{s.SaleItems.Count},{units},{s.TotalAmount:F2}");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"Sales_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }

        private static string EscapeCsv(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\"", "\"\"");
        }
    }
}
