using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TutionCenter.Data;

namespace TutionCenter.Controllers;

[Authorize(Roles = "Student")]
public class StudentSubjectController : Controller
{
    private readonly ApplicationDbContext _context;

    public StudentSubjectController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userId, out int studentId))
        {
            return Unauthorized();
        }

        var subjects = await _context.Enrollments
            .Where(e => e.StudentId == studentId && !e.IsDropped)
            .Include(e => e.TuitionClass)
                .ThenInclude(c => c!.Subject)
            .Where(e => e.TuitionClass!.Subject != null)
            .Select(e => e.TuitionClass!.Subject!)
            .Where(s => s.IsActive)
            .Distinct()
            .OrderBy(s => s.SubjectCode)
            .ToListAsync();

        ViewBag.TotalSubjects = subjects.Count;

        return View(subjects);
    }
}