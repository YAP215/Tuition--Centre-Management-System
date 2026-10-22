using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TutionCenter.Models;

[Index(nameof(StudentId), nameof(EnrollmentId), nameof(BillingPeriod), IsUnique = true)]
public class Bill
{
    public int Id { get; set; }

    [Required]
    public int StudentId { get; set; }

    public User? Student { get; set; }

    [Required]
    public int EnrollmentId { get; set; }

    public Enrollment? Enrollment { get; set; }

    [Required]
    [StringLength(200)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 100000)]
    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime BillDate { get; set; } = DateTime.Today;

    [Required]
    [DataType(DataType.Date)]
    public DateTime DueDate { get; set; }

    [Required]
    [StringLength(7)]
    public string BillingPeriod { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = "Unpaid";

    public Payment? Payment { get; set; }
}
