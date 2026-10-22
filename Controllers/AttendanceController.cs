using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TutionCenter.Data;
using TutionCenter.Models;
using TutionCenter.Services;

namespace TutionCenter.Controllers
{
    // Admin、Teacher 和 Student 都可以进入 Attendance 模块
    [Authorize(Roles = "Admin,Teacher,Student")]
    public class AttendanceController : Controller
    {
        // 数据库连接
        private readonly ApplicationDbContext _context;

        // PDF Report Service
        private readonly AttendancePdfService _attendancePdfService;

        public AttendanceController(
             ApplicationDbContext context,
             AttendancePdfService attendancePdfService)
        {
            _context = context;
            _attendancePdfService = attendancePdfService;
        }

        // =========================================================
        // Attendance Records 首页
        // 按月份筛选，并把同一个 Subject + Class 合并成一行
        // =========================================================
        public async Task<IActionResult> Index(
            int? month,
            int? year)
        {
            // 没有选择月份时，默认使用当前月份
            int selectedMonth =
                month ?? DateTime.Today.Month;

            // 没有选择年份时，默认使用当前年份
            int selectedYear =
                year ?? DateTime.Today.Year;

            // 防止月份超出 1 至 12
            if (selectedMonth < 1 ||
                selectedMonth > 12)
            {
                selectedMonth =
                    DateTime.Today.Month;
            }

            // 防止年份不合理
            if (selectedYear < 2000 ||
                selectedYear > 2100)
            {
                selectedYear =
                    DateTime.Today.Year;
            }

            // 取得目前登录的 User
            User? currentUser =
                await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            // 准备 Attendance 查询
            IQueryable<Attendance> query =
                _context.Attendances
                    .AsNoTracking()
                    .Include(a => a.Student)
                    .Include(a => a.TuitionClass)
                        .ThenInclude(c => c!.Subject);

            // Student 只能计算自己的 Attendance
            if (User.IsInRole("Student"))
            {
                query = query.Where(a =>
                    a.StudentId == currentUser.Id);
            }

            // Teacher 只能计算自己负责班级的 Attendance
            else if (User.IsInRole("Teacher"))
            {
                query = query.Where(a =>
                    a.TuitionClass != null &&
                    a.TuitionClass.TeacherId ==
                        currentUser.Id);
            }

            // Admin 不需要过滤，可以看到全部班级
            // 根据所选月份和年份读取 Attendance
            List<Attendance> monthlyRecords =
                await query
                    .Where(a =>
                        a.AttendanceDate.Month ==
                            selectedMonth &&
                        a.AttendanceDate.Year ==
                            selectedYear)
                    .OrderBy(a =>
                        a.TuitionClass!.Subject!.Name)
                    .ThenBy(a =>
                        a.TuitionClass!.ClassCode)
                    .ThenByDescending(a =>
                        a.AttendanceDate)
                    .ToListAsync();

            // =====================================================
            // 计算当前月份整体统计
            // =====================================================

            int totalRecords =
                monthlyRecords.Count;

            int presentCount =
                monthlyRecords.Count(a =>
                    a.Status == "Present");

            int lateCount =
                monthlyRecords.Count(a =>
                    a.Status == "Late");

            int absentCount =
                monthlyRecords.Count(a =>
                    a.Status == "Absent");

            // Present 和 Late 都算有出席
            double overallPercentage =
                totalRecords == 0
                    ? 0
                    : Math.Round(
                        (presentCount + lateCount) *
                        100.0 / totalRecords,
                        1);

            // =====================================================
            // 只根据 Class 归类
            // 同一个班级不论有多少 Student，都只显示一行
            // =====================================================

            List<AttendanceClassSummaryViewModel>
                classSummaries =
                    monthlyRecords
                        .GroupBy(a => new
                        {
                            a.TuitionClassId,

                            ClassCode =
                                a.TuitionClass!.ClassCode,

                            SubjectCode =
                                a.TuitionClass
                                    .Subject!
                                    .SubjectCode,

                            SubjectName =
                                a.TuitionClass
                                    .Subject
                                    .Name
                        })
                        .Select(group =>
                        {
                            // 该班级当月总记录
                            int groupTotal =
                                group.Count();

                            // 该班级 Present 数量
                            int groupPresent =
                                group.Count(a =>
                                    a.Status == "Present");

                            // 该班级 Late 数量
                            int groupLate =
                                group.Count(a =>
                                    a.Status == "Late");

                            // 该班级 Absent 数量
                            int groupAbsent =
                                group.Count(a =>
                                    a.Status == "Absent");

                            // 计算该班级 Attendance Percentage
                            double percentage =
                                groupTotal == 0
                                    ? 0
                                    : Math.Round(
                                        (groupPresent +
                                         groupLate) *
                                        100.0 /
                                        groupTotal,
                                        1);

                            return new
                                AttendanceClassSummaryViewModel
                            {
                                TuitionClassId =
                                    group.Key.TuitionClassId,

                                ClassCode =
                                    group.Key.ClassCode,

                                SubjectCode =
                                    group.Key.SubjectCode,

                                SubjectName =
                                    group.Key.SubjectName,

                                TotalRecords =
                                    groupTotal,

                                PresentCount =
                                    groupPresent,

                                LateCount =
                                    groupLate,

                                AbsentCount =
                                    groupAbsent,

                                AttendancePercentage =
                                    percentage
                            };
                        })
                        .OrderBy(x =>
                            x.SubjectName)
                        .ThenBy(x =>
                            x.ClassCode)
                        .ToList();

            // 把资料放进 ViewModel
            AttendanceDashboardViewModel model =
                new AttendanceDashboardViewModel
                {
                    SelectedMonth =
                        selectedMonth,

                    SelectedYear =
                        selectedYear,

                    TotalRecords =
                        totalRecords,

                    PresentCount =
                        presentCount,

                    LateCount =
                        lateCount,

                    AbsentCount =
                        absentCount,

                    OverallPercentage =
                        overallPercentage,

                    ClassSummaries =
                        classSummaries
                };

            return View(model);
        }

