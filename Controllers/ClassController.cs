using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TutionCenter.Data;
using TutionCenter.Models;

namespace TutionCenter.Controllers;

[Authorize(Roles = "Admin")]
public class ClassController : Controller
{
    private readonly ApplicationDbContext _context;

    public ClassController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Class List
    public async Task<IActionResult> Index()
    {
        var classes = await _context.TuitionClasses
            .AsNoTracking()
            .Include(c => c.Subject)
            .Include(c => c.Teacher)
            .Include(c => c.Enrollments)
            .OrderBy(c => c.DayOfWeek)
            .ThenBy(c => c.StartTime)
            .ToListAsync();

        return View(classes);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateOptions();

        return View(
            new TuitionClass
            {
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 30)
            });
    }

    // Create - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TuitionClass tuitionClass)
    {
        // Normalize Class Code before validation/checking
        tuitionClass.ClassCode =
            tuitionClass.ClassCode?.Trim().ToUpperInvariant()
            ?? string.Empty;

        // Check duplicate Class Code
        if (await _context.TuitionClasses.AnyAsync(c =>
                c.ClassCode == tuitionClass.ClassCode))
        {
            ModelState.AddModelError(
                nameof(tuitionClass.ClassCode),
                "This class code is already in use.");
        }

        // Check schedule conflicts

        await ValidateScheduleConflict(tuitionClass);

        // Validation failed

        if (!ModelState.IsValid)
        {
            await PopulateOptions(tuitionClass);

            return View(tuitionClass);
        }

        // Create Class

        _context.TuitionClasses.Add(tuitionClass);

        await _context.SaveChangesAsync();

        TempData["Success"] =
            "Class created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // Edit - GET

    public async Task<IActionResult> Edit(int id)
    {
        var tuitionClass =
            await _context.TuitionClasses.FindAsync(id);

        if (tuitionClass is null)
        {
            return NotFound();
        }

        await PopulateOptions(tuitionClass);

        return View(tuitionClass);
    }

    // Edit - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        TuitionClass tuitionClass)
    {

        if (id != tuitionClass.Id)
        {
            return BadRequest();
        }

        // Normalize Class Code
        tuitionClass.ClassCode =
            tuitionClass.ClassCode?.Trim().ToUpperInvariant()
            ?? string.Empty;

        // Check duplicate Class Code
        // Exclude current class itself
        if (await _context.TuitionClasses.AnyAsync(c =>
                c.Id != id &&
                c.ClassCode == tuitionClass.ClassCode))
        {
            ModelState.AddModelError(
                nameof(tuitionClass.ClassCode),
                "This class code is already in use.");
        }

        // Check schedule conflicts
        // Exclude current class itself
        await ValidateScheduleConflict(
            tuitionClass,
            id);

        // Validation failed
        if (!ModelState.IsValid)
        {
            await PopulateOptions(tuitionClass);

            return View(tuitionClass);
        }
        _context.Update(tuitionClass);

        await _context.SaveChangesAsync();

        TempData["Success"] =
            "Class updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var tuitionClass =
            await _context.TuitionClasses
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c => c.Id == id);

        if (tuitionClass is null)
        {
            return NotFound();
        }

        // Prevent deleting classes with enrollments
        if (tuitionClass.Enrollments.Any())
        {
            TempData["Error"] =
                "A class with enrollments cannot be deleted. Mark it closed instead.";

            return RedirectToAction(nameof(Index));
        }

        _context.TuitionClasses.Remove(tuitionClass);

        await _context.SaveChangesAsync();

        TempData["Success"] =
            "Class deleted successfully.";

        return RedirectToAction(nameof(Index));
    }

    // Additional Feature:
    // Class Schedule Conflict Detection
    private async Task ValidateScheduleConflict(
        TuitionClass tuitionClass,
        int? excludeClassId = null)
    {
        // If the class itself is inactive,
        // schedule conflict checking is not necessary.
        if (!tuitionClass.IsActive)
        {
            return;
        }

        // Do not check invalid time ranges here.
        // TuitionClass.IValidatableObject already handles:
        // EndTime must be later than StartTime.
        if (tuitionClass.EndTime <= tuitionClass.StartTime)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(tuitionClass.DayOfWeek))
        {
            return;
        }

        // Find classes on the same day
        IQueryable<TuitionClass> query =
            _context.TuitionClasses
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&
                    c.DayOfWeek == tuitionClass.DayOfWeek);

        if (excludeClassId.HasValue)
        {
            query = query.Where(c =>
                c.Id != excludeClassId.Value);
        }

        var existingClasses =
            await query.ToListAsync();

        // Teacher Conflict
        if (tuitionClass.TeacherId.HasValue)
        {
            bool teacherConflict =
                existingClasses.Any(c =>
                    c.TeacherId == tuitionClass.TeacherId &&
                    c.StartTime < tuitionClass.EndTime &&
                    c.EndTime > tuitionClass.StartTime);

            if (teacherConflict)
            {
                ModelState.AddModelError(
                    nameof(tuitionClass.TeacherId),
                    "Schedule conflict: This teacher already has another class during the selected time.");
            }
        }

        // Room Conflict
        if (!string.IsNullOrWhiteSpace(tuitionClass.Room))
        {
            string room =
                tuitionClass.Room.Trim();

            bool roomConflict =
                existingClasses.Any(c =>
                    !string.IsNullOrWhiteSpace(c.Room) &&
                    c.Room.Trim()
                        .Equals(
                            room,
                            StringComparison.OrdinalIgnoreCase) &&
                    c.StartTime < tuitionClass.EndTime &&
                    c.EndTime > tuitionClass.StartTime);

            if (roomConflict)
            {
                ModelState.AddModelError(
                    nameof(tuitionClass.Room),
                    "Schedule conflict: This room is already occupied during the selected time.");
            }
        }
    }

    // Populate Subject / Teacher / Day dropdowns
    private async Task PopulateOptions(
        TuitionClass? selected = null)
    {
        ViewBag.Subjects =
            new SelectList(
                await _context.Subjects
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.SubjectCode)
                    .ToListAsync(),
                "Id",
                "SubjectCode",
                selected?.SubjectId);

        ViewBag.Teachers =
            new SelectList(
                await _context.Users
                    .Where(u => u.Role == "Teacher")
                    .OrderBy(u => u.Name)
                    .ToListAsync(),
                "Id",
                "Name",
                selected?.TeacherId);

        ViewBag.Days =
            new SelectList(
                new[]
                {
                    "Monday",
                    "Tuesday",
                    "Wednesday",
                    "Thursday",
                    "Friday",
                    "Saturday",
                    "Sunday"
                },
                selected?.DayOfWeek);
    }
}