using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutionCenter.Data;

namespace TutionCenter.Controllers;

[Authorize(Roles = "Teacher")]
public class TeacherClassController : Controller
{
    private readonly ApplicationDbContext _context;

    public TeacherClassController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index(string? search, string? day, string? status)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var teacher = await _context.Users.AsNoTracking()
            .SingleOrDefaultAsync(user => user.Email == email && user.Role == "Teacher");
        if (teacher is null) return Forbid();

        search = search?.Trim() ?? string.Empty;
        day = string.IsNullOrWhiteSpace(day) ? "all" : day;
        status = status is "open" or "closed" ? status : "all";

        var classes = _context.TuitionClasses.AsNoTracking()
            .Include(item => item.Subject)
            .Include(item => item.Enrollments)
            .Where(item => item.TeacherId == teacher.Id);

        if (!string.IsNullOrEmpty(search))
            classes = classes.Where(item => item.ClassCode.Contains(search) || item.Subject!.SubjectCode.Contains(search) || item.Subject.Name.Contains(search));
        if (day != "all") classes = classes.Where(item => item.DayOfWeek == day);
        if (status == "open") classes = classes.Where(item => item.IsActive);
        if (status == "closed") classes = classes.Where(item => !item.IsActive);

        ViewBag.Search = search;
        ViewBag.Day = day;
        ViewBag.Status = status;
        ViewBag.Days = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

        return View(await classes.OrderBy(item => item.DayOfWeek).ThenBy(item => item.StartTime).ToListAsync());
    }
}
