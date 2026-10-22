using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutionCenter.Data;
using TutionCenter.Models;

namespace TutionCenter.Controllers;

[Authorize(Roles = "Admin")]
public class SubjectController : Controller
{
    private const int PageSize = 5;
    private readonly ApplicationDbContext _context;

    public SubjectController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index(string? search, string? status, string? sort, int page = 1)
        => View(await BuildList(search, status, sort, page));

    [HttpGet]
    public async Task<IActionResult> List(string? search, string? status, string? sort, int page = 1)
        => PartialView("_SubjectTable", await BuildList(search, status, sort, page));

    public IActionResult Create() => View(new Subject());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Subject subject)
    {
        if (await _context.Subjects.AnyAsync(s => s.SubjectCode == subject.SubjectCode))
            ModelState.AddModelError(nameof(subject.SubjectCode), "This subject code is already in use.");
        if (!ModelState.IsValid) return View(subject);

        subject.SubjectCode = subject.SubjectCode.Trim().ToUpperInvariant();
        subject.Name = subject.Name.Trim();
        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Subject created successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var subject = await _context.Subjects.FindAsync(id);
        return subject is null ? NotFound() : View(subject);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Subject subject)
    {
        if (id != subject.Id) return BadRequest();
        if (await _context.Subjects.AnyAsync(s => s.Id != id && s.SubjectCode == subject.SubjectCode))
            ModelState.AddModelError(nameof(subject.SubjectCode), "This subject code is already in use.");
        if (!ModelState.IsValid) return View(subject);

        subject.SubjectCode = subject.SubjectCode.Trim().ToUpperInvariant();
        subject.Name = subject.Name.Trim();
        _context.Update(subject);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Subject updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var subject = await _context.Subjects.Include(s => s.Classes).FirstOrDefaultAsync(s => s.Id == id);
        if (subject is null) return NotFound();
        if (subject.Classes.Any())
        {
            TempData["Error"] = "A subject with classes cannot be deleted. Mark it inactive instead.";
            return RedirectToAction(nameof(Index));
        }
        _context.Subjects.Remove(subject);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Subject deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<SubjectListViewModel> BuildList(string? search, string? status, string? sort, int page)
    {
        search = search?.Trim() ?? string.Empty;
        status = status is "active" or "inactive" ? status : "all";
        sort = sort is "name" or "fee" or "code_desc" ? sort : "code";
        var query = _context.Subjects.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.SubjectCode.Contains(search) || s.Name.Contains(search));
        if (status == "active") query = query.Where(s => s.IsActive);
        if (status == "inactive") query = query.Where(s => !s.IsActive);
        query = sort switch
        {
            "name" => query.OrderBy(s => s.Name),
            "fee" => query.OrderByDescending(s => s.MonthlyFee),
            "code_desc" => query.OrderByDescending(s => s.SubjectCode),
            _ => query.OrderBy(s => s.SubjectCode)
        };
        var count = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);
        return new SubjectListViewModel
        {
            Subjects = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(),
            Search = search, Status = status, Sort = sort, Page = page, TotalPages = totalPages
        };
    }
}
