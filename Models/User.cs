using System.ComponentModel.DataAnnotations;

namespace TutionCenter.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), StringLength(500)]
        public string Password { get; set; } = string.Empty;

        [Required, RegularExpression("Admin|Teacher|Student")]
        public string Role { get; set; } = string.Empty;

        public bool IsVerified { get; set; } = false;

        public string? ResetToken { get; set; }

        public DateTime? ResetTokenExpiresAt { get; set; }

        public string? EmailVerificationToken { get; set; }

        public DateTime? EmailVerificationTokenExpiresAt { get; set; }

        public int FailedLoginAttempts { get; set; } = 0;

        public DateTime? LockoutEnd { get; set; }

        [Phone]
        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [RegularExpression(
            "^(Male|Female|Other)$",
            ErrorMessage = "Please select a valid gender."
        )]
        public string? Gender { get; set; }

        public string? ProfileImage { get; set; }

        public ICollection<TuitionClass> TeachingClasses { get; set; } = new List<TuitionClass>();
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        // 学生的出席记录
        public ICollection<Attendance> Attendances { get; set; }
            = new List<Attendance>();

        // Teacher 或 Admin 建立的签到时段
        public ICollection<AttendanceSession> CreatedAttendanceSessions { get; set; }
            = new List<AttendanceSession>();

    }

}
