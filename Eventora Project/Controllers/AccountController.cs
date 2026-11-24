using Eventora_Project.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Eventora_Project.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
        }

        [HttpGet]
        public IActionResult Login(string? role)
        {
            SetLoginContext(role);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe, string? role)
        {
            SetLoginContext(role);
            ViewBag.Email = email;

            var result = await _signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    await _signInManager.SignOutAsync();
                    TempData["Error"] = "We could not complete sign in. Please try again.";
                    return View();
                }

                var roles = await _userManager.GetRolesAsync(user);
                var selectedRole = NormalizeRoleHint(role);

                if (selectedRole != null && !roles.Contains(selectedRole, StringComparer.Ordinal))
                {
                    await _signInManager.SignOutAsync();
                    TempData["Error"] = "These credentials do not match the selected portal.";
                    return View();
                }

                return RedirectByRole(selectedRole ?? roles.FirstOrDefault() ?? "Student");
            }

            TempData["Error"] = result.IsLockedOut
                ? "Your account is locked. Please try again later."
                : "Invalid email or password.";
            return View();
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpGet]
        public IActionResult AccessDenied() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var fullName = model.FullName.Trim();
            var email = model.Email.Trim();
            var studentId = model.StudentId.Trim();

            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(studentId))
            {
                TempData["Error"] = "Full name, email address and student ID are required.";
                return View(model);
            }

            const string studentRole = "Student";
            if (!await _roleManager.RoleExistsAsync(studentRole))
            {
                var roleResult = await _roleManager.CreateAsync(new IdentityRole(studentRole));
                if (!roleResult.Succeeded)
                {
                    TempData["Error"] = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                    return View(model);
                }
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                StudentIdNumber = studentId
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, studentRole);
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectByRole(studentRole);
            }

            TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        private void SetLoginContext(string? role)
        {
            var normalizedRole = NormalizeRoleHint(role);
            ViewBag.RoleHint = normalizedRole;
            ViewBag.RoleLabel = normalizedRole switch
            {
                "Admin" => "Admin Portal",
                "SocietyHead" => "Society Head Portal",
                "Student" => "Student Portal",
                _ => "Eventora Account"
            };
        }

        private static string? NormalizeRoleHint(string? role) => role switch
        {
            "Admin" => "Admin",
            "SocietyHead" => "SocietyHead",
            "Student" => "Student",
            _ => null
        };

        private IActionResult RedirectByRole(string role) => role switch
        {
            "Admin" => RedirectToAction("Dashboard", "Admin"),
            "SocietyHead" => RedirectToAction("Dashboard", "SocietyHead"),
            _ => RedirectToAction("Dashboard", "Student")
        };
    }
}