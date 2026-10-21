using Microsoft.AspNetCore.Authorization;

namespace Inventory_Management_System.Controllers
{
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.Cashier},{AppRoles.ProcurementSpecialist},{AppRoles.Auditor}")]
    public class ProductsController : Controller
    {
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;

        public ProductsController(IProductService productService, ICategoryService categoryService)
        {
            _productService = productService;
            _categoryService = categoryService;
        }

        // GET: /Products/Index
        [HttpGet]
        public async Task<IActionResult> Index(ProductFilterViewModel filter)
        {
            filter.Categories = await _categoryService.GetCategorySelectListAsync();
            var pagedResult = await _productService.GetProductsAsync(filter);
            ViewBag.Filter = filter;
            return View(pagedResult);
        }

        // GET: /Products/Details/{id}
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (id <= 0) return NotFound();

            var viewModel = await _productService.GetProductDetailsAsync(id);
            if (viewModel == null) return NotFound();

            return View(viewModel);
        }

        // GET: /Products/Create
        [HttpGet]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager}")]
        public async Task<IActionResult> Create(int? categoryId)
        {
            var model = new ProductFormViewModel
            {
                CategoryID = categoryId ?? 0,
                Categories = await _categoryService.GetCategorySelectListAsync()
            };
            return View(model);
        }

        // POST: /Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager}")]
        public async Task<IActionResult> Create(ProductFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Categories = await _categoryService.GetCategorySelectListAsync();
                return View(model);
            }

            var result = await _productService.CreateProductAsync(model);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                model.Categories = await _categoryService.GetCategorySelectListAsync();
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // GET: /Products/Edit/{id}
        [HttpGet]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager}")]
        public async Task<IActionResult> Edit(int id)
        {
            if (id <= 0) return NotFound();

            var model = await _productService.GetProductForEditAsync(id);
            if (model == null) return NotFound();

            model.Categories = await _categoryService.GetCategorySelectListAsync();
            return View(model);
        }

        // POST: /Products/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager}")]
        public async Task<IActionResult> Edit(int id, ProductFormViewModel model)
        {
            if (id != model.ProductID)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                model.Categories = await _categoryService.GetCategorySelectListAsync();
                return View(model);
            }

            var result = await _productService.UpdateProductAsync(model);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                model.Categories = await _categoryService.GetCategorySelectListAsync();
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // GET: /Products/Delete/{id}
        [HttpGet]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0) return NotFound();

            var model = await _productService.GetProductForDeleteAsync(id);
            if (model == null) return NotFound();

            return View(model);
        }

        // POST: /Products/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager}")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (id <= 0) return BadRequest();

            var result = await _productService.DeleteProductAsync(id);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction(nameof(Delete), new { id });
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // Remote validation: /Products/CheckSkuUnique
        [HttpGet]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager}")]
        public async Task<IActionResult> CheckSkuUnique(string sku, int? productId)
        {
            var isUnique = await _productService.IsSkuUniqueAsync(sku, productId);
            return Json(isUnique ? (object)true : $"The SKU '{sku}' is already in use by another product.");
        }
    }
}
