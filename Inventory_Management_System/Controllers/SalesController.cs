using Microsoft.AspNetCore.Authorization;
using Inventory_Management_System.Models.Webhooks.Events;
using Inventory_Management_System.Services.Webhooks;

namespace Inventory_Management_System.Controllers
{
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Cashier},{AppRoles.Auditor}")]
    [Route("Sales")]
    public class SalesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebhookDispatcher _webhookDispatcher;
        private readonly ILogger<SalesController> _logger;

        public SalesController(
            ApplicationDbContext context,
            IWebhookDispatcher webhookDispatcher,
            ILogger<SalesController> logger)
        {
            _context = context;
            _webhookDispatcher = webhookDispatcher;
            _logger = logger;
        }

        // GET: /Sales or /Sales/Index
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(int page = 1, string? search = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            const int pageSize = 10;
            if (page < 1) page = 1;

            var query = _context.Sales
                .AsNoTracking()
                .Include(s => s.SaleItems)
                .AsQueryable();

            // Search filter by customer info or ID
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

            // Date filtering
            if (fromDate.HasValue)
            {
                DateTime from = fromDate.Value.Date;
                query = query.Where(s => s.SaleDate >= from);
            }

            if (toDate.HasValue)
            {
                DateTime to = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(s => s.SaleDate <= to);
            }

            // Calculate KPI summary figures across all filtered items
            var allSales = await query.ToListAsync();
            decimal totalRevenue = allSales.Sum(s => s.TotalAmount);
            int totalSalesCount = allSales.Count;
            int totalUnitsSold = allSales.Sum(s => s.SaleItems.Sum(si => si.Quantity));
            decimal avgOrderValue = totalSalesCount > 0 ? totalRevenue / totalSalesCount : 0;

            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);
            decimal todayRevenue = allSales.Where(s => s.SaleDate >= today && s.SaleDate < tomorrow).Sum(s => s.TotalAmount);

            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TotalSalesCount = totalSalesCount;
            ViewBag.TotalUnitsSold = totalUnitsSold;
            ViewBag.AvgOrderValue = avgOrderValue;
            ViewBag.TodayRevenue = todayRevenue;

            ViewBag.CurrentSearch = search;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            // Server-side pagination
            int totalItems = allSales.Count;
            var pagedItems = allSales
                .OrderByDescending(s => s.SaleDate)
                .ThenByDescending(s => s.SaleID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new SaleListItemViewModel
                {
                    SaleID = s.SaleID,
                    SaleDate = s.SaleDate,
                    CustomerInfo = string.IsNullOrWhiteSpace(s.CustomerInfo) ? "Walk-in Customer" : s.CustomerInfo,
                    TotalAmount = s.TotalAmount,
                    ItemsCount = s.SaleItems.Count,
                    TotalUnits = s.SaleItems.Sum(si => si.Quantity)
                })
                .ToList();

            var pagedResult = new PagedResult<SaleListItemViewModel>
            {
                Items = pagedItems,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            return View(pagedResult);
        }

        // GET: /Sales/Details/{id} or /Sales/Invoice/{id}
        [HttpGet("Details/{id}")]
        [HttpGet("Invoice/{id}")]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            int saleId = 0;
            if (id.StartsWith("INV-", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(id.Substring(4), out saleId);
            }
            else
            {
                int.TryParse(id, out saleId);
            }

            if (saleId <= 0) return NotFound();

            var sale = await _context.Sales
                .AsNoTracking()
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .FirstOrDefaultAsync(s => s.SaleID == saleId);

            if (sale == null) return NotFound();

            var viewModel = new SaleDetailsViewModel
            {
                SaleID = sale.SaleID,
                SaleDate = sale.SaleDate,
                CustomerInfo = string.IsNullOrWhiteSpace(sale.CustomerInfo) ? "Walk-in Customer" : sale.CustomerInfo,
                TotalAmount = sale.TotalAmount,
                Items = sale.SaleItems.Select(si => new SaleItemDetailsViewModel
                {
                    SaleItemID = si.SaleItemID,
                    ProductID = si.ProductID,
                    ProductName = si.Product?.ProductName ?? "Unknown Product",
                    SKU = si.Product?.SKU ?? "—",
                    Quantity = si.Quantity,
                    UnitPrice = si.UnitPrice
                }).ToList()
            };

            return View("Details", viewModel);
        }

        // GET: /Sales/Create
        [HttpGet("Create")]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Cashier}")]
        public async Task<IActionResult> Create()
        {
            var model = new SaleFormViewModel
            {
                SaleDate = DateTime.Today
            };

            await PopulateProductListsAsync(model);

            return View(model);
        }

        // POST: /Sales/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Cashier}")]
        public async Task<IActionResult> Create(SaleFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateProductListsAsync(model);
                return View(model);
            }

            if (model.Items == null || !model.Items.Any())
            {
                ModelState.AddModelError("", "At least one product item is required.");
                await PopulateProductListsAsync(model);
                return View(model);
            }

            // Reject duplicate products in the same sale
            var duplicateProducts = model.Items
                .GroupBy(x => x.ProductID)
                .Where(x => x.Count() > 1)
                .ToList();

            if (duplicateProducts.Any())
            {
                ModelState.AddModelError("", "You cannot add the same product multiple times in one transaction. Please adjust the item quantity instead.");
                await PopulateProductListsAsync(model);
                return View(model);
            }

            // Check non-positive quantities
            if (model.Items.Any(i => i.Quantity <= 0))
            {
                ModelState.AddModelError("", "All item quantities must be at least 1.");
                await PopulateProductListsAsync(model);
                return View(model);
            }

            // Open EF Core strict atomic transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                decimal totalAmount = 0;
                var saleItems = new List<SaleItem>();

                foreach (var item in model.Items)
                {
                    var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == item.ProductID);

                    if (product == null)
                    {
                        ModelState.AddModelError("", $"Product not found (ID: {item.ProductID}).");
                        await transaction.RollbackAsync();
                        await PopulateProductListsAsync(model);
                        return View(model);
                    }

                    // Strict stock validation
                    if (item.Quantity > product.StockQuantity)
                    {
                        ModelState.AddModelError("", $"Insufficient stock for product '{product.ProductName}'. Available: {product.StockQuantity}, Requested: {item.Quantity}");
                        await transaction.RollbackAsync();
                        await PopulateProductListsAsync(model);
                        return View(model);
                    }

                    // Atomic stock deduction
                    product.StockQuantity -= item.Quantity;

                    // Snapshot pricing: capture current product unit price
                    decimal snapshotPrice = product.UnitPrice;
                    totalAmount += item.Quantity * snapshotPrice;

                    saleItems.Add(new SaleItem
                    {
                        ProductID = item.ProductID,
                        Quantity = item.Quantity,
                        UnitPrice = snapshotPrice
                    });
                }

                var sale = new Sale
                {
                    SaleDate = model.SaleDate,
                    CustomerInfo = string.IsNullOrWhiteSpace(model.CustomerInfo) ? "Walk-in Customer" : model.CustomerInfo.Trim(),
                    TotalAmount = totalAmount,
                    SaleItems = saleItems
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Evaluate and dispatch low-stock alerts asynchronously
                foreach (var item in saleItems)
                {
                    var prod = await _context.Products
                        .Include(p => p.Category)
                        .FirstOrDefaultAsync(p => p.ProductID == item.ProductID);

                    if (prod != null && prod.StockQuantity <= prod.LowStockThreshold)
                    {
                        await _webhookDispatcher.DispatchAsync("inventory.low_stock_alert", new LowStockAlertEvent
                        {
                            ProductId = prod.ProductID,
                            Sku = prod.SKU,
                            ProductName = prod.ProductName,
                            CurrentStock = prod.StockQuantity,
                            LowStockThreshold = prod.LowStockThreshold,
                            UnitPrice = prod.UnitPrice,
                            CategoryName = prod.Category?.CategoryName,
                            TriggerSource = $"POS Sale Order #{sale.SaleID}",
                            TriggeredAt = DateTime.UtcNow
                        });
                    }
                }

                TempData["SuccessMessage"] = $"Sale #{sale.SaleID} completed successfully and inventory stock was deducted.";
                return RedirectToAction(nameof(Details), new { id = sale.SaleID });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating sale transaction.");
                ModelState.AddModelError("", $"An unexpected error occurred while saving the sale: {ex.Message}");
                await PopulateProductListsAsync(model);
                return View(model);
            }
        }

        // GET: /Sales/Delete/{id}
        [HttpGet("Delete/{id}")]
        [Authorize(Roles = AppRoles.SuperAdmin)]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0) return NotFound();

            var sale = await _context.Sales
                .AsNoTracking()
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .FirstOrDefaultAsync(s => s.SaleID == id);

            if (sale == null) return NotFound();

            var viewModel = new SaleDeleteViewModel
            {
                SaleID = sale.SaleID,
                SaleDate = sale.SaleDate,
                CustomerInfo = string.IsNullOrWhiteSpace(sale.CustomerInfo) ? "Walk-in Customer" : sale.CustomerInfo,
                TotalAmount = sale.TotalAmount,
                ItemsCount = sale.SaleItems.Count,
                Items = sale.SaleItems.Select(si => new SaleItemDetailsViewModel
                {
                    SaleItemID = si.SaleItemID,
                    ProductID = si.ProductID,
                    ProductName = si.Product?.ProductName ?? "Unknown Product",
                    SKU = si.Product?.SKU ?? "—",
                    Quantity = si.Quantity,
                    UnitPrice = si.UnitPrice
                }).ToList()
            };

            return View(viewModel);
        }

        // POST: /Sales/Delete/{id}
        [HttpPost("Delete/{id}")]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.SuperAdmin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (id <= 0) return BadRequest();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var sale = await _context.Sales
                    .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Product)
                    .FirstOrDefaultAsync(s => s.SaleID == id);

                if (sale == null) return NotFound();

                // Atomically revert product stock
                foreach (var item in sale.SaleItems)
                {
                    var product = item.Product ?? await _context.Products.FindAsync(item.ProductID);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity;
                    }
                }

                _context.SaleItems.RemoveRange(sale.SaleItems);
                _context.Sales.Remove(sale);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Sale Order #{id} was deleted and product stock quantities were successfully reverted.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error deleting sale order {SaleID}", id);
                TempData["ErrorMessage"] = $"Failed to delete sale order: {ex.Message}";
                return RedirectToAction(nameof(Delete), new { id });
            }
        }

        // Helper: populate products dropdown and catalog metadata for client-side auto-fill
        private async Task PopulateProductListsAsync(SaleFormViewModel model)
        {
            var products = await _context.Products
                .AsNoTracking()
                .OrderBy(p => p.ProductName)
                .ToListAsync();

            model.Products = products.Select(p => new SelectListItem
            {
                Value = p.ProductID.ToString(),
                Text = $"{p.ProductName} (SKU: {p.SKU}) - ${p.UnitPrice:N2} [{p.StockQuantity} in stock]"
            }).ToList();

            model.ProductCatalog = products.Select(p => new SaleProductOptionViewModel
            {
                ProductID = p.ProductID,
                ProductName = p.ProductName,
                SKU = p.SKU,
                UnitPrice = p.UnitPrice,
                StockQuantity = p.StockQuantity
            }).ToList();
        }
    }
}
