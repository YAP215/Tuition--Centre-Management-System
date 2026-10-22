using System.ComponentModel.DataAnnotations;

using System.ComponentModel.DataAnnotations.Schema;

namespace TutionCenter.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int? BillId { get; set; }
        public Bill? Bill { get; set; }
        [Required]
        public int StudentId {  get; set; }

        public User? Student {  get; set; }

        [Required]
        [Range(0.01, 100000)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; } = DateTime.Today;

        [Required]
        public string PaymentMethod { get; set; } = "Cash";

        [Required]
        public string Status { get; set; } = "Paid";

        public string? Remarks { get; set; }

        [Required]
        [StringLength(50)]
        public string TransactionId { get; set; } = string.Empty;

    }
}
