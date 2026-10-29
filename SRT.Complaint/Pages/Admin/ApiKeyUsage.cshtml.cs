#nullable enable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;

namespace SRT.Complaint.Pages.Admin;

[Authorize(Policy = "SuperAdmin")]
public class ApiKeyUsageModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public int    KeyId       { get; set; }
    [BindProperty(SupportsGet = true)] public int    CurrentPage { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public string? ResultType { get; set; }
    [BindProperty(SupportsGet = true)] public string? Endpoint { get; set; }

    public const int PageSize = 50;

    public ApiKey?                    ApiKey     { get; private set; }
    public IReadOnlyList<ApiRequestLog> Logs     { get; private set; } = [];
    public int TotalCount  { get; private set; }
    public int TotalPages  { get; private set; }
    public int PageStart   => TotalCount == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
    public int PageEnd     => Math.Min(CurrentPage * PageSize, TotalCount);

    // Stats
    public int SuccessCount  { get; private set; }
    public int ErrorCount    { get; private set; }
    public double? AvgMs     { get; private set; }
    public double ErrorRate => TotalCount == 0 ? 0 : Math.Round((double)ErrorCount / TotalCount * 100, 1);
    public IReadOnlyList<string> Endpoints { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Title"] = "API Key Usage";

        ApiKey = await db.ApiKeys.Include(k => k.Partner).Include(k => k.Scopes).FirstOrDefaultAsync(k => k.Id == KeyId);
        if (ApiKey == null) return NotFound();

        Endpoints = await db.ApiRequestLogs.Where(l => l.ApiKeyId == KeyId)
            .Select(l => l.Endpoint).Distinct().OrderBy(x => x).ToListAsync();

        var q = db.ApiRequestLogs.AsNoTracking().Where(l => l.ApiKeyId == KeyId);
        if (FromDate.HasValue) q = q.Where(x => x.CreatedAt >= FromDate.Value.Date.ToUniversalTime());
        if (ToDate.HasValue) q = q.Where(x => x.CreatedAt < ToDate.Value.Date.AddDays(1).ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(Endpoint)) q = q.Where(x => x.Endpoint == Endpoint);
        if (ResultType == "success") q = q.Where(x => x.ResponseStatus >= 200 && x.ResponseStatus < 400);
        if (ResultType == "error") q = q.Where(x => x.ResponseStatus >= 400);

        TotalCount = await q.CountAsync();
        TotalPages = (int)Math.Ceiling((double)TotalCount / PageSize);
        if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

        Logs = await q
            .OrderByDescending(l => l.CreatedAt)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var stats = await q
            .GroupBy(l => 1)
            .Select(g => new
            {
                Success = g.Count(l => l.ResponseStatus >= 200 && l.ResponseStatus < 300),
                Error   = g.Count(l => l.ResponseStatus >= 400),
                AvgMs   = (double?)g.Average(l => l.ResponseMs)
            })
            .FirstOrDefaultAsync();

        if (stats != null)
        {
            SuccessCount = stats.Success;
            ErrorCount   = stats.Error;
            AvgMs        = stats.AvgMs.HasValue ? Math.Round(stats.AvgMs.Value, 0) : null;
        }

        return Page();
    }
}
