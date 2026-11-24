using Eventora_Project.Models;
using Eventora_Project.Repositories.Interfaces;
using Eventora_Project.Repositories.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventora_Project.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IEventRepository _events;

        public AdminController(IEventRepository events)
        {
            _events = events;
        }

        // READ: All events & stats
        public async Task<IActionResult> Dashboard()
        {
            var allEvents = await _events.GetAllForAdminAsync();

            ViewBag.TotalEvents = allEvents.Count;
            ViewBag.PendingCount = allEvents.Count(e => e.Status == "Pending");
            ViewBag.ApprovedCount = allEvents.Count(e => e.Status == "Approved");
            ViewBag.RejectedCount = allEvents.Count(e => e.Status == "Rejected");
            return View("AdminDashboard", allEvents);
        }

        // UPDATE: Approve Event
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveEvent(int id)
        {
            var result = await _events.UpdateStatusAsync(id, "Pending", "Approved");
            if (result.Kind == EventStatusUpdateResultKind.NotFound)
            {
                TempData["Error"] = "Event not found.";
            }
            else if (result.Kind == EventStatusUpdateResultKind.Conflict)
            {
                TempData["Error"] = $"'{result.Title}' is no longer pending.";
            }
            else
            {
                TempData["Success"] = $"'{result.Title}' approved!";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        // UPDATE: Reject Event
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectEvent(int id)
        {
            var result = await _events.UpdateStatusAsync(id, "Pending", "Rejected");
            if (result.Kind == EventStatusUpdateResultKind.NotFound)
            {
                TempData["Error"] = "Event not found.";
            }
            else if (result.Kind == EventStatusUpdateResultKind.Conflict)
            {
                TempData["Error"] = $"'{result.Title}' is no longer pending.";
            }
            else
            {
                TempData["Success"] = $"'{result.Title}' rejected.";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        // DELETE: Delete any event
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var result = await _events.DeleteWithRegistrationsAsync(id);
            if (!result.Deleted)
            {
                TempData["Error"] = "Event not found.";
            }
            else
            {
                TempData["Success"] = $"'{result.Title}' deleted.";
            }

            return RedirectToAction(nameof(Dashboard));
        }
    }
}
