using System.ComponentModel.DataAnnotations;

namespace TutionCenter.Models
{
    // =========================================================
    // Attendance Records 首页使用的 ViewModel
    // =========================================================
    public class AttendanceDashboardViewModel
    {
        // 当前选择的月份
        public int SelectedMonth { get; set; }

        // 当前选择的年份
        public int SelectedYear { get; set; }

        // 当前月份全部 Attendance 记录数量
        public int TotalRecords { get; set; }

        // 当前月份 Present 数量
        public int PresentCount { get; set; }

        // 当前月份 Late 数量
        public int LateCount { get; set; }

        // 当前月份 Absent 数量
        public int AbsentCount { get; set; }

        // 当前月份整体 Attendance Percentage
        public double OverallPercentage { get; set; }

        // 按 Subject + Class 合并后的统计资料
        public List<AttendanceClassSummaryViewModel> ClassSummaries
        {
            get;
            set;
        } = new List<AttendanceClassSummaryViewModel>();
    }

    // =========================================================
    // Attendance Records 首页每一个 Subject / Class 的统计
    // =========================================================
    public class AttendanceClassSummaryViewModel
    {
        // 班级 Id
        public int TuitionClassId { get; set; }

        // 班级编号，例如 C001
        public string ClassCode { get; set; } = string.Empty;

        // 科目编号，例如 WBP101
        public string SubjectCode { get; set; } = string.Empty;

        // 科目名称，例如 Web Base
        public string SubjectName { get; set; } = string.Empty;

        // 该月份这个班级的全部 Attendance 数量
        public int TotalRecords { get; set; }

        // Present 数量
        public int PresentCount { get; set; }

        // Late 数量
        public int LateCount { get; set; }

        // Absent 数量
        public int AbsentCount { get; set; }

        // Attendance Percentage
        public double AttendancePercentage { get; set; }
    }

    // =========================================================
    // View Details 页面使用的 ViewModel
    // =========================================================
    public class AttendanceDetailsViewModel
    {
        // 班级 Id
        public int TuitionClassId { get; set; }

        // 班级编号
        public string ClassCode { get; set; } = string.Empty;

        // 科目编号
        public string SubjectCode { get; set; } = string.Empty;

        // 科目名称
        public string SubjectName { get; set; } = string.Empty;

        // 当前选择的月份
        public int SelectedMonth { get; set; }

        // 当前选择的年份
        public int SelectedYear { get; set; }

        // 当前选择的具体日期
        // null 代表 All Dates
        [DataType(DataType.Date)]
        public DateTime? SelectedDate { get; set; }

        // 该月份中可以选择的 Attendance 日期
        public List<DateTime> AvailableDates { get; set; }
            = new List<DateTime>();

        // Details 统计
        public int TotalRecords { get; set; }

        public int PresentCount { get; set; }

        public int LateCount { get; set; }

        public int AbsentCount { get; set; }

        public double AttendancePercentage { get; set; }

        // 详细 Attendance Records
        public List<Attendance> Records { get; set; }
            = new List<Attendance>();
    }
}