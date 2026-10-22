using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TutionCenter.Data;
using TutionCenter.Models;

namespace TutionCenter.Controllers
{
    [Authorize]
    public class AttendanceSessionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AttendanceSessionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // Teacher / Admin：查看签到码记录
        // =========================================================
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> Index()
        {
            User? currentUser = await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            IQueryable<AttendanceSession> query =
                _context.AttendanceSessions
                    .Include(a => a.TuitionClass)
                        .ThenInclude(c => c!.Subject)
                    .Include(a => a.CreatedByUser)
                    .Include(a => a.Attendances)
                    .OrderByDescending(a => a.CreatedAt);

            // Teacher 只能看到自己负责班级的签到码
            if (User.IsInRole("Teacher"))
            {
                query = query.Where(a =>
                    a.TuitionClass != null &&
                    a.TuitionClass.TeacherId == currentUser.Id);
            }

            List<AttendanceSession> sessions = await query.ToListAsync();

            return View(sessions);
        }

        // =========================================================
        // Teacher / Admin：打开产生签到码页面
        // =========================================================
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> Create()
        {
            User? currentUser = await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            await PopulateClassesAsync(currentUser);

            return View();
        }

        // =========================================================
        // Teacher / Admin：产生 6 位数字签到码
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> Create(
            int tuitionClassId,
            int validMinutes = 5)
        {
            User? currentUser = await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // 签到码有效时间必须介于 1 至 60 分钟
            if (validMinutes < 1 || validMinutes > 60)
            {
                ModelState.AddModelError(
                    "validMinutes",
                    "Valid duration must be between 1 and 60 minutes.");
            }

            TuitionClass? tuitionClass =
                await _context.TuitionClasses
                    .Include(c => c.Subject)
                    .FirstOrDefaultAsync(c => c.Id == tuitionClassId);

            if (tuitionClass == null)
            {
                ModelState.AddModelError(
                    "tuitionClassId",
                    "The selected class does not exist.");
            }
            else if (
                User.IsInRole("Teacher") &&
                tuitionClass.TeacherId != currentUser.Id)
            {
                // Teacher 不能替其他 Teacher 的班级产生签到码
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                await PopulateClassesAsync(currentUser, tuitionClassId);

                return View();
            }

            // 关闭这个班级之前还没有过期的签到码
            List<AttendanceSession> previousSessions =
                await _context.AttendanceSessions
                    .Where(a =>
                        a.TuitionClassId == tuitionClassId &&
                        a.IsActive)
                    .ToListAsync();

            foreach (AttendanceSession previousSession in previousSessions)
            {
                previousSession.IsActive = false;
            }

            string attendanceCode = await GenerateUniqueCodeAsync();

            AttendanceSession session = new AttendanceSession
            {
                TuitionClassId = tuitionClassId,
                AttendanceCode = attendanceCode,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddMinutes(validMinutes),
                IsActive = true,
                CreatedByUserId = currentUser.Id
            };

            _context.AttendanceSessions.Add(session);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Attendance code generated successfully.";

            return RedirectToAction(
                nameof(Details),
                new { id = session.Id });
        }

        // =========================================================
        // Teacher / Admin：显示签到码和签到学生
        // =========================================================
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> Details(int id)
        {
            User? currentUser = await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            AttendanceSession? session =
                await _context.AttendanceSessions
                    .Include(a => a.TuitionClass)
                        .ThenInclude(c => c!.Subject)
                    .Include(a => a.CreatedByUser)
                    .Include(a => a.Attendances)
                        .ThenInclude(a => a.Student)
                    .FirstOrDefaultAsync(a => a.Id == id);

            if (session == null)
            {
                return NotFound();
            }

            // Teacher 只能查看自己班级的签到码
            if (
                User.IsInRole("Teacher") &&
                session.TuitionClass?.TeacherId != currentUser.Id)
            {
                return Forbid();
            }

            // 页面打开时，如果已经过期，就自动关闭
            if (
                session.IsActive &&
                session.ExpiresAt <= DateTime.Now)
            {
                session.IsActive = false;

                await _context.SaveChangesAsync();
            }

            return View(session);
        }

        // =========================================================
        // Teacher / Admin：手动结束签到
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> Close(int id)
        {
            User? currentUser = await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            AttendanceSession? session =
                await _context.AttendanceSessions
                    .Include(a => a.TuitionClass)
                    .FirstOrDefaultAsync(a => a.Id == id);

            if (session == null)
            {
                return NotFound();
            }

            if (
                User.IsInRole("Teacher") &&
                session.TuitionClass?.TeacherId != currentUser.Id)
            {
                return Forbid();
            }

            session.IsActive = false;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Attendance session has been closed.";

            return RedirectToAction(
                nameof(Details),
                new { id });
        }

        // =========================================================
        // Student：打开输入签到码页面
        // =========================================================
        [Authorize(Roles = "Student")]
        public IActionResult CheckIn()
        {
            return View(new AttendanceCodeViewModel());
        }

