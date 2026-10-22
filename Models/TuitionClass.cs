using System.ComponentModel.DataAnnotations;

namespace TutionCenter.Models;

public class TuitionClass : IValidatableObject
{
    public int Id { get; set; }

    [Required, StringLength(30), Display(Name = "Class Code")]
    public string ClassCode { get; set; } = string.Empty;

    [Required, Display(Name = "Subject")]
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    [Display(Name = "Teacher")]
    public int? TeacherId { get; set; }
    public User? Teacher { get; set; }

    [Required]
    [RegularExpression(
        "^(Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)$",
        ErrorMessage = "Please select a valid day."
    )]
    [Display(Name = "Day")]
    public string DayOfWeek { get; set; } = string.Empty;

    [DataType(DataType.Time), Display(Name = "Start Time")]
    public TimeOnly StartTime { get; set; }

    [DataType(DataType.Time), Display(Name = "End Time")]
    public TimeOnly EndTime { get; set; }

    [Required, StringLength(50)]
    public string Room { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "Capacity must be between 1 and 100.")]
    [Display(Name = "Maximum Students")]
    public int Capacity { get; set; } = 20;

    [Display(Name = "Open for enrollment")]
    public bool IsActive { get; set; } = true;

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    // 这个班级的出席记录
    public ICollection<Attendance> Attendances { get; set; }
        = new List<Attendance>();

    // 一个班级可以产生多个签到时段
    public ICollection<AttendanceSession> AttendanceSessions { get; set; }
        = new List<AttendanceSession>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndTime <= StartTime)
            yield return new ValidationResult("End time must be later than start time.", new[] { nameof(EndTime) });
    }
}
