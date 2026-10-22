using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TutionCenter.Data;
using TutionCenter.Models;

namespace TutionCenter.Controllers;

[Authorize(Roles = "Admin,Student")]
public class EnrollmentController : Controller
{
    private readonly ApplicationDbContext _context;

    public EnrollmentController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var query = EnrollmentQuery();

        if (User.IsInRole("Student"))
        {
            var student = await CurrentStudent();

            if (student is null)
            {
                return Forbid();
            }

            query = query.Where(e => e.StudentId == student.Id);
        }

        return View(
            await query
                .OrderByDescending(e => e.EnrolledAt)
                .ToListAsync()
        );
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create()
    {
        await PopulateOptions();

        return View(new EnrollmentCreateViewModel());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EnrollmentCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateOptions(model);
            return View(model);
        }

        var error = await Enroll(
            model.StudentId,
            model.TuitionClassId
        );

        if (error is not null)
        {
            ModelState.AddModelError(string.Empty, error);
            await PopulateOptions(model);
            return View(model);
        }

        TempData["Success"] = "Student enrolled successfully.";

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Enroll()
    {
        var student = await CurrentStudent();

        if (student is null)
        {
            return Forbid();
        }

        var classes = await _context.TuitionClasses
            .AsNoTracking()
            .Include(c => c.Subject)
            .Include(c => c.Teacher)
            .Where(c =>
                c.IsActive &&
                c.Subject!.IsActive &&
                !_context.Enrollments.Any(e =>
                    e.StudentId == student.Id &&
                    e.TuitionClassId == c.Id &&
                    !e.IsDropped
                )
            )
            .OrderBy(c => c.Subject!.SubjectCode)
            .ThenBy(c => c.DayOfWeek)
            .ToListAsync();

        return View(classes);
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enroll(int tuitionClassId)
    {
        var student = await CurrentStudent();

        if (student is null)
        {
            return Forbid();
        }

        var error = await Enroll(
            student.Id,
            tuitionClassId
        );

        TempData[error is null ? "Success" : "Error"] =
            error ?? "Enrollment completed successfully.";

        return RedirectToAction(
            error is null ? nameof(Index) : nameof(Enroll)
        );
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Drop(int id)
    {
        var enrollment = await _context.Enrollments.FindAsync(id);

        if (enrollment is null)
        {
            return NotFound();
        }

        if (User.IsInRole("Student"))
        {
            var student = await CurrentStudent();

            if (student is null || enrollment.StudentId != student.Id)
            {
                return Forbid();
            }
        }

        enrollment.IsDropped = true;

        var unpaidBills = await _context.Bills
            .Where(b =>
                b.EnrollmentId == enrollment.Id &&
                b.Status == "Unpaid")
            .ToListAsync();

        foreach (var bill in unpaidBills)
        {
            bill.Status = "Cancelled";
        }

        await _context.SaveChangesAsync();

        TempData["Success"] =
            "Enrollment dropped. Any unpaid bill was cancelled.";

        return RedirectToAction(nameof(Index));
    }

    private IQueryable<Enrollment> EnrollmentQuery()
    {
        return _context.Enrollments
            .AsNoTracking()
            .Where(e => !e.IsDropped)
            .Include(e => e.Student)
            .Include(e => e.TuitionClass)!
                .ThenInclude(c => c!.Subject)
            .Include(e => e.TuitionClass)!
                .ThenInclude(c => c!.Teacher);
    }

    private async Task<User?> CurrentStudent()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);

        return await _context.Users
            .SingleOrDefaultAsync(u =>
                u.Email == email &&
                u.Role == "Student"
            );
    }

    private async Task<string?> Enroll(
        int studentId,
        int classId)
    {
        var tuitionClass = await _context.TuitionClasses
            .Include(c => c.Subject)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == classId);

        if (tuitionClass is null ||
            !tuitionClass.IsActive ||
            tuitionClass.Subject is null ||
            !tuitionClass.Subject.IsActive)
        {
            return "This class is not available for enrollment.";
        }

