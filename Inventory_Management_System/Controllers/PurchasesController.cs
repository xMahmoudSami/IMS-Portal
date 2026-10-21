using Microsoft.AspNetCore.Authorization;

namespace Inventory_Management_System.Controllers
{
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.ProcurementSpecialist},{AppRoles.Auditor}")]
    public class PurchasesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PurchasesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Purchases
        public async Task<IActionResult> Index()
        {
            var purchases = await _context.Purchases
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseItems)
                .Select(p => new PurchaseListItemViewModel
                {
                    PurchaseID = p.PurchaseID,
                    PurchaseDate = p.PurchaseDate,
                    SupplierID = p.SupplierID,
                    SupplierName = p.Supplier != null ? p.Supplier.SupplierName : "N/A",
                    TotalAmount = p.TotalAmount,
                    ItemsCount = p.PurchaseItems.Count
                })
                .ToListAsync();

            return View(purchases);
        }

        // GET: /Purchases/Details/{id}
        public async Task<IActionResult> Details(int id)
        {
            var purchase = await _context.Purchases
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseItems)
                .ThenInclude(pi => pi.Product)
                .FirstOrDefaultAsync(p => p.PurchaseID == id);

            if (purchase == null) return NotFound();

            var viewModel = new PurchaseDetailsViewModel
            {
                PurchaseID = purchase.PurchaseID,
                PurchaseDate = purchase.PurchaseDate,
                SupplierID = purchase.SupplierID,
                SupplierName = purchase.Supplier?.SupplierName ?? "N/A",
                TotalAmount = purchase.TotalAmount,
                Items = purchase.PurchaseItems.Select(pi => new PurchaseItemDetailsViewModel
                {
                    PurchaseItemID = pi.PurchaseItemID,
                    ProductID = pi.ProductID,
                    ProductName = pi.Product?.ProductName ?? "N/A",
                    SKU = pi.Product?.SKU ?? "N/A",
                    Quantity = pi.Quantity,
                    UnitCost = pi.UnitCost
                }).ToList()
            };

            return View(viewModel);
        }

        // GET: /Purchases/Create
        [HttpGet]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.ProcurementSpecialist}")]
        public async Task<IActionResult> Create()
        {
            var model = new PurchaseFormViewModel
            {
                PurchaseDate = DateTime.Now
            };

            await PopulateSelectListsAsync(model);

            return View(model);
        }

        // POST: /Purchases/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.ProcurementSpecialist}")]
        public async Task<IActionResult> Create(PurchaseFormViewModel model)
        {
            if (model.SupplierID <= 0)
            {
                ModelState.AddModelError("SupplierID", "Please select a valid supplier.");
            }

            if (model.Items == null || !model.Items.Any())
            {
                ModelState.AddModelError("", "At least one product is required.");
            }
            else
            {
                var duplicateProducts = model.Items
                    .GroupBy(x => x.ProductID)
                    .Where(x => x.Key > 0 && x.Count() > 1)
                    .ToList();

                if (duplicateProducts.Any())
                {
                    ModelState.AddModelError("", "You cannot add the same product more than once.");
                }

                if (model.SupplierID > 0)
                {
                    var contractPrices = await _context.SupplierProducts
                        .Where(sp => sp.SupplierID == model.SupplierID)
                        .ToDictionaryAsync(sp => sp.ProductID, sp => sp.ContractPrice);

                    for (int i = 0; i < model.Items.Count; i++)
                    {
                        var item = model.Items[i];
                        if (item.ProductID <= 0)
                        {
                            ModelState.AddModelError($"Items[{i}].ProductID", "Please select a valid product.");
                            continue;
                        }

                        if (!contractPrices.TryGetValue(item.ProductID, out decimal contractPrice))
                        {
                            var prod = await _context.Products.FindAsync(item.ProductID);
                            ModelState.AddModelError("", $"Product '{prod?.ProductName ?? item.ProductID.ToString()}' is not supplied by the selected supplier.");
                            continue;
                        }

                        // Validates that UnitCost matches or defaults to the supplier's ContractPrice
                        if (item.UnitCost <= 0)
                        {
                            item.UnitCost = contractPrice;
                            ModelState.Remove($"Items[{i}].UnitCost");
                        }
                        else if (contractPrice > 0 && Math.Abs(item.UnitCost - contractPrice) > 0.01m)
                        {
                            var prod = await _context.Products.FindAsync(item.ProductID);
                            ModelState.AddModelError("", $"Unit cost for '{prod?.ProductName ?? item.ProductID.ToString()}' (${item.UnitCost:N2}) must match the supplier's contract price (${contractPrice:N2}).");
                        }
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                await PopulateSelectListsAsync(model);
                return View(model);
            }

            decimal totalAmount = model.Items!.Sum(item => item.Quantity * item.UnitCost);

            var purchase = new Purchase
            {
                SupplierID = model.SupplierID,
                PurchaseDate = model.PurchaseDate,
                TotalAmount = totalAmount
            };

            _context.Purchases.Add(purchase);
            await _context.SaveChangesAsync();

            foreach (var item in model.Items!)
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == item.ProductID);

                if (product == null)
                {
                    ModelState.AddModelError("", "Product not found.");
                    await PopulateSelectListsAsync(model);
                    return View(model);
                }

                product.StockQuantity += item.Quantity;

                var purchaseItem = new PurchaseItem
                {
                    PurchaseID = purchase.PurchaseID,
                    ProductID = item.ProductID,
                    Quantity = item.Quantity,
                    UnitCost = item.UnitCost
                };

                _context.PurchaseItems.Add(purchaseItem);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Purchases/Edit/{id}
        [HttpGet]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.ProcurementSpecialist}")]
        public async Task<IActionResult> Edit(int id)
        {
            var purchase = await _context.Purchases
                .Include(p => p.PurchaseItems)
                .FirstOrDefaultAsync(p => p.PurchaseID == id);

            if (purchase == null) return NotFound();

            var model = new PurchaseFormViewModel
            {
                PurchaseID = purchase.PurchaseID,
                SupplierID = purchase.SupplierID,
                PurchaseDate = purchase.PurchaseDate,
                TotalAmount = purchase.TotalAmount,
                Items = purchase.PurchaseItems.Select(item => new PurchaseItemFormViewModel
                {
                    PurchaseItemID = item.PurchaseItemID,
                    ProductID = item.ProductID,
                    Quantity = item.Quantity,
                    UnitCost = item.UnitCost
                }).ToList()
            };

            await PopulateSelectListsAsync(model);

            return View(model);
        }

        // POST: /Purchases/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.ProcurementSpecialist}")]
        public async Task<IActionResult> Edit(int id, PurchaseFormViewModel model)
        {
            if (model.SupplierID <= 0)
            {
                ModelState.AddModelError("SupplierID", "Please select a valid supplier.");
            }

            if (model.Items == null || !model.Items.Any())
            {
                ModelState.AddModelError("", "At least one product item is required.");
            }
            else
            {
                var duplicateProducts = model.Items
                    .GroupBy(x => x.ProductID)
                    .Where(x => x.Key > 0 && x.Count() > 1)
                    .ToList();

                if (duplicateProducts.Any())
                {
                    ModelState.AddModelError("", "You cannot add the same product more than once.");
                }

                if (model.SupplierID > 0)
                {
                    var contractPrices = await _context.SupplierProducts
                        .Where(sp => sp.SupplierID == model.SupplierID)
                        .ToDictionaryAsync(sp => sp.ProductID, sp => sp.ContractPrice);

                    for (int i = 0; i < model.Items.Count; i++)
                    {
                        var item = model.Items[i];
                        if (item.ProductID <= 0)
                        {
                            ModelState.AddModelError($"Items[{i}].ProductID", "Please select a valid product.");
                            continue;
                        }

                        if (!contractPrices.TryGetValue(item.ProductID, out decimal contractPrice))
                        {
                            var prod = await _context.Products.FindAsync(item.ProductID);
                            ModelState.AddModelError("", $"Product '{prod?.ProductName ?? item.ProductID.ToString()}' is not supplied by the selected supplier.");
                            continue;
                        }

                        // Validates that UnitCost matches or defaults to the supplier's ContractPrice
                        if (item.UnitCost <= 0)
                        {
                            item.UnitCost = contractPrice;
                            ModelState.Remove($"Items[{i}].UnitCost");
                        }
                        else if (contractPrice > 0 && Math.Abs(item.UnitCost - contractPrice) > 0.01m)
                        {
                            var prod = await _context.Products.FindAsync(item.ProductID);
                            ModelState.AddModelError("", $"Unit cost for '{prod?.ProductName ?? item.ProductID.ToString()}' (${item.UnitCost:N2}) must match the supplier's contract price (${contractPrice:N2}).");
                        }
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                await PopulateSelectListsAsync(model);
                return View(model);
            }

            var purchase = await _context.Purchases
                .Include(p => p.PurchaseItems)
                .FirstOrDefaultAsync(p => p.PurchaseID == id);

            if (purchase == null) return NotFound();

            // Return old quantities to stock
            foreach (var oldItem in purchase.PurchaseItems)
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == oldItem.ProductID);
                if (product != null)
                {
                    product.StockQuantity -= oldItem.Quantity;
                }
            }

            _context.PurchaseItems.RemoveRange(purchase.PurchaseItems);

            purchase.SupplierID = model.SupplierID;
            purchase.PurchaseDate = model.PurchaseDate;

            decimal totalAmount = 0;

            foreach (var item in model.Items!)
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == item.ProductID);

                if (product == null)
                {
                    ModelState.AddModelError("", "Product not found.");
                    await PopulateSelectListsAsync(model);
                    return View(model);
                }

                product.StockQuantity += item.Quantity;
                totalAmount += item.Quantity * item.UnitCost;

                var purchaseItem = new PurchaseItem
                {
                    PurchaseID = purchase.PurchaseID,
                    ProductID = item.ProductID,
                    Quantity = item.Quantity,
                    UnitCost = item.UnitCost
                };

                _context.PurchaseItems.Add(purchaseItem);
            }

            purchase.TotalAmount = totalAmount;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: /Purchases/Delete/{id}
        [HttpGet]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.ProcurementSpecialist}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0) return NotFound();

            var purchase = await _context.Purchases
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseItems)
                .ThenInclude(pi => pi.Product)
                .FirstOrDefaultAsync(m => m.PurchaseID == id);

            if (purchase == null) return NotFound();

            var viewModel = new PurchaseDeleteViewModel
            {
                PurchaseID = purchase.PurchaseID,
                PurchaseDate = purchase.PurchaseDate,
                SupplierName = purchase.Supplier?.SupplierName ?? "N/A",
                TotalAmount = purchase.TotalAmount,
                ItemsCount = purchase.PurchaseItems.Count,
                Items = purchase.PurchaseItems.Select(pi => new PurchaseItemDetailsViewModel
                {
                    PurchaseItemID = pi.PurchaseItemID,
                    ProductID = pi.ProductID,
                    ProductName = pi.Product?.ProductName ?? "N/A",
                    SKU = pi.Product?.SKU ?? "N/A",
                    Quantity = pi.Quantity,
                    UnitCost = pi.UnitCost
                }).ToList()
            };

            foreach (var item in purchase.PurchaseItems)
            {
                if (item.Product != null && item.Product.StockQuantity < item.Quantity)
                {
                    viewModel.CanDelete = false;
                    viewModel.BlockingReason = $"Cannot delete purchase order. Product '{item.Product.ProductName}' current stock ({item.Product.StockQuantity}) is less than the quantity to revert ({item.Quantity}).";
                    break;
                }
            }

            return View(viewModel);
        }

        // POST: /Purchases/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.ProcurementSpecialist}")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (id <= 0) return BadRequest();

            var purchase = await _context.Purchases
                .Include(p => p.PurchaseItems)
                .ThenInclude(pi => pi.Product)
                .FirstOrDefaultAsync(p => p.PurchaseID == id);

            if (purchase == null) return NotFound();

            foreach (var item in purchase.PurchaseItems)
            {
                var product = item.Product ?? await _context.Products.FindAsync(item.ProductID);
                if (product != null && product.StockQuantity < item.Quantity)
                {
                    TempData["ErrorMessage"] = $"Cannot delete purchase. Product '{product.ProductName}' stock ({product.StockQuantity}) is less than the quantity to revert ({item.Quantity}).";
                    return RedirectToAction(nameof(Delete), new { id = id });
                }
            }

            foreach (var item in purchase.PurchaseItems)
            {
                var product = item.Product ?? await _context.Products.FindAsync(item.ProductID);
                if (product != null)
                {
                    product.StockQuantity -= item.Quantity;
                }
            }

            _context.PurchaseItems.RemoveRange(purchase.PurchaseItems);
            _context.Purchases.Remove(purchase);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Purchase deleted and inventory adjusted successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Purchases/GetProductsBySupplier?supplierId={id}
        [HttpGet]
        [Route("Purchases/GetProductsBySupplier")]
        [Route("api/Purchases/GetSupplierProducts")]
        public async Task<IActionResult> GetProductsBySupplier(int supplierId)
        {
            if (supplierId <= 0)
            {
                return Json(new List<object>());
            }

            var products = await _context.SupplierProducts
                .Include(sp => sp.Product)
                .Where(sp => sp.SupplierID == supplierId && sp.Product != null)
                .OrderBy(sp => sp.Product!.ProductName)
                .Select(sp => new
                {
                    productId = sp.ProductID,
                    productName = sp.Product!.ProductName,
                    sku = sp.Product!.SKU,
                    contractPrice = sp.ContractPrice,
                    displayText = $"{sp.Product!.ProductName} (SKU: {sp.Product!.SKU}) - ${sp.ContractPrice:N2}"
                })
                .ToListAsync();

            return Json(products);
        }

        // Helper Method To Populate Dropdown Lists inside ViewModel
        private async Task PopulateSelectListsAsync(PurchaseFormViewModel model)
        {
            var suppliers = await _context.Suppliers.OrderBy(s => s.SupplierName).ToListAsync();

            model.Suppliers = suppliers.Select(s => new SelectListItem
            {
                Value = s.SupplierID.ToString(),
                Text = s.SupplierName,
                Selected = s.SupplierID == model.SupplierID
            }).ToList();

            if (model.SupplierID > 0)
            {
                var supplierProducts = await _context.SupplierProducts
                    .Include(sp => sp.Product)
                    .Where(sp => sp.SupplierID == model.SupplierID && sp.Product != null)
                    .OrderBy(sp => sp.Product!.ProductName)
                    .ToListAsync();

                model.AvailableProducts = supplierProducts.Select(sp => new SupplierProductOptionViewModel
                {
                    ProductID = sp.ProductID,
                    ProductName = sp.Product!.ProductName,
                    SKU = sp.Product!.SKU,
                    ContractPrice = sp.ContractPrice
                }).ToList();

                // If editing an existing purchase, make sure any already-selected product is included in AvailableProducts
                if (model.Items != null && model.Items.Any())
                {
                    var existingProductIds = model.Items.Select(i => i.ProductID).Where(id => id > 0).Distinct().ToList();
                    foreach (var prodId in existingProductIds)
                    {
                        if (!model.AvailableProducts.Any(ap => ap.ProductID == prodId))
                        {
                            var prod = await _context.Products.FindAsync(prodId);
                            if (prod != null)
                            {
                                var itemCost = model.Items.FirstOrDefault(i => i.ProductID == prodId)?.UnitCost ?? prod.UnitPrice;
                                model.AvailableProducts.Add(new SupplierProductOptionViewModel
                                {
                                    ProductID = prod.ProductID,
                                    ProductName = prod.ProductName,
                                    SKU = prod.SKU,
                                    ContractPrice = itemCost
                                });
                            }
                        }
                    }
                }

                model.Products = model.AvailableProducts.Select(p => new SelectListItem
                {
                    Value = p.ProductID.ToString(),
                    Text = $"{p.ProductName} (SKU: {p.SKU}) - ${p.ContractPrice:N2}"
                }).ToList();
            }
            else
            {
                model.AvailableProducts = new List<SupplierProductOptionViewModel>();
                model.Products = new List<SelectListItem>();
            }
        }
    }
}