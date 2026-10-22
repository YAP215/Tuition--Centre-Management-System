using System.ComponentModel.DataAnnotations;

namespace TutionCenter.Models;

public class EnrollmentCreateViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Please select a student.")]
    [Display(Name = "Student")]
    public int StudentId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select a class.")]
    [Display(Name = "Class")]
    public int TuitionClassId { get; set; }
}