        if (!await _context.Users.AnyAsync(u =>
                u.Id == studentId &&
                u.Role == "Student"))
        {
            return "The selected student is invalid.";
        }

        var existingEnrollment = tuitionClass.Enrollments
            .FirstOrDefault(e => e.StudentId == studentId);

        // Student previously dropped this class.
        // Restore the enrollment if there is no schedule conflict.
        if (existingEnrollment is not null)
        {
            if (existingEnrollment.IsDropped)
            {
                // Check whether the class is already full.
                int activeEnrollmentCount =
                    tuitionClass.Enrollments.Count(e => !e.IsDropped);

                if (activeEnrollmentCount >= tuitionClass.Capacity)
                {
                    return "This class is already full.";
                }

                // Check whether this student has another active class
                // at the same day and overlapping time.
                var scheduleConflict =
                    await FindStudentScheduleConflict(
                        studentId,
                        classId
                    );

                if (scheduleConflict is not null)
                {
                    return scheduleConflict;
                }

                existingEnrollment.IsDropped = false;
                existingEnrollment.EnrolledAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                string billingPeriod =
                    DateTime.Today.ToString("yyyy-MM");

                bool billExists = await _context.Bills.AnyAsync(b =>
                    b.StudentId == studentId &&
                    b.EnrollmentId == existingEnrollment.Id &&
                    b.BillingPeriod == billingPeriod &&
                    b.Status != "Cancelled");

                if (!billExists)
                {
                    var bill = new Bill
                    {
                        StudentId = studentId,
                        EnrollmentId = existingEnrollment.Id,
                        Description =
                            $"{tuitionClass.Subject.SubjectCode} - Monthly Tuition Fee",
                        Amount = tuitionClass.Subject.MonthlyFee,
                        BillDate = DateTime.Today,
                        DueDate = DateTime.Today.AddDays(30),
                        BillingPeriod = billingPeriod,
                        Status = "Unpaid"
                    };

                    _context.Bills.Add(bill);

                    await _context.SaveChangesAsync();
                }

                return null;
            }

            return "This student is already enrolled in this class.";
        }

        // Only active enrollments should count toward capacity.
        int currentActiveEnrollments =
            tuitionClass.Enrollments.Count(e => !e.IsDropped);

        if (currentActiveEnrollments >= tuitionClass.Capacity)
        {
            return "This class is already full.";
        }

        // Check whether the student already has another active class
        // on the same day with an overlapping time.
        var conflict =
            await FindStudentScheduleConflict(
                studentId,
                classId
            );

        if (conflict is not null)
        {
            return conflict;
        }

        // Create enrollment
        var enrollment = new Enrollment
        {
            StudentId = studentId,
            TuitionClassId = classId,
            EnrolledAt = DateTime.UtcNow
        };

        _context.Enrollments.Add(enrollment);

        // Save first so the Enrollment gets its ID.
        await _context.SaveChangesAsync();

        // One monthly bill is created with the enrollment.
        string newBillingPeriod =
            DateTime.Today.ToString("yyyy-MM");

        bool newBillExists = await _context.Bills.AnyAsync(b =>
            b.StudentId == studentId &&
            b.EnrollmentId == enrollment.Id &&
            b.BillingPeriod == newBillingPeriod);

        if (!newBillExists)
        {
            var bill = new Bill
            {
                StudentId = studentId,
                EnrollmentId = enrollment.Id,
                Description =
                    $"{tuitionClass.Subject.SubjectCode} - Monthly Tuition Fee",
                Amount = tuitionClass.Subject.MonthlyFee,
                BillDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30),
                BillingPeriod = newBillingPeriod,
                Status = "Unpaid"
            };

            _context.Bills.Add(bill);

