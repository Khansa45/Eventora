using Microsoft.AspNetCore.Mvc;

namespace Eventora_Project.Controllers
{
    public class StudentController : Controller
    {
        public IActionResult Dashboard()
        {
            return View("StdDashboard");
        }

        public IActionResult MyRegistrations() => View("StdDashboard");
    }
}