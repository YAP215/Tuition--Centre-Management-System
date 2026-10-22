using System.ComponentModel.DataAnnotations;

namespace TutionCenter.Models
{
    public class AttendanceCodeViewModel
    {
        [Required(ErrorMessage = "Please enter the attendance code.")]
        [StringLength(
            6,
            MinimumLength = 6,
            ErrorMessage = "Attendance code must contain exactly 6 digits.")]
        [RegularExpression(
            @"^\d{6}$",
            ErrorMessage = "Attendance code must contain numbers only.")]
        [Display(Name = "Attendance Code")]
        public string AttendanceCode { get; set; } = string.Empty;
    }
}