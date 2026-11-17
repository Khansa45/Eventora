using Eventora_Project.Data;
using Eventora_Project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eventora_Project.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // READ: All events & stats
        public async Task<IActionResult> Dashboard()
        {
            var allEvents = await _context.Events
                .Include(e => e.Category)
                .Include(e => e.SocietyHead)
                .Include(e => e.Registrations)
                .OrderByDescending(e => e.Id)
                .ToListAsync();

            ViewBag.TotalUsers = await _userManager.Users.CountAsync();
            ViewBag.UsersList = await _userManager.Users.ToListAsync();
            return View("AdminDashboard", allEvents);
        }

        // UPDATE: Approve Event
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveEvent(int id)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev != null)
            {
                ev.Status = "Approved";
                await _context.SaveChangesAsync();
                TempData["Success"] = $"'{ev.Title}' approved!";
            }
            return RedirectToAction(nameof(Dashboard));
        }

        // UPDATE: Reject Event
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectEvent(int id)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev != null)
            {
                ev.Status = "Rejected";
                await _context.SaveChangesAsync();
                TempData["Success"] = $"'{ev.Title}' rejected.";
            }
            return RedirectToAction(nameof(Dashboard));
        }

        // DELETE: Delete any event
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var ev = await _context.Events.Include(e => e.Registrations).FirstOrDefaultAsync(e => e.Id == id);
            if (ev != null)
            {
                _context.Registrations.RemoveRange(ev.Registrations);
                _context.Events.Remove(ev);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Dashboard));
        }
    }
}