using System.ComponentModel.DataAnnotations;

namespace TutionCenter.Models
{
    // Profile 页面专用 ViewModel
    public class ProfileViewModel
    {
        // 用户 Id
        public int Id { get; set; }

        // 用户姓名
        [Required(ErrorMessage = "Name is required.")]
        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string Name { get; set; } = string.Empty;

        // 用户 Email
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        // 用户身份，例如 Admin、Teacher、Student
        [Display(Name = "Role")]
        public string Role { get; set; } = string.Empty;

        // 用户电话号码
        [Phone(ErrorMessage = "Invalid phone number.")]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        // 用户出生日期
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        // 用户性别
        [Display(Name = "Gender")]
        public string? Gender { get; set; }

        // 用户头像路径
        [Display(Name = "Profile Image")]
        public string? ProfileImage { get; set; }
    }
}