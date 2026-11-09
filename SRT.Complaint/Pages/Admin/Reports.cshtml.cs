#nullable enable
using ClosedXML.Excel;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;
using SRT.Complaint.Services;
using System.Security.Claims;

namespace SRT.Complaint.Pages.Admin;

[Authorize(Policy = "SuperAdmin")]
public class ReportsModel(AppDbContext db, IPdfExportService pdfExportService, IAuditService auditService) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Priority { get; set; }

    public IReadOnlyList<CategoryStat>  ByCategorySeries { get; private set; } = [];
    public IReadOnlyList<StatusStat>    ByStatusSeries   { get; private set; } = [];
    public List<string>                 MonthlyLabels    { get; private set; } = [];
    public List<int>                    MonthlyData      { get; private set; } = [];
    public IReadOnlyList<StaffStat>     TopStaff         { get; private set; } = [];
    public int TotalAll     { get; private set; }
    public int TotalClosed  { get; private set; }
    public int TotalOpen    { get; private set; }
    public int TotalBreached { get; private set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "รายงาน";
        await LoadStatsAsync();
    }

    public async Task<IActionResult> OnPostExportAsync(string format = "excel")
    {
        var complaints = await BuildFilteredQuery()
            .Include(c => c.Category)
            .Include(c => c.SubCategory)
            .Include(c => c.AssignedTo)
            .OrderByDescending(c => c.CreatedAt)
            .Take(5000)
            .ToListAsync();

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            var pdf = pdfExportService.GenerateComplaintReportPdf(complaints, User.Identity?.Name);
            await AuditExportAsync("pdf", complaints.Count);
            return File(pdf, "application/pdf", $"complaints_{stamp}.pdf");
        }

        if (string.Equals(format, "word", StringComparison.OrdinalIgnoreCase))
        {
            var word = BuildWordDocument(complaints);
            await AuditExportAsync("word", complaints.Count);
            return File(word, "application/msword", $"complaints_{stamp}.doc");
        }

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("เรื่องร้องเรียน");

        var headers = new[]
        {
            "เลขที่อ้างอิง", "หมวดหมู่", "หัวข้อย่อย", "ฝ่ายรับผิดชอบ", "สถานะ", "ความสำคัญ",
            "ผู้ร้องเรียน", "โทรศัพท์", "อีเมล", "วัตถุประสงค์",
            "รายละเอียด", "เจ้าหน้าที่ผู้รับผิดชอบ", "SLA Deadline",
            "SLA เกินกำหนด", "วันที่รับเรื่อง", "วันที่ปิด"
        };
        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
            ws.Cell(1, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1A4A9A");
            ws.Cell(1, c + 1).Style.Font.FontColor = XLColor.White;
        }

        for (int row = 0; row < complaints.Count; row++)
        {
            var r = row + 2;
            var comp = complaints[row];
            ws.Cell(r, 1).Value  = comp.ReferenceNumber;
            ws.Cell(r, 2).Value  = comp.Category?.Name ?? "";
            ws.Cell(r, 3).Value  = comp.SubCategory?.Name ?? "";
            ws.Cell(r, 4).Value  = comp.ResponsibleDepartment ?? comp.Category?.DepartmentName ?? "";
            ws.Cell(r, 5).Value  = comp.Status;
            ws.Cell(r, 6).Value  = comp.Priority;
            ws.Cell(r, 7).Value  = comp.ReporterName;
            ws.Cell(r, 8).Value  = comp.ReporterPhone;
            ws.Cell(r, 9).Value  = comp.ReporterEmail ?? "";
            ws.Cell(r, 10).Value = ComplaintSubmissionPurposes.Label(comp.SubmissionPurpose);
            ws.Cell(r, 11).Value = comp.Description;
            ws.Cell(r, 12).Value = comp.AssignedTo?.FullName ?? "";
            ws.Cell(r, 13).Value = comp.SlaDeadline.HasValue
                ? comp.SlaDeadline.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "";
            ws.Cell(r, 14).Value = comp.SlaBreached ? "ใช่" : "ไม่";
            ws.Cell(r, 15).Value = comp.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            ws.Cell(r, 16).Value = comp.ClosedAt.HasValue
                ? comp.ClosedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "";
        }

        ws.Columns().AdjustToContents();
        ws.Column(11).Width = 50;
        ws.Column(11).Style.Alignment.WrapText = true;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        await AuditExportAsync("excel", complaints.Count);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"complaints_{stamp}.xlsx");
    }

    private Task AuditExportAsync(string format, int count) => auditService.LogAsync(
        "ExportComplaintReport",
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
        User.FindFirstValue("EmployeeCode"),
        "Complaint",
        null,
        new { format, count, containsPersonalData = true, FromDate, ToDate, Status, Priority },
        HttpContext.Connection.RemoteIpAddress?.ToString());

    private static byte[] BuildWordDocument(IReadOnlyList<Models.Complaint> complaints)
    {
        static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "-");

        var html = new StringBuilder();
        html.Append("<html><head><meta charset='utf-8'><style>")
            .Append("body{font-family:'Sarabun','Tahoma',sans-serif;font-size:11pt;color:#1f2937}")
            .Append("h1{color:#0D2E6E}table{width:100%;border-collapse:collapse;margin-bottom:16px;page-break-inside:avoid}")
            .Append("th,td{border:1px solid #d1d5db;padding:6px;text-align:left;vertical-align:top}th{width:22%;background:#f3f4f6}")
            .Append("</style></head><body><h1>รายงานรายละเอียดเรื่องร้องเรียน</h1>")
            .Append($"<p>จำนวน {complaints.Count:N0} รายการ · ส่งออกเมื่อ {DateTime.Now:dd/MM/yyyy HH:mm}</p>");

        foreach (var complaint in complaints)
        {
            html.Append("<table>")
                .Append($"<tr><th>เลขที่อ้างอิง</th><td>{Encode(complaint.ReferenceNumber)}</td><th>วันที่รับเรื่อง</th><td>{complaint.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm}</td></tr>")
                .Append($"<tr><th>ประเภทเรื่อง</th><td>{Encode(complaint.Category?.Name)}</td><th>หัวข้อย่อย</th><td>{Encode(complaint.SubCategory?.Name)}</td></tr>")
                .Append($"<tr><th>ฝ่ายรับผิดชอบ</th><td>{Encode(complaint.ResponsibleDepartment ?? complaint.Category?.DepartmentName)}</td><th>สถานะ</th><td>{Encode(complaint.Status)}</td></tr>")
                .Append($"<tr><th>ผู้ร้อง</th><td>{Encode(complaint.ReporterName)}</td><th>โทรศัพท์</th><td>{Encode(complaint.ReporterPhone)}</td></tr>")
                .Append($"<tr><th>อีเมล</th><td>{Encode(complaint.ReporterEmail)}</td><th>วัตถุประสงค์</th><td>{Encode(ComplaintSubmissionPurposes.Label(complaint.SubmissionPurpose))}</td></tr>")
                .Append($"<tr><th>รายละเอียด</th><td colspan='3'>{Encode(complaint.Description).Replace("\n", "<br>")}</td></tr>")
                .Append("</table>");
        }

        html.Append("</body></html>");
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(html.ToString())).ToArray();
    }

    private async Task LoadStatsAsync()
    {
        var filtered = BuildFilteredQuery();
        TotalAll      = await filtered.CountAsync();
        TotalClosed   = await filtered.CountAsync(c => c.Status == "Closed");
        TotalOpen     = await filtered.CountAsync(c => c.Status != "Closed" && c.Status != "Rejected");
        TotalBreached = await filtered.CountAsync(c => c.SlaBreached && c.Status != "Closed" && c.Status != "Rejected");

        // By category
        var catRaw = await filtered
            .Join(db.ComplaintCategories, c => c.CategoryId, cat => cat.Id, (c, cat) => cat.Name)
            .GroupBy(name => name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();
        ByCategorySeries = catRaw
            .Select(x => new CategoryStat(x.Name ?? "ไม่ระบุ", x.Count))
            .ToList();

        // By status
        var statusRaw = await filtered
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        ByStatusSeries = statusRaw
            .Select(x => new StatusStat(x.Status, x.Count))
            .ToList();

        // Monthly (last 12 months)
        var cutoff = DateTime.UtcNow.AddMonths(-11);
        var monthly = await filtered
            .Where(c => c.CreatedAt >= cutoff)
            .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        for (int m = 11; m >= 0; m--)
        {
            var d = DateTime.UtcNow.AddMonths(-m);
            MonthlyLabels.Add(d.ToString("MM/yy"));
            var found = monthly.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month);
            MonthlyData.Add(found?.Count ?? 0);
        }

        // Top staff (by closed cases, this month)
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var staffStats = await filtered
            .Where(c => c.Status == "Closed" && c.ClosedAt >= monthStart && c.AssignedToId != null)
            .GroupBy(c => c.AssignedToId!.Value)
            .Select(g => new { StaffId = g.Key, Closed = g.Count() })
            .OrderByDescending(x => x.Closed)
            .Take(8)
            .ToListAsync();

        var staffIds = staffStats.Select(x => x.StaffId).ToList();
        var staffNames = staffIds.Any()
            ? await db.StaffUsers.Where(u => staffIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName)
            : new Dictionary<int, string>();

        TopStaff = staffStats
            .Select(x => new StaffStat(x.StaffId, staffNames.GetValueOrDefault(x.StaffId, "—"), x.Closed))
            .ToList();
    }

    private IQueryable<Models.Complaint> BuildFilteredQuery()
    {
        var query = db.Complaints.AsQueryable();
        if (FromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(FromDate.Value.Date, DateTimeKind.Local).ToUniversalTime();
            query = query.Where(x => x.CreatedAt >= fromUtc);
        }
        if (ToDate.HasValue)
        {
            var toUtcExclusive = DateTime.SpecifyKind(ToDate.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
            query = query.Where(x => x.CreatedAt < toUtcExclusive);
        }
        if (!string.IsNullOrWhiteSpace(Status)) query = query.Where(x => x.Status == Status);
        if (!string.IsNullOrWhiteSpace(Priority)) query = query.Where(x => x.Priority == Priority);
        return query;
    }
}

public record CategoryStat(string Name, int Count);
public record StatusStat(string Status, int Count);
public record StaffStat(int StaffId, string FullName, int ClosedThisMonth);
