using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;
using System.Linq;
using TutionCenter.Data;
using TutionCenter.Models;

namespace TutionCenter.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            ViewBag.StudentCount = _context.Users.Count(x => x.Role == "Student");
            ViewBag.TeacherCount = _context.Users.Count(x => x.Role == "Teacher");
            ViewBag.AdminCount = _context.Users.Count(x => x.Role == "Admin");
            ViewBag.SubjectCount = _context.Subjects.Count(x => x.IsActive);
            ViewBag.ClassCount = _context.TuitionClasses.Count(x => x.IsActive);
            ViewBag.ClassCount=_context.TuitionClasses.Count( x => x.IsActive);
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Welcome()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
