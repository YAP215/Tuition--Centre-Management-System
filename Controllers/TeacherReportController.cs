using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TutionCenter.Data;

namespace TutionCenter.Controllers;

[Authorize(Roles = "Teacher")]
public class TeacherReportController : Controller
{
    private readonly ApplicationDbContext _context;

    public TeacherReportController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> StudentReport(
        string? search,
        string? classCode,
        string? subjectName)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userId, out int teacherId))
        {
            return Unauthorized();
        }

        var query = _context.Enrollments
            .Where(e => e.TuitionClass!.TeacherId == teacherId)
            .Include(e => e.Student)
            .Include(e => e.TuitionClass!)
                .ThenInclude(c => c.Subject)
            .AsQueryable();

        // Search by student name
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e =>
                e.Student!.Name.Contains(search));
        }

        // Filter by class
        if (!string.IsNullOrWhiteSpace(classCode))
        {
            query = query.Where(e =>
                e.TuitionClass!.ClassCode == classCode);
        }

        // Filter by subject
        if (!string.IsNullOrWhiteSpace(subjectName))
        {
            query = query.Where(e =>
                e.TuitionClass!.Subject!.Name == subjectName);
        }

        var students = await query
            .Select(e => new
            {
                StudentName = e.Student!.Name,
                Email = e.Student.Email,
                ClassCode = e.TuitionClass!.ClassCode,
                SubjectName = e.TuitionClass.Subject!.Name
            })
            .OrderBy(x => x.StudentName)
            .ToListAsync();

        // Summary
        ViewBag.TotalEnrollments = students.Count;
        ViewBag.TotalStudents = students
            .Select(x => x.StudentName)
            .Distinct()
            .Count();

        ViewBag.TotalSubjects = students
            .Select(x => x.SubjectName)
            .Distinct()
            .Count();

        // Filter options
        ViewBag.Classes = await _context.Enrollments
            .Where(e => e.TuitionClass!.TeacherId == teacherId)
            .Select(e => e.TuitionClass!.ClassCode)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        ViewBag.Subjects = await _context.Enrollments
            .Where(e => e.TuitionClass!.TeacherId == teacherId)
            .Select(e => e.TuitionClass!.Subject!.Name)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.SelectedClass = classCode;
        ViewBag.SelectedSubject = subjectName;

        return View(students);
    }
    public async Task<IActionResult> AttendanceReport()
    {

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userId, out int teacherId))
        {
            return Unauthorized();
        }

        var attendance = await _context.Attendances
            .Where(a => a.TuitionClass!.TeacherId == teacherId)
            .Include(a => a.Student)
            .Include(a => a.TuitionClass!)
                .ThenInclude(c => c.Subject)
            .OrderByDescending(a => a.AttendanceDate)
            .Select(a => new
            {
                AttendanceId = a.Id,
                StudentName = a.Student!.Name,
                ClassCode = a.TuitionClass!.ClassCode,
                SubjectName = a.TuitionClass.Subject!.Name,
                AttendanceDate = a.AttendanceDate,
                Status = a.Status,
                Remarks = a.Remarks
            })
            .ToListAsync();

        return View(attendance);
    }
    public async Task<IActionResult> EditAttendance(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userId, out int teacherId))
        {
            return Unauthorized();
        }

        var attendance = await _context.Attendances
            .Include(a => a.Student)
            .Include(a => a.TuitionClass!)
                .ThenInclude(c => c.Subject)
            .FirstOrDefaultAsync(a =>
                a.Id == id &&
                a.TuitionClass!.TeacherId == teacherId);

        if (attendance == null)
        {
            return NotFound();
        }

        return View(attendance);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAttendance(
    int id,
    string status,
    string? remarks)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userId, out int teacherId))
        {
            return Unauthorized();
        }

        var attendance = await _context.Attendances
            .FirstOrDefaultAsync(a =>
                a.Id == id &&
                a.TuitionClass!.TeacherId == teacherId);

        if (attendance == null)
        {
            return NotFound();
        }

        if (status != "Present" &&
            status != "Absent" &&
            status != "Late")
        {
            ModelState.AddModelError("Status", "Invalid attendance status.");
        }

        if (!ModelState.IsValid)
        {
            return View(attendance);
        }

        attendance.Status = status;
        attendance.Remarks = remarks;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(AttendanceReport));
    }
}