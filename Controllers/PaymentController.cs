using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutionCenter.Data;
using TutionCenter.Models;
using TutionCenter.Services;

namespace TutionCenter.Controllers;

[Authorize]
public class PaymentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PaymentReceiptPdfService _receiptPdfService;

    public PaymentController(
        ApplicationDbContext context,
        PaymentReceiptPdfService receiptPdfService)
    {
        _context = context;
        _receiptPdfService = receiptPdfService;
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index()
    {
        return View(await _context.Payments.Where(p => p.Status == "Paid").Include(p => p.Student).Include(p => p.Bill).OrderByDescending(p => p.PaymentDate).ToListAsync());
    }

    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Bills()
    {
        var id = StudentId();
        if (id is null) return Forbid();
        return View(await _context.Bills.Where(b => b.StudentId == id).Include(b => b.Payment).OrderByDescending(b => b.BillDate).ToListAsync());
    }

    [Authorize(Roles = "Student")]
    public async Task<IActionResult> StudentBilling()
    {
        var id = StudentId();
        if (id is null) return Forbid();
        return View(await _context.Payments.Where(p => p.StudentId == id).Include(p => p.Bill).OrderByDescending(p => p.PaymentDate).ToListAsync());
    }

    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Checkout(int id)
    {
        var bill = await UnpaidBill(id);
        if (bill is null) return NotFound();
        return View(new PaymentCheckoutViewModel { BillId = bill.Id, Description = bill.Description, Amount = bill.Amount });
    }

    [HttpPost, Authorize(Roles = "Student"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(PaymentCheckoutViewModel model)
    {
        var bill = await UnpaidBill(model.BillId);
        if (bill is null) return NotFound();
        ValidateCheckout(model);
        if (!ModelState.IsValid) { 
            model.Description = bill.Description;
            model.Amount = bill.Amount;
            return View(model);
        }
        var payment = new Payment { BillId = bill.Id, StudentId = bill.StudentId, Amount = bill.Amount, PaymentDate = DateTime.Now, PaymentMethod = model.PaymentMethod, Status = "Paid", TransactionId = "TXN" + DateTime.Now.ToString("yyyyMMddHHmmss") + Random.Shared.Next(100, 999), Remarks = "Simulated payment completed" };
        bill.Status = "Paid";
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Success), new { id = payment.Id });
    }

    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Success(int id)
    {
        var payment = await _context.Payments.Include(p => p.Bill).FirstOrDefaultAsync(p => p.Id == id && p.StudentId == StudentId());
        return payment is null ? NotFound() : View(payment);
    }

    [Authorize(Roles = "Student")]
    public async Task<IActionResult> DownloadReceipt(int id)
    {
        var studentId = StudentId();

        if (studentId is null)
            return Forbid();

        var payment = await _context.Payments
            .Include(p => p.Student)
            .Include(p => p.Bill)
            .FirstOrDefaultAsync(p =>
                p.Id == id &&
                p.StudentId == studentId &&
                p.Status == "Paid");

        if (payment is null)
            return NotFound();

        var pdf = _receiptPdfService.GenerateReceipt(payment);

        var fileName =
            $"Payment-Receipt-{payment.TransactionId}.pdf";

        return File(
            pdf,
            "application/pdf",
            fileName);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var payment = await _context.Payments.FindAsync(id);

        if (payment is null)
            return NotFound();

        ViewBag.Students = await _context.Users
            .Where(u => u.Role == "Student")
            .OrderBy(u => u.Name)
            .ToListAsync();

        return View(payment);
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Payment payment)
    {
        var existing = await _context.Payments.FindAsync(id);
        if (existing is null) return NotFound();
        existing.Remarks = payment.Remarks;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id) => (await _context.Payments.Include(p => p.Student).FirstOrDefaultAsync(p => p.Id == id)) is Payment p ? View(p) : NotFound();

    [HttpPost, ActionName("Delete"), Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var p = await _context.Payments.FindAsync(id);
        if (p is not null) { _context.Payments.Remove(p); await _context.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }


    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RevenueReport()
    {
        var payments = await _context.Payments
            .Include(p => p.Student)
            .Where(p => p.Status == "Paid")
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();

        var bills = await _context.Bills
            .AsNoTracking()
            .ToListAsync();

        // ==========================
        // Payment Summary
        // ==========================

        var paidBills =
            bills.Count(b => b.Status == "Paid");

        var unpaidBills =
            bills.Count(b => b.Status == "Unpaid");

        ViewBag.TotalPayments =
            payments.Count;

        ViewBag.TotalRevenue =
            payments.Sum(p => p.Amount);

        ViewBag.PaidPayments =
            paidBills;

        ViewBag.PendingPayments =
            unpaidBills;

        // ==========================
        // Payment Status Pie Chart
        // ==========================

        ViewBag.PaymentStatusLabels =
            JsonSerializer.Serialize(new[]
            {
            "Paid",
            "Unpaid"
            });

        ViewBag.PaymentStatusData =
            JsonSerializer.Serialize(new[]
            {
            paidBills,
            unpaidBills
            });

        // ==========================
        // Revenue by Payment Method
        // ==========================

        var groups =
            payments
                .GroupBy(p => p.PaymentMethod)
                .Select(g => new
                {
                    Method = g.Key,
                    Revenue = g.Sum(p => p.Amount)
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();

        ViewBag.PaymentMethodLabels =
            JsonSerializer.Serialize(
                groups.Select(x => x.Method));

        ViewBag.PaymentMethodData =
            JsonSerializer.Serialize(
                groups.Select(x => x.Revenue));

        return View(payments);
    }

    private int? StudentId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;
    private Task<Bill?> UnpaidBill(int id) => _context.Bills.FirstOrDefaultAsync(b => b.Id == id && b.StudentId == StudentId() && b.Status == "Unpaid");
    private void ValidateCheckout(PaymentCheckoutViewModel m)
    {
        string[] allowedPaymentMethods =
        {
            "Online Banking",
            "Credit/Debit Card",
            "E-Wallet"
        };

        if (!allowedPaymentMethods.Contains(m.PaymentMethod))
        {
            ModelState.AddModelError(
                nameof(m.PaymentMethod),
                "Please select a valid payment method."
            );

            return;
        }

        if (m.PaymentMethod == "Credit/Debit Card" &&
            (string.IsNullOrWhiteSpace(m.CardNumber) ||
             string.IsNullOrWhiteSpace(m.CardHolder) ||
             string.IsNullOrWhiteSpace(m.ExpiryDate) ||
             string.IsNullOrWhiteSpace(m.Cvv)))
        {
            ModelState.AddModelError(
                string.Empty,
                "Please complete all card details."
            );
        }

        if (m.PaymentMethod == "Online Banking" &&
            (string.IsNullOrWhiteSpace(m.Bank) ||
             string.IsNullOrWhiteSpace(m.BankLoginId)))
        {
            ModelState.AddModelError(
                string.Empty,
                "Please select a bank and enter a login ID."
            );
        }

        if (m.PaymentMethod == "E-Wallet" &&
            (string.IsNullOrWhiteSpace(m.Wallet) ||
             string.IsNullOrWhiteSpace(m.PhoneNumber)))
        {
            ModelState.AddModelError(
                string.Empty,
                "Please select a wallet and enter a phone number."
            );
        }
    }
}
