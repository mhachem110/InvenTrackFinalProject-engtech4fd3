using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using InvenTrack.Data;
using InvenTrack.Models;

namespace InvenTrackFinalProject.Controllers
{
    public class StorageLocationsController : Controller
    {
        private readonly InvenTrackContext _context;

        public StorageLocationsController(InvenTrackContext context)
        {
            _context = context;
        }

        // GET: StorageLocations
        public async Task<IActionResult> Index()
        {
              return View(await _context.StorageLocations.ToListAsync());
        }

        // GET: StorageLocations/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.StorageLocations == null)
            {
                return NotFound();
            }

            var storageLocation = await _context.StorageLocations
                .FirstOrDefaultAsync(m => m.ID == id);
            if (storageLocation == null)
            {
                return NotFound();
            }

            return View(storageLocation);
        }

        // GET: StorageLocations/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: StorageLocations/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,Name,Building,Room")] StorageLocation storageLocation)
        {
            if (ModelState.IsValid)
            {
                _context.Add(storageLocation);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(storageLocation);
        }

        // GET: StorageLocations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.StorageLocations == null)
            {
                return NotFound();
            }

            var storageLocation = await _context.StorageLocations.FindAsync(id);
            if (storageLocation == null)
            {
                return NotFound();
            }
            return View(storageLocation);
        }

        // POST: StorageLocations/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,Name,Building,Room")] StorageLocation storageLocation)
        {
            if (id != storageLocation.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(storageLocation);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StorageLocationExists(storageLocation.ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(storageLocation);
        }

        // GET: StorageLocations/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.StorageLocations == null)
            {
                return NotFound();
            }

            var storageLocation = await _context.StorageLocations
                .FirstOrDefaultAsync(m => m.ID == id);
            if (storageLocation == null)
            {
                return NotFound();
            }

            return View(storageLocation);
        }

        // POST: StorageLocations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.StorageLocations == null)
            {
                return Problem("Entity set 'InvenTrackContext.StorageLocations'  is null.");
            }
            var storageLocation = await _context.StorageLocations.FindAsync(id);
            if (storageLocation != null)
            {
                _context.StorageLocations.Remove(storageLocation);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool StorageLocationExists(int id)
        {
          return _context.StorageLocations.Any(e => e.ID == id);
        }
    }
}
