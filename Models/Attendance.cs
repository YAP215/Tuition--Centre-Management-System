using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace TutionCenter.Models
{
    // 防止同一个学生在同一个班级、同一天被重复记录
    [Index(
        nameof(StudentId),
        nameof(TuitionClassId),
        nameof(AttendanceDate),
        IsUnique = true)]
    public class Attendance
    {
        // Attendance 记录编号
        public int Id { get; set; }

        // 学生编号
        [Required]
        [Display(Name = "Student")]
        public int StudentId { get; set; }

        // 连接 User Table
        public User? Student { get; set; }

        // 班级编号
        [Required]
        [Display(Name = "Class")]
        public int TuitionClassId { get; set; }

        // 连接 TuitionClass Table
        public TuitionClass? TuitionClass { get; set; }

        // 出席日期
        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Attendance Date")]
        public DateTime AttendanceDate { get; set; } = DateTime.Today;

        // Present、Absent 或 Late
        [Required]
        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Present";

        // 备注，可不填写
        [StringLength(300)]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        // 记录建立时间
        [Display(Name = "Recorded At")]
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

        // Student 使用签到码签到的实际时间
        [Display(Name = "Check-in Time")]
        public DateTime? CheckInTime { get; set; }

        // 如果 Attendance 是通过签到码产生，这里会保存签到时段
        public int? AttendanceSessionId { get; set; }
        public AttendanceSession? AttendanceSession { get; set; }
    }
}