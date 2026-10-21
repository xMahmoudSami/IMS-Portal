using Microsoft.AspNetCore.Authorization;

namespace Inventory_Management_System.Controllers
{
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.ProcurementSpecialist}")]
    public class SupplierProductsController : Controller
    {
        private readonly ISupplierProductService _supplierProductService;
        private readonly ISupplierService _supplierService;
        private readonly IProductService _productService;

        public SupplierProductsController(
            ISupplierProductService supplierProductService,
            ISupplierService supplierService,
            IProductService productService)
        {
            _supplierProductService = supplierProductService;
            _supplierService = supplierService;
            _productService = productService;
        }

        // GET: /SupplierProducts/Index
        [HttpGet]
        public async Task<IActionResult> Index(SupplierProductFilterViewModel filter)
        {
            filter.Suppliers = await _supplierService.GetSupplierSelectListAsync();
            filter.Products = await _productService.GetProductSelectListAsync();

            var pagedResult = await _supplierProductService.GetSupplierProductsAsync(filter);
            ViewBag.Filter = filter;
            return View(pagedResult);
        }

        // GET: /SupplierProducts/Create
        [HttpGet]
        public async Task<IActionResult> Create(int? supplierId, int? productId)
        {
            var model = new SupplierProductFormViewModel
            {
                SupplierID = supplierId ?? 0,
                ProductID = productId ?? 0,
                LeadTimeDays = 7, // sensible default
                Suppliers = await _supplierService.GetSupplierSelectListAsync(),
                Products = await _productService.GetProductSelectListAsync()
            };
            return View(model);
        }

        // POST: /SupplierProducts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SupplierProductFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Suppliers = await _supplierService.GetSupplierSelectListAsync();
                model.Products = await _productService.GetProductSelectListAsync();
                return View(model);
            }

            var result = await _supplierProductService.CreateSupplierProductAsync(model);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                model.Suppliers = await _supplierService.GetSupplierSelectListAsync();
                model.Products = await _productService.GetProductSelectListAsync();
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // GET: /SupplierProducts/Edit/{id}
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (id <= 0) return NotFound();

            var model = await _supplierProductService.GetSupplierProductForEditAsync(id);
            if (model == null) return NotFound();

            model.Suppliers = await _supplierService.GetSupplierSelectListAsync();
            model.Products = await _productService.GetProductSelectListAsync();
            return View(model);
        }

        // POST: /SupplierProducts/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SupplierProductFormViewModel model)
        {
            if (id != model.SupplierProductID)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                model.Suppliers = await _supplierService.GetSupplierSelectListAsync();
                model.Products = await _productService.GetProductSelectListAsync();
                return View(model);
            }

            var result = await _supplierProductService.UpdateSupplierProductAsync(model);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                model.Suppliers = await _supplierService.GetSupplierSelectListAsync();
                model.Products = await _productService.GetProductSelectListAsync();
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // GET: /SupplierProducts/Delete/{id}
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0) return NotFound();

            var model = await _supplierProductService.GetSupplierProductForDeleteAsync(id);
            if (model == null) return NotFound();

            return View(model);
        }

        // POST: /SupplierProducts/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (id <= 0) return BadRequest();

            var result = await _supplierProductService.DeleteSupplierProductAsync(id);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction(nameof(Delete), new { id });
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }
    }
}
