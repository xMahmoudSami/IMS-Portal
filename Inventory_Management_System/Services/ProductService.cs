using Inventory_Management_System.Models.Webhooks.Events;
using Inventory_Management_System.Services.Webhooks;

namespace Inventory_Management_System.Services
{
    public class ProductService : IProductService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebhookDispatcher _webhookDispatcher;

        public ProductService(ApplicationDbContext context, IWebhookDispatcher webhookDispatcher)
        {
            _context = context;
            _webhookDispatcher = webhookDispatcher;
        }

        public async Task<PagedResult<ProductListItemViewModel>> GetProductsAsync(ProductFilterViewModel filter)
        {
            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.SupplierProducts)
                .AsQueryable();

            // 1. Search filter by Product Name or SKU
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim().ToLower();
                query = query.Where(p => p.ProductName.ToLower().Contains(term) || p.SKU.ToLower().Contains(term));
            }

            // 2. Category filter
            if (filter.CategoryID.HasValue && filter.CategoryID.Value > 0)
            {
                query = query.Where(p => p.CategoryID == filter.CategoryID.Value);
            }

            // 3. Stock status filter (Evaluate Out of Stock first)
            switch (filter.Status)
            {
                case StockStatusFilter.OutOfStock:
                    query = query.Where(p => p.StockQuantity == 0);
                    break;
                case StockStatusFilter.LowStock:
                    query = query.Where(p => p.StockQuantity > 0 && p.StockQuantity <= p.LowStockThreshold);
                    break;
                case StockStatusFilter.InStock:
                    query = query.Where(p => p.StockQuantity > p.LowStockThreshold);
                    break;
            }

            var totalItems = await query.CountAsync();
            var page = Math.Max(1, filter.Page);
            var pageSize = filter.PageSize is 10 or 25 or 50 or 100 ? filter.PageSize : 10;

            var items = await query
                .OrderBy(p => p.ProductName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductListItemViewModel
                {
                    ProductID = p.ProductID,
                    SKU = p.SKU,
                    ProductName = p.ProductName,
                    CategoryID = p.CategoryID,
                    CategoryName = p.Category != null ? p.Category.CategoryName : "Uncategorized",
                    UnitPrice = p.UnitPrice,
                    StockQuantity = p.StockQuantity,
                    LowStockThreshold = p.LowStockThreshold,
                    MappedSuppliersCount = p.SupplierProducts.Count()
                })
                .ToListAsync();

            return new PagedResult<ProductListItemViewModel>
            {
                Items = items,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };
        }

        public async Task<ProductFormViewModel?> GetProductForEditAsync(int id)
        {
            var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductID == id);
            if (product == null) return null;

            return new ProductFormViewModel
            {
                ProductID = product.ProductID,
                SKU = product.SKU,
                ProductName = product.ProductName,
                CategoryID = product.CategoryID,
                UnitPrice = product.UnitPrice,
                StockQuantity = product.StockQuantity,
                LowStockThreshold = product.LowStockThreshold
            };
        }

        public async Task<ProductDetailsViewModel?> GetProductDetailsAsync(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.SupplierProducts)
                    .ThenInclude(sp => sp.Supplier)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null) return null;

            var purchaseItemsCount = await _context.PurchaseItems.CountAsync(pi => pi.ProductID == id);
            var saleItemsCount = await _context.SaleItems.CountAsync(si => si.ProductID == id);

            return new ProductDetailsViewModel
            {
                ProductID = product.ProductID,
                SKU = product.SKU,
                ProductName = product.ProductName,
                CategoryID = product.CategoryID,
                CategoryName = product.Category != null ? product.Category.CategoryName : "Uncategorized",
                UnitPrice = product.UnitPrice,
                StockQuantity = product.StockQuantity,
                LowStockThreshold = product.LowStockThreshold,
                PurchaseItemsCount = purchaseItemsCount,
                SaleItemsCount = saleItemsCount,
                Suppliers = product.SupplierProducts.Select(sp => new ProductSupplierItemViewModel
                {
                    SupplierProductID = sp.SupplierProductID,
                    SupplierID = sp.SupplierID,
                    SupplierName = sp.Supplier?.SupplierName ?? "Unknown Supplier",
                    ContactName = sp.Supplier?.ContactName ?? "",
                    Phone = sp.Supplier?.Phone ?? "",
                    Email = sp.Supplier?.Email ?? "",
                    SupplierSKU = sp.SupplierSKU,
                    ContractPrice = sp.ContractPrice,
                    LeadTimeDays = sp.LeadTimeDays
                }).OrderBy(s => s.SupplierName).ToList()
            };
        }

        public async Task<ProductDeleteViewModel?> GetProductForDeleteAsync(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null) return null;

            var purchaseCount = await _context.PurchaseItems.CountAsync(pi => pi.ProductID == id);
            var saleCount = await _context.SaleItems.CountAsync(si => si.ProductID == id);
            var supplierProductCount = await _context.SupplierProducts.CountAsync(sp => sp.ProductID == id);

            return new ProductDeleteViewModel
            {
                ProductID = product.ProductID,
                SKU = product.SKU,
                ProductName = product.ProductName,
                CategoryName = product.Category?.CategoryName ?? "Uncategorized",
                UnitPrice = product.UnitPrice,
                StockQuantity = product.StockQuantity,
                PurchaseItemsCount = purchaseCount,
                SaleItemsCount = saleCount,
                SupplierProductsCount = supplierProductCount
            };
        }

        public async Task<OperationResult> CreateProductAsync(ProductFormViewModel model)
        {
            // 1. Verify Category exists
            var categoryExists = await _context.Categories.AnyAsync(c => c.CategoryID == model.CategoryID);
            if (!categoryExists)
            {
                return OperationResult.Fail("The selected category does not exist.");
            }

            // 2. Verify SKU uniqueness
            var skuUnique = await IsSkuUniqueAsync(model.SKU);
            if (!skuUnique)
            {
                return OperationResult.Fail($"The SKU '{model.SKU}' is already in use by another product.");
            }

            var entity = new Product
            {
                SKU = model.SKU.Trim(),
                ProductName = model.ProductName.Trim(),
                CategoryID = model.CategoryID,
                UnitPrice = model.UnitPrice,
                StockQuantity = model.StockQuantity,
                LowStockThreshold = model.LowStockThreshold
            };

            try
            {
                _context.Products.Add(entity);
                await _context.SaveChangesAsync();
                return OperationResult.Ok("Product created successfully.");
            }
            catch (DbUpdateException)
            {
                return OperationResult.Fail("A database conflict occurred. The SKU may already exist.");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail("An error occurred while saving the product: " + ex.Message);
            }
        }

        public async Task<OperationResult> UpdateProductAsync(ProductFormViewModel model)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == model.ProductID);
            if (product == null)
            {
                return OperationResult.Fail("Product not found.");
            }

            // 1. Verify Category exists
            var categoryExists = await _context.Categories.AnyAsync(c => c.CategoryID == model.CategoryID);
            if (!categoryExists)
            {
                return OperationResult.Fail("The selected category does not exist.");
            }

            // 2. Verify SKU uniqueness (excluding current product)
            var skuUnique = await IsSkuUniqueAsync(model.SKU, model.ProductID);
            if (!skuUnique)
            {
                return OperationResult.Fail($"The SKU '{model.SKU}' is already in use by another product.");
            }

            product.SKU = model.SKU.Trim();
            product.ProductName = model.ProductName.Trim();
            product.CategoryID = model.CategoryID;
            product.UnitPrice = model.UnitPrice;
            product.StockQuantity = model.StockQuantity;
            product.LowStockThreshold = model.LowStockThreshold;

            try
            {
                await _context.SaveChangesAsync();

                if (product.StockQuantity <= product.LowStockThreshold)
                {
                    var cat = await _context.Categories.FindAsync(product.CategoryID);
                    await _webhookDispatcher.DispatchAsync("inventory.low_stock_alert", new LowStockAlertEvent
                    {
                        ProductId = product.ProductID,
                        Sku = product.SKU,
                        ProductName = product.ProductName,
                        CurrentStock = product.StockQuantity,
                        LowStockThreshold = product.LowStockThreshold,
                        UnitPrice = product.UnitPrice,
                        CategoryName = cat?.CategoryName,
                        TriggerSource = "Catalog Stock Adjustment",
                        TriggeredAt = DateTime.UtcNow
                    });
                }

                return OperationResult.Ok("Product updated successfully.");
            }
            catch (DbUpdateException)
            {
                return OperationResult.Fail("A database conflict occurred while updating the product.");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail("An error occurred while updating the product: " + ex.Message);
            }
        }

        public async Task<OperationResult> DeleteProductAsync(int id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == id);
            if (product == null)
            {
                return OperationResult.Fail("Product not found.");
            }

            // DELETE SAFETY: Check purchase items and sales items
            var purchaseCount = await _context.PurchaseItems.CountAsync(pi => pi.ProductID == id);
            var saleCount = await _context.SaleItems.CountAsync(si => si.ProductID == id);

            if (purchaseCount > 0 || saleCount > 0)
            {
                var reasons = new List<string>();
                if (purchaseCount > 0) reasons.Add($"{purchaseCount} purchase history record(s)");
                if (saleCount > 0) reasons.Add($"{saleCount} sales history record(s)");
                return OperationResult.Fail($"This product cannot be deleted because it has {string.Join(" and ", reasons)}. Historical business records must be preserved.");
            }

            try
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                return OperationResult.Ok("Product deleted successfully.");
            }
            catch (DbUpdateException)
            {
                return OperationResult.Fail("Cannot delete this product because related records depend on it.");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail("An error occurred while deleting the product: " + ex.Message);
            }
        }

        public async Task<bool> IsSkuUniqueAsync(string sku, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(sku)) return true;
            var trimmed = sku.Trim().ToLower();

            var query = _context.Products.AsNoTracking().Where(p => p.SKU.ToLower() == trimmed);
            if (excludeId.HasValue && excludeId.Value > 0)
            {
                query = query.Where(p => p.ProductID != excludeId.Value);
            }

            return !await query.AnyAsync();
        }

        public async Task<List<SelectListItem>> GetProductSelectListAsync()
        {
            return await _context.Products
                .AsNoTracking()
                .OrderBy(p => p.ProductName)
                .Select(p => new SelectListItem
                {
                    Value = p.ProductID.ToString(),
                    Text = $"{p.ProductName} ({p.SKU})"
                })
                .ToListAsync();
        }
    }
}
