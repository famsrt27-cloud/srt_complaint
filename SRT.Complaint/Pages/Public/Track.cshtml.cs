#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using SRT.Complaint.Models;
using SRT.Complaint.Models.Corruption;
using SRT.Complaint.Services;
using SRT.Complaint.Validation;

namespace SRT.Complaint.Pages.Public;

public class TrackModel(
    IComplaintService complaintService,
    ICorruptionService corruptionService,
    IConfiguration config) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Ref { get; set; }

    public Models.Complaint? Complaint { get; private set; }
    public CorruptionReport? CorruptionReport { get; private set; }
    public bool Searched { get; private set; }
    public bool IsCorruption => CorruptionReport != null;
    public bool NeedsVerification { get; private set; }
    public IReadOnlyList<TimelineEvent> Timeline { get; private set; } = [];

    [BindProperty] public string? PhoneLast4 { get; set; }
    [BindProperty] public byte SatisfactionScore { get; set; }
    [BindProperty] public string? SatisfactionNote { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "ติดตามสถานะเรื่องร้องเรียน";
        if (!string.IsNullOrWhiteSpace(Ref))
        {
            Searched = true;
            var refUpper = Ref.Trim().ToUpper();
            if (refUpper.StartsWith("SRT-CORUPT-"))
            {
                CorruptionReport = await corruptionService.GetByReferenceAsync(refUpper);
            }
            else
            {
                var verified = HttpContext.Session.GetString($"track_v_{refUpper}") == "1";
                var complaint = await complaintService.GetByReferenceAsync(refUpper);
                if (complaint != null)
                {
                    if (verified)
                    {
                        Complaint = complaint;
                        Timeline = BuildTimeline(complaint);
                    }
                    else
                    {
                        NeedsVerification = true;
                    }
                }
            }
        }
    }

    [EnableRateLimiting("TrackVerifyPolicy")]
    public async Task<IActionResult> OnPostVerifyAsync()
    {
        var refUpper = (Ref ?? "").Trim().ToUpper();
        var last4 = (PhoneLast4 ?? "").Trim();

        var attemptsKey = $"track_attempts_{refUpper}";
        var attempts = HttpContext.Session.GetInt32(attemptsKey) ?? 0;
        if (attempts >= 5)
        {
            TempData["VerifyError"] = "พยายามตรวจสอบมากเกินไป กรุณารอสักครู่แล้วลองใหม่";
            return RedirectToPage(new { Ref = refUpper });
        }

        if (last4.Length != 4 || !last4.All(char.IsDigit))
        {
            TempData["VerifyError"] = "กรุณากรอกตัวเลข 4 หลัก";
            return RedirectToPage(new { Ref = refUpper });
        }

        var complaint = await complaintService.GetByReferenceAsync(refUpper);
        if (complaint == null)
        {
            TempData["Error"] = "ไม่พบหมายเลขอ้างอิงนี้";
            return RedirectToPage(new { Ref = refUpper });
        }

        var phoneDigits = new string(complaint.ReporterPhone.Where(char.IsDigit).ToArray());
        if (!phoneDigits.EndsWith(last4))
        {
            HttpContext.Session.SetInt32(attemptsKey, attempts + 1);
            TempData["VerifyError"] = "เบอร์โทรไม่ตรงกัน กรุณาตรวจสอบอีกครั้ง";
            return RedirectToPage(new { Ref = refUpper });
        }

        HttpContext.Session.Remove(attemptsKey);
        HttpContext.Session.SetString($"track_v_{refUpper}", "1");
        return RedirectToPage(new { Ref = refUpper });
    }

    /// <summary>Serves a resolution attachment on a "PublicReply" note to the reporter — gated
    /// behind the same phone-verification session used to view the tracking details in the
    /// first place, and scoped so it can only ever return a file from a public-reply note on
    /// this exact reference number (never internal notes, never other complaints).</summary>
    public async Task<IActionResult> OnGetAttachmentAsync(string reference, int attachmentId)
    {
        var refUpper = (reference ?? "").Trim().ToUpper();
        var verified = HttpContext.Session.GetString($"track_v_{refUpper}") == "1";
        if (!verified) return Forbid();

        var complaint = await complaintService.GetByReferenceAsync(refUpper);
        var attachment = complaint?.Notes
            .Where(n => n.NoteType == "PublicReply")
            .SelectMany(n => n.Attachments)
            .FirstOrDefault(a => a.Id == attachmentId);
        if (attachment == null) return NotFound();

        var storagePath = config["FileUpload:StoragePath"] ?? Path.GetTempPath();
        var fullPath = FileStorage.ResolveWithinRoot(storagePath, attachment.StoredPath);
        if (fullPath is null) return NotFound();
        if (!System.IO.File.Exists(fullPath)) return NotFound();

        return PhysicalFile(fullPath, attachment.MimeType ?? "application/octet-stream", attachment.FileName);
    }

    public async Task<IActionResult> OnPostSatisfactionAsync()
    {
        if (string.IsNullOrWhiteSpace(Ref) || SatisfactionScore < 1 || SatisfactionScore > 5)
        {
            TempData["Error"] = "ข้อมูลไม่ถูกต้อง กรุณาเลือกคะแนน 1–5";
            return RedirectToPage(new { Ref });
        }
        try
        {
            await complaintService.SubmitSatisfactionAsync(Ref.Trim().ToUpper(), SatisfactionScore, SatisfactionNote);
            TempData["Success"] = "ขอบคุณสำหรับการประเมินความพึงพอใจ";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { Ref });
    }

    private static IReadOnlyList<TimelineEvent> BuildTimeline(Models.Complaint c)
    {
        var events = new List<TimelineEvent>
        {
            new(c.CreatedAt, "created", "ยื่นเรื่องร้องเรียน", null)
        };

        foreach (var note in c.Notes.OrderBy(n => n.CreatedAt))
        {
            if (note.NoteType == "StatusChange")
            {
                var parts = note.Content.Split('→', 2);
                var toStatus = parts.Length > 1 ? parts[1].Trim() : note.Content.Trim();
                events.Add(new(note.CreatedAt, "status", $"สถานะ: {StatusLabel(toStatus)}", null));
            }
            else if (note.NoteType == "PublicReply")
            {
                events.Add(new(note.CreatedAt, "reply", "ข้อความจากเจ้าหน้าที่", note.Content, note.Attachments.ToList()));
            }
        }

        if (c.ClosedAt.HasValue)
            events.Add(new(c.ClosedAt.Value, "closed",
                c.Status == "Resolved" ? "แก้ไขปัญหาเรียบร้อยแล้ว" : "ปิดเรื่องร้องเรียน", null));
        else if (c.Status == "Rejected")
            events.Add(new(c.UpdatedAt, "rejected", "ไม่ได้รับการพิจารณา", null));

        return events.OrderBy(e => e.At).ToList();
    }

    public record TimelineEvent(DateTime At, string Type, string Label, string? Detail,
        IReadOnlyList<ComplaintAttachment>? Attachments = null);

    public static string StatusLabel(string s) => s switch
    {
        "Pending"      => "รอดำเนินการ",
        "InProgress"   => "กำลังดำเนินการ",
        "WaitingInfo"  => "รอข้อมูลเพิ่มเติม",
        "Forwarded"    => "ส่งต่อแผนก",
        "UnderReview"  => "อยู่ระหว่างพิจารณา",
        "Resolved"     => "แก้ไขแล้ว",
        "Closed"       => "ปิดเรื่อง",
        "Rejected"     => "ปฏิเสธ",
        _              => s
    };

    public static string StatusColor(string s) => s switch
    {
        "Pending"     => "bg-yellow-100 text-yellow-800",
        "InProgress"  => "bg-blue-100 text-blue-800",
        "WaitingInfo" => "bg-orange-100 text-orange-800",
        "Forwarded"   => "bg-purple-100 text-purple-800",
        "UnderReview" => "bg-indigo-100 text-indigo-800",
        "Resolved"    => "bg-green-100 text-green-800",
        "Closed"      => "bg-gray-100 text-gray-700",
        "Rejected"    => "bg-red-100 text-red-800",
        _             => "bg-gray-100 text-gray-700"
    };

    public static string PriorityLabel(string p) => p switch
    {
        "Critical" => "เร่งด่วนมาก",
        "Urgent"   => "เร่งด่วน",
        "High"     => "สำคัญ",
        "Normal"   => "ปกติ",
        "Low"      => "ข้อเสนอแนะ",
        _          => p
    };
}
