using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutionCenter.Data;
using TutionCenter.Models;
using TutionCenter.Services;

namespace TutionCenter.Controllers
{
    [Authorize(Roles = "Admin")]
    public class TeacherController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TeacherController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var teachers = _context.Users
                                   .Where(x => x.Role == "Teacher")
                                   .ToList();

            return View(teachers);
        }

        public IActionResult Create()
        {
            return View();
        }

        public IActionResult Edit(int id)
        {
            var teacher = _context.Users.Find(id);

            if (teacher == null)
            {
                return NotFound();
            }

            return View(teacher);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var teacher = _context.Users.FirstOrDefault(x => x.Id == id && x.Role == "Teacher");

            if (teacher == null)
            {
                return NotFound();
            }

            _context.Users.Remove(teacher);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(User user)
        {
            // Role is assigned by this admin-only action, not by the form.
            ModelState.Remove(nameof(user.Role));
            user.Role = "Teacher";
            user.IsVerified = true;

            if (_context.Users.Any(x => x.Email == user.Email))
                ModelState.AddModelError(nameof(user.Email), "This email is already registered.");

            if (!ModelState.IsValid)
                return View(user);

            user.Password = PasswordHelper.Hash(user.Password);
            _context.Users.Add(user);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(User user)
        {
            ModelState.Remove(nameof(user.Password));
            var existingTeacher = _context.Users.FirstOrDefault(x => x.Id == user.Id && x.Role == "Teacher");
            if (existingTeacher == null)
                return NotFound();

            if (_context.Users.Any(x => x.Email == user.Email && x.Id != user.Id))
                ModelState.AddModelError(nameof(user.Email), "This email is already registered.");

            if (!ModelState.IsValid)
                return View(user);

            existingTeacher.Name = user.Name;
            existingTeacher.Email = user.Email;
            _context.SaveChanges();

            return RedirectToAction("Index");
        }
    }
}