        // =========================================================
        // View Details
        // 显示指定班级、月份以及日期的详细 Attendance
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Details(
            int tuitionClassId,
            int month,
            int year,
            DateTime? selectedDate)
        {
            // 取得目前登录的 User
            User? currentUser =
                await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            // 检查月份
            if (month < 1 || month > 12)
            {
                return BadRequest();
            }

            // 查询 Class 和 Subject
            TuitionClass? tuitionClass =
                await _context.TuitionClasses
                    .AsNoTracking()
                    .Include(c => c.Subject)
                    .FirstOrDefaultAsync(c =>
                        c.Id == tuitionClassId);

            if (tuitionClass == null)
            {
                return NotFound();
            }

            // Teacher 只能查看自己负责的 Class
            if (User.IsInRole("Teacher") &&
                tuitionClass.TeacherId !=
                    currentUser.Id)
            {
                return Forbid();
            }

            // 准备该班级的 Attendance 查询
            IQueryable<Attendance> query =
                _context.Attendances
                    .AsNoTracking()
                    .Include(a => a.Student)
                    .Include(a => a.TuitionClass)
                        .ThenInclude(c => c!.Subject)
                    .Where(a =>
                        a.TuitionClassId ==
                            tuitionClassId &&
                        a.AttendanceDate.Month ==
                            month &&
                        a.AttendanceDate.Year ==
                            year);

            // Student 只能查看自己的详细记录
            if (User.IsInRole("Student"))
            {
                query = query.Where(a =>
                    a.StudentId ==
                        currentUser.Id);
            }

            // 先取得整个月份的记录
            List<Attendance> monthlyRecords =
                await query
                    .OrderByDescending(a =>
                        a.AttendanceDate)
                    .ThenBy(a =>
                        a.Student!.Name)
                    .ToListAsync();

            // 取得该月份有 Attendance 的日期
            List<DateTime> availableDates =
                monthlyRecords
                    .Select(a =>
                        a.AttendanceDate.Date)
                    .Distinct()
                    .OrderBy(date => date)
                    .ToList();

            // 默认显示整个月份
            List<Attendance> displayedRecords =
                monthlyRecords;

            // 如果用户选择具体日期，只显示该日期
            if (selectedDate.HasValue)
            {
                displayedRecords =
                    monthlyRecords
                        .Where(a =>
                            a.AttendanceDate.Date ==
                            selectedDate.Value.Date)
                        .ToList();
            }

            // =====================================================
            // 统计继续计算整个月份
            // 日期筛选只影响下面的详细记录表格
            // =====================================================

            int totalRecords =
                monthlyRecords.Count;

            int presentCount =
                monthlyRecords.Count(a =>
                    a.Status == "Present");

            int lateCount =
                monthlyRecords.Count(a =>
                    a.Status == "Late");

            int absentCount =
                monthlyRecords.Count(a =>
                    a.Status == "Absent");

            double attendancePercentage =
                totalRecords == 0
                    ? 0
                    : Math.Round(
                        (presentCount + lateCount) *
                        100.0 / totalRecords,
                        1);

            AttendanceDetailsViewModel model =
                new AttendanceDetailsViewModel
                {
                    TuitionClassId =
                        tuitionClass.Id,

                    ClassCode =
                        tuitionClass.ClassCode,

                    SubjectCode =
                        tuitionClass.Subject?
                            .SubjectCode ?? "",

                    SubjectName =
                        tuitionClass.Subject?
                            .Name ?? "",

                    SelectedMonth =
                        month,

                    SelectedYear =
                        year,

                    SelectedDate =
                        selectedDate,

                    AvailableDates =
                        availableDates,

                    TotalRecords =
                        totalRecords,

                    PresentCount =
                        presentCount,

                    LateCount =
                        lateCount,

                    AbsentCount =
                        absentCount,

                    AttendancePercentage =
                        attendancePercentage,

                    Records =
                        displayedRecords
                };

            return View(model);
        }
        // =========================================================
        // Additional Feature：下载 Attendance PDF Report
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> DownloadPdf(
            int tuitionClassId,
            int month,
            int year)
        {
            // 取得目前登录用户
            User? currentUser =
                await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            // 检查月份
            if (month < 1 || month > 12)
            {
                return BadRequest();
            }

            // 查询 Class 和 Subject
            TuitionClass? tuitionClass =
                await _context.TuitionClasses
                    .AsNoTracking()
                    .Include(c => c.Subject)
                    .FirstOrDefaultAsync(c =>
                        c.Id == tuitionClassId);

            if (tuitionClass == null)
            {
                return NotFound();
            }

            // Teacher 只能下载自己负责班级的报告
            if (User.IsInRole("Teacher") &&
                tuitionClass.TeacherId != currentUser.Id)
            {
                return Forbid();
            }

            // 查询该班级和月份的 Attendance
            IQueryable<Attendance> query =
                _context.Attendances
                    .AsNoTracking()
                    .Include(a => a.Student)
                    .Include(a => a.TuitionClass)
                        .ThenInclude(c => c!.Subject)
                    .Where(a =>
                        a.TuitionClassId == tuitionClassId &&
                        a.AttendanceDate.Month == month &&
                        a.AttendanceDate.Year == year);

            // Student 只能下载自己的 Attendance
            if (User.IsInRole("Student"))
            {
                query = query.Where(a =>
                    a.StudentId == currentUser.Id);
            }

            List<Attendance> records =
                await query
                    .OrderBy(a => a.AttendanceDate)
                    .ThenBy(a => a.Student!.Name)
                    .ToListAsync();

            // 产生 PDF
            byte[] pdfBytes =
                _attendancePdfService.GenerateAttendanceReport(
                    tuitionClass,
                    month,
                    year,
                    records);

            // 文件名
            string monthName =
                new DateTime(year, month, 1)
                    .ToString("MMMM");

            string fileName =
                $"Attendance_{tuitionClass.ClassCode}_" +
                $"{monthName}_{year}.pdf";

            // 下载 PDF
            return File(
                pdfBytes,
                "application/pdf",
                fileName);
        }
        // =========================================================
        // Additional Feature：AJAX 搜索和筛选 Attendance Details
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> FilterDetails(
            int tuitionClassId,
            int month,
            int year,
            DateTime? selectedDate,
            string? search,
            string? status)
        {
            // 取得目前登录用户
            User? currentUser =
                await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return Unauthorized();
            }

