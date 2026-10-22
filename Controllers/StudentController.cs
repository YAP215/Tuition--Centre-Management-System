using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TutionCenter.Data;
using TutionCenter.Models;
using TutionCenter.Services;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace TutionCenter.Controllers
{
    [Authorize(Roles = "Admin")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudentController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var students = _context.Users
                .Where(x => x.Role == "Student")
                .ToList();

            return View(students);
        }

        public IActionResult Create()
        {
            return View();
        }

        public IActionResult Edit(int id)
        {
            var student = _context.Users.Find(id);

            if (student == null)
            {
                return NotFound();
            }

            return View(student);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var student = _context.Users.FirstOrDefault(x => x.Id == id && x.Role == "Student");

            if (student == null)
            {
                return NotFound();
            }

            _context.Users.Remove(student);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(User user)
        {
            // Role is assigned by this admin-only action, not by the form.
            ModelState.Remove(nameof(user.Role));
            user.Role = "Student";
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
            var existingStudent = _context.Users.FirstOrDefault(x => x.Id == user.Id && x.Role == "Student");
            if (existingStudent == null)
                return NotFound();

            if (_context.Users.Any(x => x.Email == user.Email && x.Id != user.Id))
                ModelState.AddModelError(nameof(user.Email), "This email is already registered.");

            if (!ModelState.IsValid)
                return View(user);

            existingStudent.Name = user.Name;
            existingStudent.Email = user.Email;
            _context.SaveChanges();

            return RedirectToAction("Index");
        }
    }
}
