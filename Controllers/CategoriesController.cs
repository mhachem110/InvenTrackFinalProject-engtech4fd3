using InvenTrack.Data;
using InvenTrack.Models;
using InvenTrack.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvenTrack.Controllers
{
    [Authorize(Roles =
        AppRoles.Admin + "," +
        AppRoles.RegionalManager + "," +
        AppRoles.Manager + "," +
        AppRoles.Supervisor + "," +
        AppRoles.Employee)]
    public class CategoriesController : Controller
    {
        private readonly InvenTrackContext _context;

        public CategoriesController(InvenTrackContext context)
        {
            _context = context;
        }

        private static string Clean(string? s) => (s ?? string.Empty).Trim();

        private async Task<bool> CategoryNameExistsAsync(string name, int? excludeId = null)
        {
            name = Clean(name);
            if (string.IsNullOrWhiteSpace(name)) return false;

            return await _context.Categories.AnyAsync(c =>
                c.Name == name && (!excludeId.HasValue || c.ID != excludeId.Value));
        }

        public async Task<IActionResult> Index(string? searchString, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;

            var allowedPageSizes = new[] { 10, 25, 50 };
            if (!allowedPageSizes.Contains(pageSize))
                pageSize = 10;

            IQueryable<Category> query = _context.Categories
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim();

                query = query.Where(c =>
                    c.Name.Contains(searchString) ||
                    (c.Description != null && c.Description.Contains(searchString)));
            }

            var pagedCategories = await PaginatedList<Category>.CreateAsync(
                query.OrderBy(c => c.Name),
                page,
                pageSize);

            if (pagedCategories.TotalPages > 0 && page > pagedCategories.TotalPages)
            {
                page = pagedCategories.TotalPages;

                pagedCategories = await PaginatedList<Category>.CreateAsync(
                    query.OrderBy(c => c.Name),
                    page,
                    pageSize);
            }

            ViewData["CurrentFilter"] = searchString ?? string.Empty;
            ViewData["CurrentPageSize"] = pageSize;

            return View(pagedCategories);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ID == id);

            if (category == null) return NotFound();

            return View(category);
        }

        [Authorize(Roles = AppRoles.Admin + "," + AppRoles.RegionalManager)]
        public IActionResult Create()
        {
            return View();
        }

        [Authorize(Roles = AppRoles.Admin + "," + AppRoles.RegionalManager)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description")] Category category)
        {
            category.Name = Clean(category.Name);
            category.Description = Clean(category.Description);

            if (await CategoryNameExistsAsync(category.Name))
            {
                ModelState.AddModelError(nameof(Category.Name), "Category name must be unique. This category already exists.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(category);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError(string.Empty, "Unable to save category. Please try again.");
                }
            }

            return View(category);
        }

        [Authorize(Roles = AppRoles.Admin + "," + AppRoles.RegionalManager)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            return View(category);
        }

        [Authorize(Roles = AppRoles.Admin + "," + AppRoles.RegionalManager)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id)
        {
            var categoryToUpdate = await _context.Categories.FirstOrDefaultAsync(c => c.ID == id);
            if (categoryToUpdate == null) return NotFound();

            if (await TryUpdateModelAsync(categoryToUpdate, "",
                c => c.Name, c => c.Description))
            {
                categoryToUpdate.Name = Clean(categoryToUpdate.Name);
                categoryToUpdate.Description = Clean(categoryToUpdate.Description);

                if (await CategoryNameExistsAsync(categoryToUpdate.Name, excludeId: id))
                {
                    ModelState.AddModelError(nameof(Category.Name), "Category name must be unique. This category already exists.");
                    return View(categoryToUpdate);
                }

                try
                {
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Categories.AnyAsync(e => e.ID == id))
                        return NotFound();

                    throw;
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError(string.Empty, "Unable to save changes. Please try again.");
                }
            }

            return View(categoryToUpdate);
        }

        [Authorize(Roles = AppRoles.Admin + "," + AppRoles.RegionalManager)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ID == id);

            if (category == null) return NotFound();

            return View(category);
        }

        [Authorize(Roles = AppRoles.Admin + "," + AppRoles.RegionalManager)]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return RedirectToAction(nameof(Index));

            try
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty,
                    "Unable to delete this category because it is being used by one or more inventory items.");

                return View("Delete", category);
            }
        }
    }
}