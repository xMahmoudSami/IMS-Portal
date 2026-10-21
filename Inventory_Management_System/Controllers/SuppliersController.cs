using Microsoft.AspNetCore.Authorization;

namespace Inventory_Management_System.Controllers
{
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.ProcurementSpecialist},{AppRoles.Auditor}")]
    public class SuppliersController : Controller
    {
        private readonly ISupplierService _supplierService;

        public SuppliersController(ISupplierService supplierService)
        {
            _supplierService = supplierService;
        }

        // GET: /Suppliers/Index
        [HttpGet]
        public async Task<IActionResult> Index(SupplierFilterViewModel filter)
        {
            var pagedResult = await _supplierService.GetSuppliersAsync(filter);
            ViewBag.Filter = filter;
            return View(pagedResult);
        }

        // GET: /Suppliers/Details/{id}
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (id <= 0) return NotFound();

            var viewModel = await _supplierService.GetSupplierDetailsAsync(id);
            if (viewModel == null) return NotFound();

            return View(viewModel);
        }

        // GET: /Suppliers/Create
        [HttpGet]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.ProcurementSpecialist}")]
        public IActionResult Create()
        {
            return View(new SupplierFormViewModel());
        }

        // POST: /Suppliers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.ProcurementSpecialist}")]
        public async Task<IActionResult> Create(SupplierFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _supplierService.CreateSupplierAsync(model);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // GET: /Suppliers/Edit/{id}
        [HttpGet]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.ProcurementSpecialist}")]
        public async Task<IActionResult> Edit(int id)
        {
            if (id <= 0) return NotFound();

            var model = await _supplierService.GetSupplierForEditAsync(id);
            if (model == null) return NotFound();

            return View(model);
        }

        // POST: /Suppliers/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.ProcurementSpecialist}")]
        public async Task<IActionResult> Edit(int id, SupplierFormViewModel model)
        {
            if (id != model.SupplierID)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _supplierService.UpdateSupplierAsync(model);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // GET: /Suppliers/Delete/{id}
        [HttpGet]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.ProcurementSpecialist}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0) return NotFound();

            var model = await _supplierService.GetSupplierForDeleteAsync(id);
            if (model == null) return NotFound();

            return View(model);
        }

        // POST: /Suppliers/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.WarehouseManager},{AppRoles.ProcurementSpecialist}")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (id <= 0) return BadRequest();

            var result = await _supplierService.DeleteSupplierAsync(id);
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
