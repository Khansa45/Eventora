using Eventora_Project.Models;
using Eventora_Project.Repositories.Interfaces;
using Eventora_Project.Repositories.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Eventora_Project.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly IEventRepository _events;
        private readonly IRegistrationRepository _registrations;
        private readonly ICategoryRepository _categories;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentController(
            IEventRepository events,
            IRegistrationRepository registrations,
            ICategoryRepository categories,
            UserManager<ApplicationUser> userManager)
        {
            _events = events;
            _registrations = registrations;
            _categories = categories;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard(string? search = null, int categoryId = 0)
        {
            var approvedEvents = await _events.GetApprovedAsync(search, categoryId);

            var userId = _userManager.GetUserId(User);
            var registrations = string.IsNullOrEmpty(userId)
                ? new List<Registration>()
                : await _registrations.GetByStudentWithEventAsync(userId);

            ViewBag.MyRegistrations = registrations;
            ViewBag.RegisteredEventIds = registrations.Select(r => r.EventId).ToList();
            ViewBag.Categories = await _categories.GetOrderedByNameAsync();

            return View("StdDashboard", approvedEvents);
        }

        public async Task<IActionResult> MyRegistrations() => await Dashboard();

        [HttpGet]
        public async Task<IActionResult> EventDetailsPartial(int id)
        {
            var approvedEvent = await _events.GetApprovedByIdAsync(id);
            if (approvedEvent == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            ViewBag.IsRegistered = !string.IsNullOrEmpty(userId)
                && await _registrations.ExistsForStudentAsync(id, userId);

            return PartialView("_StudentEventDetails", approvedEvent);
        }

        [HttpGet]
        public async Task<IActionResult> EventListPartial(string? search = null, int categoryId = 0)
        {
            var approvedEvents = await _events.GetApprovedAsync(search, categoryId);

            var userId = _userManager.GetUserId(User);
            var registrations = string.IsNullOrEmpty(userId)
                ? new List<Registration>()
                : await _registrations.GetByStudentWithEventAsync(userId);

            ViewBag.RegisteredEventIds = registrations.Select(r => r.EventId).ToList();
            return PartialView("_StudentEventList", approvedEvents);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterEvent(int eventId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return AuthenticationRequiredResult();

            if (await _registrations.ExistsForStudentAsync(eventId, userId))
            {
                const string message = "You are already registered for this event.";
                return RegistrationResult(false, message, eventId);
            }

            try
            {
                var outcome = await _registrations.TryRegisterWithSeatDecrementAsync(
                    eventId,
                    userId,
                    DateTime.Now);

                var message = GetRegistrationMessage(outcome);
                return RegistrationResult(outcome == RegistrationOutcome.Success, message, eventId);
            }
            catch (Exception)
            {
                return RegistrationResult(false, "Registration could not be completed. Please try again.", eventId);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRegistration(int registrationId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Challenge();

            var registration = await _registrations.GetByIdAndStudentAsync(registrationId, userId);

            if (registration == null)
            {
                TempData["Error"] = "Registration not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            try
            {
                var outcome = await _registrations.CancelOwnedWithSeatReturnAsync(registrationId, userId);

                switch (outcome)
                {
                    case CancellationOutcome.Success:
                        TempData["Success"] = "Registration cancelled.";
                        break;
                    case CancellationOutcome.SeatRestoreFailed:
                        TempData["Error"] = "Registration could not be cancelled because the event is at full capacity.";
                        break;
                    case CancellationOutcome.RegistrationNotFound:
                        TempData["Error"] = "Registration not found.";
                        break;
                }
            }
            catch (Exception)
            {
                TempData["Error"] = "Registration could not be cancelled. Please try again.";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        private bool IsAjaxRequest()
        {
            return Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        }

        private IActionResult AuthenticationRequiredResult()
        {
            if (!IsAjaxRequest())
                return Challenge();

            return new JsonResult(new { success = false, message = "Authentication required." })
            {
                StatusCode = 401
            };
        }

        private IActionResult RegistrationResult(bool success, string message, int eventId)
        {
            if (!IsAjaxRequest())
            {
                if (success)
                    TempData["Success"] = message;
                else
                    TempData["Error"] = message;

                return RedirectToAction(nameof(Dashboard));
            }

            return Json(new { success, message, eventId });
        }

        private static string GetRegistrationMessage(RegistrationOutcome outcome)
        {
            return outcome switch
            {
                RegistrationOutcome.Success => "You are registered for the event!",
                RegistrationOutcome.Duplicate => "You are already registered for this event.",
                RegistrationOutcome.NotFound => "Event not found.",
                RegistrationOutcome.NotApproved => "This event is not open for registration.",
                RegistrationOutcome.NoSeats => "No seats are available for this event.",
                _ => "Registration could not be completed. Please try again."
            };
        }
    }
}