        // =========================================================
        // Student：输入数字后进行签到
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CheckIn(
            AttendanceCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            User? student = await GetCurrentUserAsync();

            if (student == null || student.Role != "Student")
            {
                return RedirectToAction("Login", "Account");
            }

            AttendanceSession? session =
                await _context.AttendanceSessions
                    .Include(a => a.TuitionClass)
                        .ThenInclude(c => c!.Subject)
                    .FirstOrDefaultAsync(a =>
                        a.AttendanceCode == model.AttendanceCode);

            // 检查签到码是否存在
            if (session == null)
            {
                ModelState.AddModelError(
                    nameof(model.AttendanceCode),
                    "Invalid attendance code.");

                return View(model);
            }

            // 检查签到码是否已经被 Teacher 关闭
            if (!session.IsActive)
            {
                ModelState.AddModelError(
                    nameof(model.AttendanceCode),
                    "This attendance code is no longer active.");

                return View(model);
            }

            // 检查签到码是否已经过期
            if (session.ExpiresAt <= DateTime.Now)
            {
                session.IsActive = false;

                await _context.SaveChangesAsync();

                ModelState.AddModelError(
                    nameof(model.AttendanceCode),
                    "This attendance code has expired.");

                return View(model);
            }

            // 检查 Student 是否真的报名此班级
            bool isEnrolled =
                await _context.Enrollments.AnyAsync(e =>
                    e.StudentId == student.Id &&
                    e.TuitionClassId == session.TuitionClassId);

            if (!isEnrolled)
            {
                ModelState.AddModelError(
                    nameof(model.AttendanceCode),
                    "You are not enrolled in this class.");

                return View(model);
            }

            // 检查 Student 是否已经使用这个签到码签到
            bool alreadyCheckedIn =
                await _context.Attendances.AnyAsync(a =>
                    a.StudentId == student.Id &&
                    a.AttendanceSessionId == session.Id);

            if (alreadyCheckedIn)
            {
                ModelState.AddModelError(
                    nameof(model.AttendanceCode),
                    "You have already checked in for this class.");

                return View(model);
            }

            // 检查同一天同一个班级是否已经有 Attendance
            bool attendanceExistsForToday =
                await _context.Attendances.AnyAsync(a =>
                    a.StudentId == student.Id &&
                    a.TuitionClassId == session.TuitionClassId &&
                    a.AttendanceDate.Date == DateTime.Today);

            if (attendanceExistsForToday)
            {
                ModelState.AddModelError(
                    nameof(model.AttendanceCode),
                    "Your attendance for this class has already been recorded today.");

                return View(model);
            }

            // 自动建立 Present Attendance
            Attendance attendance = new Attendance
            {
                StudentId = student.Id,
                TuitionClassId = session.TuitionClassId,
                AttendanceDate = DateTime.Now,
                Status = "Present",
                CheckInTime = DateTime.Now,
                AttendanceSessionId = session.Id
            };

            _context.Attendances.Add(attendance);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Attendance recorded successfully for " +
                $"{session.TuitionClass?.Subject?.Name} " +
                $"({session.TuitionClass?.ClassCode}).";

            return RedirectToAction(nameof(CheckInSuccess));
        }

        // =========================================================
        // Student：签到成功页面
        // =========================================================
        [Authorize(Roles = "Student")]
        public IActionResult CheckInSuccess()
        {
            if (TempData["SuccessMessage"] == null)
            {
                return RedirectToAction(nameof(CheckIn));
            }

            return View();
        }

        // =========================================================
        // 找出目前登录的 User
        // =========================================================
        private async Task<User?> GetCurrentUserAsync()
        {
            string? email =
                User.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            return await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        // =========================================================
        // 产生不会重复的 6 位数字
        // =========================================================
        private async Task<string> GenerateUniqueCodeAsync()
        {
            string code;

            do
            {
                code = Random.Shared
                    .Next(100000, 1000000)
                    .ToString();
            }
            while (
                await _context.AttendanceSessions
                    .AnyAsync(a => a.AttendanceCode == code));

            return code;
        }

        // =========================================================
        // 准备 Teacher / Admin 可以选择的班级
        // =========================================================
        private async Task PopulateClassesAsync(
            User currentUser,
            int? selectedClassId = null)
        {
            IQueryable<TuitionClass> query =
                _context.TuitionClasses
                    .Include(c => c.Subject)
                    .Where(c => c.IsActive);

            // Teacher 只能选择自己负责的班级
            if (User.IsInRole("Teacher"))
            {
                query = query.Where(c =>
                    c.TeacherId == currentUser.Id);
            }

            List<TuitionClass> classes =
                await query
                    .OrderBy(c => c.Subject!.Name)
                    .ThenBy(c => c.ClassCode)
                    .ToListAsync();

            ViewBag.Classes = new SelectList(
                classes.Select(c => new
                {
                    c.Id,
                    DisplayText =
                        $"{c.Subject?.Name} - {c.ClassCode}"
                }),
                "Id",
                "DisplayText",
                selectedClassId);
        }
    }
}