            await _context.SaveChangesAsync();
        }

        return null;
    }

    private async Task<string?> FindStudentScheduleConflict(
        int studentId,
        int classId)
    {
        var newClass = await _context.TuitionClasses
            .AsNoTracking()
            .Include(c => c.Subject)
            .FirstOrDefaultAsync(c => c.Id == classId);

        if (newClass is null)
        {
            return null;
        }

        var conflictingClass = await _context.Enrollments
            .AsNoTracking()
            .Where(e =>
                e.StudentId == studentId &&
                !e.IsDropped &&
                e.TuitionClassId != classId &&
                e.TuitionClass != null &&
                e.TuitionClass.IsActive &&
                e.TuitionClass.DayOfWeek == newClass.DayOfWeek &&
                e.TuitionClass.StartTime < newClass.EndTime &&
                e.TuitionClass.EndTime > newClass.StartTime)
            .Include(e => e.TuitionClass!)
                .ThenInclude(c => c.Subject)
            .FirstOrDefaultAsync();

        if (conflictingClass?.TuitionClass is null)
        {
            return null;
        }

        var existingClass = conflictingClass.TuitionClass;

        string subjectName =
            existingClass.Subject != null
                ? $"{existingClass.Subject.SubjectCode} - {existingClass.Subject.Name}"
                : existingClass.ClassCode;

        return
            $"Schedule conflict: You already have {subjectName} " +
            $"({existingClass.ClassCode}) scheduled on " +
            $"{existingClass.DayOfWeek} " +
            $"{existingClass.StartTime:HH\\:mm} - {existingClass.EndTime:HH\\:mm}.";
    }

    private async Task PopulateOptions(
        EnrollmentCreateViewModel? model = null)
    {
        ViewBag.Students = new SelectList(
            await _context.Users
                .Where(u => u.Role == "Student")
                .OrderBy(u => u.Name)
                .ToListAsync(),
            "Id",
            "Name",
            model?.StudentId
        );

        var classes = await _context.TuitionClasses
            .Include(c => c.Subject)
            .Where(c =>
                c.IsActive &&
                c.Subject!.IsActive
            )
            .OrderBy(c => c.Subject!.SubjectCode)
            .ToListAsync();

        ViewBag.Classes = new SelectList(
            classes.Select(c => new
            {
                c.Id,
                Name =
                    $"{c.Subject!.SubjectCode} - {c.ClassCode} " +
                    $"({c.DayOfWeek} {c.StartTime:HH\\:mm})"
            }),
            "Id",
            "Name",
            model?.TuitionClassId
        );
    }

    [Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> Timetable()
    {
        var student = await CurrentStudent();

        if (student is null)
        {
            return Forbid();
        }

        var timetable = await _context.Enrollments
            .AsNoTracking()
            .Where(e =>
                e.StudentId == student.Id &&
                !e.IsDropped &&
                e.TuitionClass != null &&
                e.TuitionClass.IsActive &&
                e.TuitionClass.Subject != null &&
                e.TuitionClass.Subject.IsActive)
            .Include(e => e.TuitionClass!)
                .ThenInclude(c => c.Subject)
            .Include(e => e.TuitionClass!)
                .ThenInclude(c => c.Teacher)
            .Select(e => new StudentTimetableViewModel
            {
                Day = e.TuitionClass!.DayOfWeek,

                SubjectCode =
                    e.TuitionClass.Subject!.SubjectCode,

                SubjectName =
                    e.TuitionClass.Subject.Name,

                ClassCode =
                    e.TuitionClass.ClassCode,

                StartTime =
                    e.TuitionClass.StartTime,

                EndTime =
                    e.TuitionClass.EndTime,

                Room =
                    e.TuitionClass.Room,

                TeacherName =
                    e.TuitionClass.Teacher != null
                        ? e.TuitionClass.Teacher.Name
                        : "Not Assigned"
            })
            .OrderBy(x => x.Day)
            .ThenBy(x => x.StartTime)
            .ToListAsync();

        return View(timetable);
    }
}