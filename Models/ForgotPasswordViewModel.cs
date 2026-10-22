using System.ComponentModel.DataAnnotations;

namespace TutionCenter.Models
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}