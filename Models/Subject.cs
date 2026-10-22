using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TutionCenter.Models;

public class Subject
{
    public int Id { get; set; }

    [Required, StringLength(20), Display(Name = "Subject Code")]
    public string SubjectCode { get; set; } = string.Empty;

    [Required, StringLength(100), Display(Name = "Subject Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Range(0, 10000), DataType(DataType.Currency), Display(Name = "Monthly Fee (RM)")]
    [Column(TypeName = "decimal(10,2)")]
    public decimal MonthlyFee { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public ICollection<TuitionClass> Classes { get; set; } = new List<TuitionClass>();
}
