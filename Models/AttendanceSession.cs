using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TutionCenter.Models
{
    // 确保每个签到码不会重复
    [Index(nameof(AttendanceCode), IsUnique = true)]
    public class AttendanceSession
    {
        public int Id { get; set; }

        // 签到码属于哪个班级
        [Required]
        [Display(Name = "Class")]
        public int TuitionClassId { get; set; }

        public TuitionClass? TuitionClass { get; set; }

        // 6 位数字签到码
        [Required]
        [StringLength(6, MinimumLength = 6)]
        [RegularExpression(
            @"^\d{6}$",
            ErrorMessage = "Attendance code must contain exactly 6 digits.")]
        [Display(Name = "Attendance Code")]
        public string AttendanceCode { get; set; } = string.Empty;

        // 签到码建立时间
        [Display(Name = "Created Time")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // 签到码过期时间
        [Display(Name = "Expiry Time")]
        public DateTime ExpiresAt { get; set; }

        // 签到码是否有效
        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        // 建立签到码的 Teacher 或 Admin
        [Required]
        [Display(Name = "Created By")]
        public int CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }

        // 使用这个签到码产生的 Attendance
        public ICollection<Attendance> Attendances { get; set; }
            = new List<Attendance>();
    }
}