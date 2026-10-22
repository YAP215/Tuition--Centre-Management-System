using System.ComponentModel.DataAnnotations;

namespace TutionCenter.Models;

public class PaymentCheckoutViewModel
{
    public int BillId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Please select a payment method.")]
    [RegularExpression(
        "^(Online Banking|Credit/Debit Card|E-Wallet)$",
        ErrorMessage = "Please select a valid payment method."
    )]
    public string PaymentMethod { get; set; } = string.Empty;

    public string? Bank { get; set; }
    public string? BankLoginId { get; set; }

    [StringLength(19, MinimumLength = 16)]
    public string? CardNumber { get; set; }
    public string? CardHolder { get; set; }
    public string? ExpiryDate { get; set; }
    [StringLength(4, MinimumLength = 3)]
    public string? Cvv { get; set; }

    public string? Wallet { get; set; }
    [Phone]
    public string? PhoneNumber { get; set; }
}
