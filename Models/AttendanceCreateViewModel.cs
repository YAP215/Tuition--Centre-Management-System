using System.ComponentModel.DataAnnotations;

namespace TutionCenter.Models
{
    // Mark Attendance 页面使用的 ViewModel
    public class AttendanceCreateViewModel
    {
        // 选择学生
        [Required(ErrorMessage = "Please select a student.")]
        [Display(Name = "Student")]
        public int StudentId { get; set; }

        // 选择班级
        [Required(ErrorMessage = "Please select a class.")]
        [Display(Name = "Class")]
        public int TuitionClassId { get; set; }

        // 出席日期
        [Required(ErrorMessage = "Attendance date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Attendance Date")]
        public DateTime AttendanceDate { get; set; } = DateTime.Today;

        // Present、Absent 或 Late
        [Required(ErrorMessage = "Please select an attendance status.")]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Present";

        // 备注，可不填写
        [StringLength(300)]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }
    }
}