            // 检查月份
            if (month < 1 || month > 12)
            {
                return BadRequest();
            }

            // 查询班级
            TuitionClass? tuitionClass =
                await _context.TuitionClasses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c =>
                        c.Id == tuitionClassId);

            if (tuitionClass == null)
            {
                return NotFound();
            }

            // Teacher 只能查询自己负责的班级
            if (User.IsInRole("Teacher") &&
                tuitionClass.TeacherId != currentUser.Id)
            {
                return Forbid();
            }

            // 准备 Attendance 查询
            IQueryable<Attendance> query =
                _context.Attendances
                    .AsNoTracking()
                    .Include(a => a.Student)
                    .Include(a => a.TuitionClass)
                        .ThenInclude(c => c!.Subject)
                    .Where(a =>
                        a.TuitionClassId == tuitionClassId &&
                        a.AttendanceDate.Month == month &&
                        a.AttendanceDate.Year == year);

            // Student 只能查询自己的 Attendance
            if (User.IsInRole("Student"))
            {
                query = query.Where(a =>
                    a.StudentId == currentUser.Id);
            }

            // 按具体日期筛选
            if (selectedDate.HasValue)
            {
                query = query.Where(a =>
                    a.AttendanceDate.Date ==
                    selectedDate.Value.Date);
            }

