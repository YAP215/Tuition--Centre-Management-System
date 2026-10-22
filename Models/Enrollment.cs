using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TutionCenter.Models;

[Index(nameof(StudentId), nameof(TuitionClassId), IsUnique = true)]
public class Enrollment
{
    public int Id { get; set; }

    [Required]
    public int StudentId { get; set; }

    public User? Student { get; set; }

    [Required, Display(Name = "Class")]
    public int TuitionClassId { get; set; }

    public TuitionClass? TuitionClass { get; set; }

    [Display(Name = "Enrolled On")]
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    public bool IsDropped { get; set; }
}