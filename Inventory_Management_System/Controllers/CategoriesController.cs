using Microsoft.AspNetCore.Authorization;

namespace Inventory_Management_System.Controllers
{
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.Auditor}")]
    public class CategoriesController : Controller
    {
        private readonly ICategoryService _categoryService;

        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        // GET: /Categories/Index
        [HttpGet]
        public async Task<IActionResult> Index(CategoryFilterViewModel filter)
        {
            var pagedResult = await _categoryService.GetCategoriesAsync(filter);
            ViewBag.Filter = filter;
            return View(pagedResult);
        }

        // GET: /Categories/Details/{id}
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (id <= 0) return NotFound();

            var viewModel = await _categoryService.GetCategoryDetailsAsync(id);
            if (viewModel == null) return NotFound();

            return View(viewModel);
        }

        // GET: /Categories/Create
        [HttpGet]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager}")]
        public IActionResult Create()
        {
            return View(new CategoryFormViewModel());
        }

        // POST: /Categories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager}")]
        public async Task<IActionResult> Create(CategoryFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _categoryService.CreateCategoryAsync(model);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // GET: /Categories/Edit/{id}
        [HttpGet]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager}")]
        public async Task<IActionResult> Edit(int id)
        {
            if (id <= 0) return NotFound();

            var model = await _categoryService.GetCategoryForEditAsync(id);
            if (model == null) return NotFound();

            return View(model);
        }

        // POST: /Categories/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager}")]
        public async Task<IActionResult> Edit(int id, CategoryFormViewModel model)
        {
            if (id != model.CategoryID)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _categoryService.UpdateCategoryAsync(model);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // GET: /Categories/Delete/{id}
        [HttpGet]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0) return NotFound();

            var model = await _categoryService.GetCategoryForDeleteAsync(id);
            if (model == null) return NotFound();

            return View(model);
        }

        // POST: /Categories/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager}")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (id <= 0) return BadRequest();

            var result = await _categoryService.DeleteCategoryAsync(id);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction(nameof(Delete), new { id });
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // Remote validation: /Categories/CheckNameUnique
        [HttpGet]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager}")]
        public async Task<IActionResult> CheckNameUnique(string categoryName, int? categoryId)
        {
            var isUnique = await _categoryService.IsCategoryNameUniqueAsync(categoryName, categoryId);
            return Json(isUnique ? (object)true : $"A category named '{categoryName}' already exists.");
        }
    }
}
