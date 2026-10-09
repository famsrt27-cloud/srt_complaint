#nullable enable
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;
using SRT.Complaint.Models.Corruption;

namespace SRT.Complaint.Pages.Admin;

[Authorize(Policy = "SuperAdmin")]
public class AuditLogModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public string?   ActionFilter     { get; set; }
    [BindProperty(SupportsGet = true)] public string?   ActorCodeFilter  { get; set; }
    [BindProperty(SupportsGet = true)] public string?   EntityTypeFilter { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? FromDate         { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate           { get; set; }
    [BindProperty(SupportsGet = true)] public int       CurrentPage      { get; set; } = 1;

    public const int PageSize = 30;
    public IReadOnlyList<AuditLog> Logs      { get; private set; } = [];
    public Dictionary<int, string> ActorNames { get; private set; } = new();
    public int  TotalCount { get; private set; }
    public int  TotalPages { get; private set; }
    public int  PageStart  => TotalCount == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
    public int  PageEnd    => Math.Min(CurrentPage * PageSize, TotalCount);
    public IReadOnlyList<string> DistinctEntityTypes { get; private set; } = [];

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Audit Log";

        DistinctEntityTypes = await db.AuditLogs
            .Where(a => a.EntityType != null)
            .Select(a => a.EntityType!)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync();

        var q = db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(ActionFilter))
            q = q.Where(a => a.Action.Contains(ActionFilter.Trim()));
        if (!string.IsNullOrWhiteSpace(ActorCodeFilter))
            q = q.Where(a => a.ActorCode != null && a.ActorCode.Contains(ActorCodeFilter.Trim()));
        if (!string.IsNullOrWhiteSpace(EntityTypeFilter))
            q = q.Where(a => a.EntityType == EntityTypeFilter);
        if (FromDate.HasValue)
            q = q.Where(a => a.CreatedAt >= FromDate.Value.ToUniversalTime());
        if (ToDate.HasValue)
            q = q.Where(a => a.CreatedAt <= ToDate.Value.AddDays(1).ToUniversalTime());

        TotalCount = await q.CountAsync();
        TotalPages = (int)Math.Ceiling((double)TotalCount / PageSize);
        if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

        Logs = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var actorIds = Logs.Where(l => l.ActorId.HasValue).Select(l => l.ActorId!.Value).Distinct().ToList();
        ActorNames = actorIds.Count > 0
            ? await db.StaffUsers.Where(u => actorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName)
            : new Dictionary<int, string>();
    }

    // ── Label helpers ─────────────────────────────────────────────────────────

    public static string FormatAction(string action) => action switch
    {
        "ComplaintSubmitted"         => "ยื่นเรื่องร้องเรียน",
        "UpdateStatus"               => "อัปเดตสถานะเรื่อง",
        "ClaimCase"                  => "รับเรื่องดำเนินการ",
        "TransferCase"               => "โอนเรื่องให้เจ้าหน้าที่อื่น",
        "AddNote"                    => "เพิ่มบันทึก",
        "CorruptionReportSubmitted"  => "แจ้งเบาะแสทุจริต",
        "UpdateCorruptionStatus"     => "อัปเดตสถานะเรื่องทุจริต",
        "ClaimCorruptionCase"        => "รับเรื่องทุจริต",
        "DecryptReporterInfo"        => "ขอดูข้อมูลผู้แจ้งเบาะแส",
        "AddInvestigationLog"        => "เพิ่มบันทึกการสอบสวน",
        "CreateStaffUser"            => "สร้างบัญชีเจ้าหน้าที่",
        "EditStaffUser"              => "แก้ไขข้อมูลเจ้าหน้าที่",
        "ResetPassword"              => "รีเซ็ตรหัสผ่าน",
        "ActivateStaffUser"          => "เปิดใช้งานบัญชี",
        "DeactivateStaffUser"        => "ปิดใช้งานบัญชี",
        "CreateApiKey"               => "สร้าง API Key",
        "RevokeApiKey"               => "ยกเลิก API Key",
        "UpdateSlaSettings"          => "อัปเดตการตั้งค่า SLA",
        "CreateCategory"             => "สร้างหมวดหมู่",
        "EditCategory"               => "แก้ไขหมวดหมู่",
        "ActivateCategory"           => "เปิดใช้งานหมวดหมู่",
        "DeactivateCategory"         => "ปิดใช้งานหมวดหมู่",
        "UpdateNotificationTemplate" => "แก้ไข Template การแจ้งเตือน",
        "DeleteWebhook"              => "ลบ Webhook",
        _                            => action
    };

    public static string EntityTypeLabel(string? e) => e switch
    {
        "Complaint"       => "เรื่องร้องเรียน",
        "CorruptionReport"=> "เรื่องทุจริต",
        "StaffUser"       => "เจ้าหน้าที่",
        "ApiKey"          => "API Key",
        "ComplaintCategory" => "หมวดหมู่",
        "SlaConfig"       => "SLA",
        "NotificationTemplate" => "Template แจ้งเตือน",
        "Webhook"         => "Webhook",
        null or ""        => "",
        _                 => e
    };

    public static string FormatDetail(string action, string? detailJson)
    {
        if (string.IsNullOrWhiteSpace(detailJson)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(detailJson);
            var root = doc.RootElement;

            string? Get(params string[] keys)
            {
                foreach (var k in keys)
                    if (root.TryGetProperty(k, out var el) && el.ValueKind != JsonValueKind.Null)
                        return el.ToString();
                return null;
            }

            var parts = new List<string>();
            void Add(string label, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    parts.Add($"{label}: {value}");
            }

            switch (action)
            {
                case "ComplaintSubmitted":
                case "CorruptionReportSubmitted":
                    Add("เลขที่", Get("refNum", "referenceNumber", "ReferenceNumber"));
                    break;

                case "UpdateStatus":
                    Add("จาก", StatusLabel(Get("oldStatus")));
                    Add("เป็น", StatusLabel(Get("newStatus")));
                    break;

                case "UpdateCorruptionStatus":
                    Add("สถานะใหม่", StatusLabel(Get("newStatus")));
                    break;

                case "TransferCase":
                    Add("โอนให้ Staff ID", Get("toOfficerId"));
                    Add("เหตุผล", Get("reason", "Reason"));
                    break;

                case "AddNote":
                    Add("ประเภทบันทึก", NoteTypeLabel(Get("noteType")));
                    break;

                case "AddInvestigationLog":
                    var conf = Get("isConfidential");
                    Add("ประเภท", conf is "True" or "true" ? "บันทึกลับ" : "บันทึกทั่วไป");
                    break;

                case "DecryptReporterInfo":
                    Add("เหตุผล", Get("reason", "Reason"));
                    break;

                case "CreateStaffUser":
                    Add("รหัสพนักงาน", Get("EmployeeCode", "employeeCode"));
                    Add("ชื่อ", Get("FullName", "fullName"));
                    Add("บทบาท", RoleLabel(Get("Role", "role")));
                    break;

                case "EditStaffUser":
                    Add("ชื่อ", Get("FullName", "fullName"));
                    Add("บทบาท", RoleLabel(Get("Role", "role")));
                    Add("สถานะ", ActiveLabel(Get("IsActive", "isActive")));
                    break;

                case "CreateApiKey":
                    Add("ชื่อ", Get("Name", "name"));
                    Add("ประเภท", KeyTypeLabel(Get("KeyType", "keyType")));
                    var scopesJson = Get("Scopes", "scopes");
                    if (!string.IsNullOrEmpty(scopesJson))
                    {
                        try
                        {
                            var list = JsonSerializer.Deserialize<List<string>>(scopesJson);
                            if (list?.Count > 0) Add("Scopes", string.Join(", ", list));
                        }
                        catch { Add("Scopes", scopesJson); }
                    }
                    break;

                case "RevokeApiKey":
                    Add("เหตุผล", Get("Reason", "reason"));
                    break;

                case "CreateCategory":
                case "EditCategory":
                    Add("หมวดหมู่", Get("Name", "name"));
                    Add("สถานะ", ActiveLabel(Get("IsActive", "isActive")));
                    break;

                case "UpdateNotificationTemplate":
                    Add("Template", Get("EventKey", "eventKey"));
                    break;

                default:
                    foreach (var prop in root.EnumerateObject())
                        parts.Add($"{prop.Name}: {prop.Value}");
                    break;
            }

            return string.Join("  ·  ", parts);
        }
        catch
        {
            return detailJson;
        }
    }

    private static string? StatusLabel(string? s) => s switch
    {
        "Pending"     => "รอดำเนินการ",
        "InProgress"  => "กำลังดำเนินการ",
        "WaitingInfo" => "รอข้อมูลเพิ่มเติม",
        "Resolved"    => "แก้ไขแล้ว",
        "Closed"      => "ปิดเรื่องแล้ว",
        "Rejected"    => "ปฏิเสธ",
        null or ""    => null,
        _             => s
    };

    private static string? RoleLabel(string? r) => r switch
    {
        "GeneralOfficer"    => "เจ้าหน้าที่ทั่วไป",
        "CorruptionOfficer" => "เจ้าหน้าที่ทุจริต",
        "SuperAdmin"        => "ผู้ดูแลระบบ",
        null or ""          => null,
        _                   => r
    };

    private static string? ActiveLabel(string? v) => v switch
    {
        "True" or "true"   => "ใช้งานได้",
        "False" or "false" => "ปิดใช้งาน",
        _                  => null
    };

    private static string? NoteTypeLabel(string? t) => t switch
    {
        "Internal" => "บันทึกภายใน",
        "Public"   => "แจ้งผู้ร้องเรียน",
        null or "" => null,
        _          => t
    };

    private static string? KeyTypeLabel(string? t) => t switch
    {
        "External" => "ภายนอก",
        "Internal" => "ภายใน",
        null or "" => null,
        _          => t
    };

    public async Task<IActionResult> OnPostExportAsync()
    {
        var q = db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(ActionFilter))
            q = q.Where(a => a.Action.Contains(ActionFilter.Trim()));
        if (!string.IsNullOrWhiteSpace(ActorCodeFilter))
            q = q.Where(a => a.ActorCode != null && a.ActorCode.Contains(ActorCodeFilter.Trim()));
        if (!string.IsNullOrWhiteSpace(EntityTypeFilter))
            q = q.Where(a => a.EntityType == EntityTypeFilter);
        if (FromDate.HasValue)
            q = q.Where(a => a.CreatedAt >= FromDate.Value.ToUniversalTime());
        if (ToDate.HasValue)
            q = q.Where(a => a.CreatedAt <= ToDate.Value.AddDays(1).ToUniversalTime());

        var logs = await q.OrderByDescending(a => a.CreatedAt).ToListAsync();

        var actorIds = logs.Where(l => l.ActorId.HasValue).Select(l => l.ActorId!.Value).Distinct().ToList();
        var actorNames = actorIds.Any()
            ? await db.StaffUsers.Where(u => actorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName)
            : new Dictionary<int, string>();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Audit Log");

        ws.Cell(1, 1).Value = "เวลา (UTC+7)";
        ws.Cell(1, 2).Value = "Action";
        ws.Cell(1, 3).Value = "ผู้กระทำ";
        ws.Cell(1, 4).Value = "รหัสพนักงาน";
        ws.Cell(1, 5).Value = "Entity";
        ws.Cell(1, 6).Value = "Entity ID";
        ws.Cell(1, 7).Value = "รายละเอียด";
        ws.Cell(1, 8).Value = "IP Address";
        ws.Row(1).Style.Font.Bold = true;
        ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromArgb(0x00, 0x31, 0x66);
        ws.Row(1).Style.Font.FontColor = XLColor.White;

        for (int i = 0; i < logs.Count; i++)
        {
            var log = logs[i];
            var row = i + 2;
            ws.Cell(row, 1).Value = log.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
            ws.Cell(row, 2).Value = FormatAction(log.Action);
            ws.Cell(row, 3).Value = log.ActorId.HasValue ? actorNames.GetValueOrDefault(log.ActorId.Value, "") : "ระบบ";
            ws.Cell(row, 4).Value = log.ActorCode ?? "";
            ws.Cell(row, 5).Value = EntityTypeLabel(log.EntityType);
            ws.Cell(row, 6).Value = log.EntityId ?? "";
            ws.Cell(row, 7).Value = FormatDetail(log.Action, log.Detail);
            ws.Cell(row, 8).Value = log.IpAddress ?? "";
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var filename = $"audit-log-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            filename);
    }
}