            // Admin 和 Teacher 可以按学生姓名或 Email 搜索
            if (!User.IsInRole("Student") &&
                !string.IsNullOrWhiteSpace(search))
            {
                string keyword =
                    search.Trim().ToLower();

                query = query.Where(a =>
                    a.Student != null &&
                    (
                        a.Student.Name.ToLower()
                            .Contains(keyword) ||

                        a.Student.Email.ToLower()
                            .Contains(keyword)
                    ));
            }

            // 按 Attendance Status 筛选
            string[] allowedStatuses =
            {
        "Present",
        "Late",
        "Absent"
    };

            if (!string.IsNullOrWhiteSpace(status) &&
                allowedStatuses.Contains(status))
            {
                query = query.Where(a =>
                    a.Status == status);
            }

            // 取得筛选结果
            List<Attendance> records =
                await query
                    .OrderByDescending(a =>
                        a.AttendanceDate)
                    .ThenBy(a =>
                        a.Student!.Name)
                    .ToListAsync();

            // 只返回表格内容，不重新加载整个页面
            return PartialView(
                "_AttendanceRows",
                records);
        }

        // =========================================================
        // Additional Feature：下载整个月份 Attendance PDF Report
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> DownloadMonthlyPdf(
            int month,
            int year)
        {
            // 取得目前登录用户
            User? currentUser =
                await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            // 检查月份
            if (month < 1 || month > 12)
            {
                return BadRequest();
            }

            // 检查年份
            if (year < 2000 || year > 2100)
            {
                return BadRequest();
            }

            // 准备整个月份的 Attendance 查询
            IQueryable<Attendance> query =
                _context.Attendances
                    .AsNoTracking()
                    .Include(a => a.Student)
                    .Include(a => a.TuitionClass)
                        .ThenInclude(c => c!.Subject)
                    .Where(a =>
                        a.AttendanceDate.Month == month &&
                        a.AttendanceDate.Year == year);

            // Teacher 只能下载自己负责班级的资料
            if (User.IsInRole("Teacher"))
            {
                query = query.Where(a =>
                    a.TuitionClass != null &&
                    a.TuitionClass.TeacherId ==
                        currentUser.Id);
            }

            // Student 只能下载自己的资料
            else if (User.IsInRole("Student"))
            {
                query = query.Where(a =>
                    a.StudentId == currentUser.Id);
            }

            // Admin 不需要额外过滤
            List<Attendance> records =
                await query
                    .OrderBy(a =>
                        a.TuitionClass!.Subject!.Name)
                    .ThenBy(a =>
                        a.TuitionClass!.ClassCode)
                    .ThenBy(a =>
                        a.AttendanceDate)
                    .ThenBy(a =>
                        a.Student!.Name)
                    .ToListAsync();

            // 产生 Monthly PDF
            byte[] pdfBytes =
                _attendancePdfService
                    .GenerateMonthlyAttendanceReport(
                        month,
                        year,
                        records);

            // 月份名称
            string monthName =
                new DateTime(year, month, 1)
                    .ToString("MMMM");

            // 根据角色决定文件名
            string roleName =
                User.IsInRole("Admin")
                    ? "Admin"
                    : User.IsInRole("Teacher")
                        ? "Teacher"
                        : "Student";

            string fileName =
                $"Monthly_Attendance_" +
                $"{roleName}_" +
                $"{monthName}_{year}.pdf";

            // 下载 PDF
            return File(
                pdfBytes,
                "application/pdf",
                fileName);
        }

        // =========================================================
        // Teacher / Admin：显示手动 Mark Attendance 页面
        // =========================================================
        [Authorize(Roles = "Admin,Teacher")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            // 取得目前登录的 User
            User? currentUser =
                await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            // 准备下拉菜单
            await PopulateDropdowns(
                currentUser);

            AttendanceCreateViewModel model =
                new AttendanceCreateViewModel
                {
                    AttendanceDate =
                        DateTime.Today,

                    Status =
                        "Present"
                };

            return View(model);
        }

        // =========================================================
        // Teacher / Admin：接收 Mark Attendance 表单
        // =========================================================
        [Authorize(Roles = "Admin,Teacher")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            AttendanceCreateViewModel model)
        {
            // 取得目前登录的 User
            User? currentUser =
                await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            // 系统允许的 Attendance Status
            string[] allowedStatuses =
            {
                "Present",
                "Absent",
                "Late"
            };

            // 检查 Status
            if (!allowedStatuses.Contains(
                    model.Status))
            {
                ModelState.AddModelError(
                    nameof(model.Status),
                    "Invalid attendance status.");
            }

            // 检查 Student
            bool studentExists =
                await _context.Users
                    .AnyAsync(u =>
                        u.Id == model.StudentId &&
                        u.Role == "Student");

            if (!studentExists)
            {
                ModelState.AddModelError(
                    nameof(model.StudentId),
                    "The selected student is invalid.");
            }

            // 查询 Class
            TuitionClass? tuitionClass =
                await _context.TuitionClasses
                    .FirstOrDefaultAsync(c =>
                        c.Id ==
                            model.TuitionClassId);

            if (tuitionClass == null)
            {
                ModelState.AddModelError(
                    nameof(model.TuitionClassId),
                    "The selected class is invalid.");
            }
            else if (
                User.IsInRole("Teacher") &&
                tuitionClass.TeacherId !=
                    currentUser.Id)
            {
                // Teacher 不可以替其他 Teacher 的 Class 点名
                return Forbid();
            }

            // 检查 Student 是否已经 Enrollment
            bool isEnrolled =
                await _context.Enrollments
                    .AnyAsync(e =>
                        e.StudentId == model.StudentId &&
                        e.TuitionClassId == model.TuitionClassId &&
                        !e.IsDropped);

            if (!isEnrolled)
            {
                ModelState.AddModelError(
                    nameof(model.StudentId),
                    "This student is not enrolled in the selected class.");
            }

            // 检查重复记录
            bool attendanceExists =
                await _context.Attendances
                    .AnyAsync(a =>
                        a.StudentId ==
                            model.StudentId &&
                        a.TuitionClassId ==
                            model.TuitionClassId &&
                        a.AttendanceDate.Date ==
                            model.AttendanceDate.Date);

            if (attendanceExists)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Attendance has already been recorded for this student, class and date.");
            }

            // 验证失败时重新准备下拉菜单
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(
                    currentUser,
                    model.StudentId,
                    model.TuitionClassId);

                return View(model);
            }

            // 建立 Attendance
            Attendance attendance =
                new Attendance
                {
                    StudentId =
                        model.StudentId,

                    TuitionClassId =
                        model.TuitionClassId,

                    AttendanceDate =
                        model.AttendanceDate.Date,

                    Status =
                        model.Status,

                    Remarks =
                        model.Remarks,

                    RecordedAt =
                        DateTime.UtcNow
                };

            _context.Attendances.Add(
                attendance);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Attendance marked successfully.";

            return RedirectToAction(
                nameof(Index),
                new
                {
                    month =
                        model.AttendanceDate.Month,

                    year =
                        model.AttendanceDate.Year
                });
        }

        // =========================================================
        // Teacher / Admin：显示修改 Attendance 页面
        // =========================================================
        [Authorize(Roles = "Teacher,Admin")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            // 取得目前登录的 User
            User? currentUser =
                await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            // 查询 Attendance
            Attendance? attendance =
                await _context.Attendances
                    .Include(a => a.Student)
                    .Include(a => a.TuitionClass)
                        .ThenInclude(c => c!.Subject)
                    .FirstOrDefaultAsync(a =>
                        a.Id == id);

            if (attendance == null)
            {
                return NotFound();
            }

            // Teacher 只能修改自己负责班级的 Attendance
            if (User.IsInRole("Teacher") &&
                attendance.TuitionClass?
                    .TeacherId != currentUser.Id)
            {
                return Forbid();
            }

            return View(attendance);
        }

        // =========================================================
        // Teacher / Admin：保存修改后的 Attendance
        // =========================================================
        [Authorize(Roles = "Teacher,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            string status,
            string? remarks)
        {
            // 取得目前登录的 User
            User? currentUser =
                await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            // 查询 Attendance
            Attendance? attendance =
                await _context.Attendances
                    .Include(a => a.Student)
                    .Include(a => a.TuitionClass)
                        .ThenInclude(c => c!.Subject)
                    .FirstOrDefaultAsync(a =>
                        a.Id == id);

            if (attendance == null)
            {
                return NotFound();
            }

            // Teacher 只能修改自己负责班级的 Attendance
            if (User.IsInRole("Teacher") &&
                attendance.TuitionClass?
                    .TeacherId != currentUser.Id)
            {
                return Forbid();
            }

            string[] allowedStatuses =
            {
                "Present",
                "Absent",
                "Late"
            };

            // 检查 Status
            if (!allowedStatuses.Contains(status))
            {
                ModelState.AddModelError(
                    "Status",
                    "Invalid attendance status.");
            }

            // 检查 Remarks 长度
            if (!string.IsNullOrWhiteSpace(
                    remarks) &&
                remarks.Length > 300)
            {
                ModelState.AddModelError(
                    "Remarks",
                    "Remarks cannot exceed 300 characters.");
            }

            // 验证失败
            if (!ModelState.IsValid)
            {
                attendance.Status =
                    status;

                attendance.Remarks =
                    remarks;

                return View(attendance);
            }

            // 更新 Attendance
            attendance.Status =
                status;

            attendance.Remarks =
                remarks;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Attendance updated successfully.";

            // 返回该月份 Attendance Records
            return RedirectToAction(
                nameof(Index),
                new
                {
                    month =
                        attendance.AttendanceDate.Month,

                    year =
                        attendance.AttendanceDate.Year
                });
        }

        // =========================================================
        // 准备 Student 和 Class 下拉菜单
        // =========================================================
        private async Task PopulateDropdowns(
            User currentUser,
            int? selectedStudentId = null,
            int? selectedClassId = null)
        {
            // 取得所有 Student
            List<User> students =
                await _context.Users
                    .AsNoTracking()
                    .Where(u =>
                        u.Role == "Student")
                    .OrderBy(u =>
                        u.Name)
                    .ToListAsync();

            ViewBag.Students =
                new SelectList(
                    students,
                    "Id",
                    "Name",
                    selectedStudentId);

            // 准备 Class 查询
            IQueryable<TuitionClass> classQuery =
                _context.TuitionClasses
                    .AsNoTracking()
                    .Include(c => c.Subject)
                    .Where(c => c.IsActive);

            // Teacher 只能选择自己的 Class
            if (User.IsInRole("Teacher"))
            {
                classQuery =
                    classQuery.Where(c =>
                        c.TeacherId ==
                            currentUser.Id);
            }

            List<TuitionClass> classes =
                await classQuery
                    .OrderBy(c =>
                        c.Subject!.SubjectCode)
                    .ThenBy(c =>
                        c.ClassCode)
                    .ToListAsync();

            // 组合下拉菜单显示名称
            var classOptions =
                classes.Select(c => new
                {
                    c.Id,

                    Name =
                        $"{c.Subject!.SubjectCode} - " +
                        $"{c.ClassCode} " +
                        $"({c.DayOfWeek} " +
                        $"{c.StartTime:HH\\:mm})"
                });

            ViewBag.Classes =
                new SelectList(
                    classOptions,
                    "Id",
                    "Name",
                    selectedClassId);
        }

        // =========================================================
        // 根据 Login Claims 找出目前登录用户
        // =========================================================
        private async Task<User?> GetCurrentUserAsync()
        {
            // 从登录 Claims 取得 Email
            string? email =
                User.FindFirstValue(
                    ClaimTypes.Email);

            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            // 根据 Email 查询 User
            return await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == email);
        }
    }
}