using Eventora_Project.Models;
using Eventora_Project.Repositories.Interfaces;
using Eventora_Project.Repositories.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Eventora_Project.Controllers
{
    [Authorize(Roles = "SocietyHead")]
    public class SocietyHeadController : Controller
    {
        private readonly IEventRepository _events;
        private readonly ICategoryRepository _categories;
        private readonly UserManager<ApplicationUser> _userManager;

        public SocietyHeadController(
            IEventRepository events,
            ICategoryRepository categories,
            UserManager<ApplicationUser> userManager)
        {
            _events = events;
            _categories = categories;
            _userManager = userManager;
        }

        // READ:
        public async Task<IActionResult> Dashboard()
        {
            return await BuildDashboardAsync();
        }

        private async Task<IActionResult> BuildDashboardAsync()
        {
            var userId = _userManager.GetUserId(User)!;
            var myEvents = await _events.GetForSocietyHeadAsync(userId);

            ViewBag.Categories = await _categories.GetAllAsync();
            return View("HeadDashboard", myEvents);
        }

        private async Task<IActionResult> InvalidDashboardAsync(string message)
        {
            TempData["Error"] = message;
            return await BuildDashboardAsync();
        }

        // CREATE:
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEvent(Event model)
        {
            if (model == null)
                return await InvalidDashboardAsync("Event details could not be read.");

            model.TotalCapacity = model.AvailableSeats;
            ModelState.Remove(nameof(Event.TotalCapacity));

            if (model.EventDate == default)
                ModelState.AddModelError("EventDate", "Event date is required.");

            if (!await _categories.ExistsAsync(model.CategoryId))
                ModelState.AddModelError("CategoryId", "Select a valid category.");

            if (model.AvailableSeats < 1 || model.AvailableSeats > 5000)
                ModelState.AddModelError("AvailableSeats", "Capacity must be between 1 and 5000.");

            if (!ModelState.IsValid)
                return await InvalidDashboardAsync("Please correct the highlighted fields and try again.");

            var userId = _userManager.GetUserId(User);
            model.Id = 0;
            model.SocietyHeadId = userId!;
            model.Status = "Pending";
            model.TotalCapacity = model.AvailableSeats;
            model.AvailableSeats = model.TotalCapacity;

            await _events.AddAsync(model);

            TempData["Success"] = "Event submitted for Admin approval!";
            return RedirectToAction(nameof(Dashboard));
        }

        // UPDATE: Event edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEvent(Event model)
        {
            if (model == null)
                return await InvalidDashboardAsync("Event details could not be read.");

            if (!ModelState.IsValid)
                return await InvalidDashboardAsync("Please correct the highlighted fields and try again.");

            if (model.EventDate == default)
                ModelState.AddModelError("EventDate", "Event date is required.");

            if (!await _categories.ExistsAsync(model.CategoryId))
                ModelState.AddModelError("CategoryId", "Select a valid category.");

            if (model.TotalCapacity < 1 || model.TotalCapacity > 5000)
                ModelState.AddModelError("TotalCapacity", "Capacity must be between 1 and 5000.");

            if (!ModelState.IsValid)
                return await InvalidDashboardAsync("Please correct the highlighted fields and try again.");

            var userId = _userManager.GetUserId(User)!;
            var ownedEvent = await _events.GetOwnedByIdAsync(model.Id, userId);

            if (ownedEvent == null)
                return await InvalidDashboardAsync("Event not found or not owned by you.");

            try
            {
                var result = await _events.UpdateOwnedEventAndRecalculateSeatsAsync(
                    model,
                    model.TotalCapacity,
                    userId);

                if (result.Outcome == EventEditOutcome.Updated)
                {
                    TempData["Success"] = "Event updated successfully!";
                    return RedirectToAction(nameof(Dashboard));
                }

                if (result.Outcome == EventEditOutcome.CapacityBelowRegistrations)
                {
                    ModelState.AddModelError(
                        "TotalCapacity",
                        $"Capacity cannot be lower than {result.ConfirmedRegistrations} confirmed registration(s).");
                    return await InvalidDashboardAsync("Capacity cannot be lower than the number of confirmed registrations.");
                }

                TempData["Error"] = "Event not found, not owned by you, or capacity is below existing registrations.";
                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception)
            {
                TempData["Error"] = "Event could not be updated. Please try again.";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        // DELETE: Event delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var result = await _events.DeleteOwnedWithRegistrationsAsync(id, userId);

            if (result.Deleted)
            {
                TempData["Success"] = "Event deleted.";
            }
            else
            {
                TempData["Error"] = "Event not found or not owned by you.";
            }

            return RedirectToAction(nameof(Dashboard));
        }
    }
}
