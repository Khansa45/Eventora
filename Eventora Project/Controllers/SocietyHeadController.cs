using Eventora_Project.Data;
using Eventora_Project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eventora_Project.Controllers
{
    [Authorize(Roles = "SocietyHead")]
    public class SocietyHeadController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SocietyHeadController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // READ:
        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User);
            var myEvents = await _context.Events
                .Include(e => e.Category)
                .Include(e => e.Registrations)
                .Where(e => e.SocietyHeadId == userId)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();

            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View("HeadDashboard", myEvents);
        }

        // CREATE:
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEvent(Event model)
        {
            var userId = _userManager.GetUserId(User);
            model.SocietyHeadId = userId!;
            model.Status = "Pending"; // Admin approval for pending 

            _context.Events.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Event submitted for Admin approval!";
            return RedirectToAction(nameof(Dashboard));
        }

        // UPDATE: Event edit 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEvent(Event model)
        {
            var existing = await _context.Events.FindAsync(model.Id);
            if (existing == null || existing.SocietyHeadId != _userManager.GetUserId(User))
                return NotFound();

            existing.Title = model.Title;
            existing.Description = model.Description;
            existing.EventDate = model.EventDate;
            existing.Venue = model.Venue;
            existing.City = model.City;
            existing.AvailableSeats = model.AvailableSeats;
            existing.CategoryId = model.CategoryId;
            existing.Status = "Pending"; // Approval after edit

            await _context.SaveChangesAsync();
            TempData["Success"] = "Event updated successfully!";
            return RedirectToAction(nameof(Dashboard));
        }

        // DELETE: Event delete 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var ev = await _context.Events.Include(e => e.Registrations).FirstOrDefaultAsync(e => e.Id == id);
            if (ev != null && ev.SocietyHeadId == _userManager.GetUserId(User))
            {
                _context.Registrations.RemoveRange(ev.Registrations);
                _context.Events.Remove(ev);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Event deleted.";
            }
            return RedirectToAction(nameof(Dashboard));
        }
    }
}