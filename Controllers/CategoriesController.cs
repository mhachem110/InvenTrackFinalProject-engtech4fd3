using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvenTrack.Data;
using InvenTrack.Models;

namespace InvenTrack.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly InvenTrackContext _context;

        public CategoriesController(InvenTrackContext context)
        {
            _context = context;
        }

        // ---------------------------
        // Helpers
        // ---------------------------

        private static string Clean(string? s) => (s ?? string.Empty).Trim();

        private async Task<bool> CategoryNameExistsAsync(string name, int? excludeId = null)
        {
            name = Clean(name);
            if (string.IsNullOrWhiteSpace(name)) return false;

            return await _context.Categories.AnyAsync(c =>
                c.Name == name && (!excludeId.HasValue || c.ID != excludeId.Value));
        }

        // GET: Categories
        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(categories);
        }

        // GET: Categories/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ID == id);

            if (category == null) return NotFound();

            return View(category);
        }

        // GET: Categories/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Categories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description")] Category category)
        {
            // Normalize inputs
            category.Name = Clean(category.Name);
            category.Description = Clean(category.Description);

            // Friendly uniqueness check
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

        // GET: Categories/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            return View(category);
        }

        // POST: Categories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id)
        {
            var categoryToUpdate = await _context.Categories.FirstOrDefaultAsync(c => c.ID == id);
            if (categoryToUpdate == null) return NotFound();

            // Update only allowed fields (prevents overposting)
            if (await TryUpdateModelAsync(categoryToUpdate, "",
                c => c.Name, c => c.Description))
            {
                // Normalize
                categoryToUpdate.Name = Clean(categoryToUpdate.Name);
                categoryToUpdate.Description = Clean(categoryToUpdate.Description);

                // Friendly uniqueness check (exclude current category)
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

        // GET: Categories/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ID == id);

            if (category == null) return NotFound();

            return View(category);
        }

        // POST: Categories/Delete/5
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
                // Common: FK constraint if InventoryItems reference this category
                ModelState.AddModelError(string.Empty,
                    "Unable to delete this category because it is being used by one or more inventory items.");

                return View("Delete", category);
            }
        }
    }
}