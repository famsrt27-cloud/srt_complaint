using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SRT.Complaint.Models;
using SRT.Complaint.Models.Corruption;
using Microsoft.AspNetCore.Hosting;

namespace SRT.Complaint.Services;

public class PdfExportService : IPdfExportService
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private static bool _fontsRegistered;
    private static readonly Lock _fontLock = new();

    static PdfExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PdfExportService(IWebHostEnvironment env, IConfiguration config)
    {
        _env = env;
        _config = config;
        EnsureFontsRegistered();
    }

    private void EnsureFontsRegistered()
    {
        if (_fontsRegistered) return;
        lock (_fontLock)
        {
            if (_fontsRegistered) return;
            var dir = Path.Combine(_env.ContentRootPath, "Fonts");
            TryRegister(Path.Combine(dir, "Sarabun-Regular.ttf"));
            TryRegister(Path.Combine(dir, "Sarabun-Bold.ttf"));
            _fontsRegistered = true;
        }
    }

    private static void TryRegister(string path)
    {
        if (!File.Exists(path)) return;
        using var stream = File.OpenRead(path);
        FontManager.RegisterFont(stream);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Text styles  (ลดจาก 16/18pt เหลือ 13/15pt ตามสารบรรณไทย)
    // ──────────────────────────────────────────────────────────────────
    private static readonly System.Globalization.CultureInfo ThaiCulture = new("th-TH");

    private static TextStyle DocTitle  => TextStyle.Default.FontFamily("Sarabun").FontSize(14).Bold();
    private static TextStyle OrgName   => TextStyle.Default.FontFamily("Sarabun").FontSize(11);
    private static TextStyle H1        => TextStyle.Default.FontFamily("Sarabun").FontSize(11).Bold();
    private static TextStyle Body      => TextStyle.Default.FontFamily("Sarabun").FontSize(11);
    private static TextStyle Small     => TextStyle.Default.FontFamily("Sarabun").FontSize(8);
    private static TextStyle Tiny      => TextStyle.Default.FontFamily("Sarabun").FontSize(6.5f);

    private const string PdpaNotice =
        "ผู้ร้องประสงค์ให้ปกปิดข้อมูลส่วนบุคคลทั้งหมด " +
        "ตาม พ.ร.บ.คุ้มครองข้อมูลส่วนบุคคล พ.ศ. 2562";

    private const string DisclosureStatement =
        "ผู้ร้องยินดีเปิดเผยชื่อ-นามสกุล หมายเลขโทรศัพท์ อีเมล และข้อมูลติดต่อ " +
        "เพื่อให้เจ้าหน้าที่ติดต่อกลับ ทั้งนี้ โปรดใช้ข้อมูลส่วนบุคคลของผู้ร้องเรียน" +
        "เพื่อวัตถุประสงค์ในการตรวจสอบและแก้ไขข้อร้องเรียนเท่านั้น " +
        "ห้ามนำไปใช้หรือเปิดเผยข้อมูลผิดวัตถุประสงค์โดยเด็ดขาด";

    private const string ConcealmentStatement =
        "ผู้ร้องประสงค์ให้ปกปิดชื่อ-นามสกุล หมายเลขโทรศัพท์ อีเมล " +
        "และข้อมูลส่วนบุคคลทั้งหมดในเอกสารฉบับนี้";

    private const string ActionRequestStatement =
        "โปรดพิจารณาดำเนินการในส่วนที่เกี่ยวข้อง และขอความกรุณาแจ้งตอบกลับศูนย์ประชาสัมพันธ์ " +
        "เพื่อตอบชี้แจงผู้ร้องทราบต่อไปด้วย จักเป็นพระคุณยิ่ง";

    private const string InformationStatement =
        "จึงเรียนมาเพื่อโปรดรับทราบเป็นข้อมูลประกอบการพิจารณา";

    private const string CheckedBoxSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
          <rect x="1.5" y="1.5" width="21" height="21" rx="1" fill="none" stroke="#111827" stroke-width="2"/>
          <path d="M6 12.5l4 4L18.5 8" fill="none" stroke="#111827" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
        """;

    // ──────────────────────────────────────────────────────────────────
    //  Helper — แถว label : value
    // ──────────────────────────────────────────────────────────────────
    private static void Field(ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(r =>
        {
            r.AutoItem().PaddingRight(6).DefaultTextStyle(H1).Text(label);
            r.RelativeItem().DefaultTextStyle(Body).Text(value);
        });
    }

    private static void FieldPair(ColumnDescriptor col,
        string label1, string value1,
        string? label2 = null, string? value2 = null)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().Row(r =>
            {
                r.AutoItem().PaddingRight(6).DefaultTextStyle(H1).Text(label1);
                r.RelativeItem().DefaultTextStyle(Body).Text(value1);
            });
            if (label2 != null && value2 != null)
            {
                row.ConstantItem(24);
                row.RelativeItem().Row(r =>
                {
                    r.AutoItem().PaddingRight(6).DefaultTextStyle(H1).Text(label2);
                    r.RelativeItem().DefaultTextStyle(Body).Text(value2);
                });
            }
        });
    }

    private static void CheckedStatement(ColumnDescriptor col, string text)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(15).Height(15).Svg(CheckedBoxSvg);
            row.RelativeItem().PaddingLeft(8).DefaultTextStyle(Small).Text(text);
        });
    }

    private static void ComposeSignature(ColumnDescriptor col, PdfSignerProfile? signer)
    {
        signer ??= new PdfSignerProfile(
            "นางสาวชัชชนก แสงมุข",
            "หัวหน้ากองประชาสัมพันธ์",
            "ปฏิบัติการแทน ผู้อำนวยการศูนย์ประชาสัมพันธ์",
            null,
            null);

        col.Item().PaddingTop(10).AlignRight().Width(270).Column(signature =>
        {
            if (signer.SignatureImage is { Length: > 0 })
            {
                signature.Item().Height(54).AlignCenter().Image(signer.SignatureImage).FitArea();
            }

            signature.Item().AlignCenter().DefaultTextStyle(Small).Text($"({signer.FullName})");
            signature.Item().AlignCenter().DefaultTextStyle(Small).Text(signer.PositionTitle);
            signature.Item().AlignCenter().DefaultTextStyle(Small).Text(signer.ActingTitle);
        });
    }

    private static void ComposeMainFooter(ColumnDescriptor col, string? officerName, DateTime printedAt)
    {
        col.Item().DefaultTextStyle(Tiny).Text(text =>
        {
            text.Span("หมายเหตุ").Bold();
        });
        col.Item().DefaultTextStyle(Tiny)
            .Text("- หากได้รับเอกสารแล้ว ขอให้แจ้งตอบรับทางระบบสารบัญอิเล็กทรอนิกส์ของการรถไฟแห่งประเทศไทยในโอกาสแรกด้วย");
        col.Item().DefaultTextStyle(Tiny)
            .Text("- โปรดปฏิบัติตามข้อกำหนดและหลักเกณฑ์การคุ้มครองข้อมูลส่วนบุคคล ตามพระราชบัญญัติคุ้มครองข้อมูลส่วนบุคคล พ.ศ. 2562");
        col.Item().DefaultTextStyle(Tiny)
            .Text("- โปรดระบุเลขรหัสเรื่องทุกครั้งเมื่อมีการประสานงาน");
        col.Item().PaddingTop(3).LineHorizontal(0.5f);
        col.Item().PaddingTop(3).DefaultTextStyle(Tiny)
            .Text("ผู้ประสานงาน : System  งานประชาสัมพันธ์และรับเรื่องร้องเรียน  โทรศัพท์ 0 2220 4261, 4268, 4297");
        col.Item().PaddingTop(2).Row(row =>
        {
            row.RelativeItem().DefaultTextStyle(Tiny).Text(text =>
            {
                if (!string.IsNullOrWhiteSpace(officerName))
                    text.Span($"พิมพ์เอกสารโดย : {officerName}  ");
                text.Span("หน้า ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
            row.AutoItem().AlignRight().DefaultTextStyle(Tiny)
                .Text($"วันที่พิมพ์ : {printedAt.ToString("dd/MM/yyyy HH:mm:ss", ThaiCulture)}");
        });
    }

    private static void ComposeAttachmentFooter(ColumnDescriptor col, DateTime printedAt)
    {
        col.Item().LineHorizontal(0.5f);
        col.Item().PaddingTop(3).Row(row =>
        {
            row.RelativeItem().DefaultTextStyle(Tiny).Text(text =>
            {
                text.Span("ไฟล์แนบ  |  หน้า ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
            row.AutoItem().DefaultTextStyle(Tiny)
                .Text($"วันที่พิมพ์ : {printedAt.ToString("dd/MM/yyyy HH:mm:ss", ThaiCulture)}");
        });
    }

    private sealed record PdfAttachment(string FileName, byte[]? ImageData);

    private List<PdfAttachment> LoadAttachments(Models.Complaint complaint)
    {
        var result = new List<PdfAttachment>();
        foreach (var attachment in complaint.Attachments
                     .Where(x => x.ComplaintNoteId is null)
                     .OrderBy(x => x.UploadedAt)
                     .ThenBy(x => x.Id))
        {
            var isImage = attachment.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true
                || Path.GetExtension(attachment.FileName).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png";
            if (!isImage) continue;

            byte[]? imageData = null;
            try
            {
                var path = ResolveAttachmentPath(attachment.StoredPath);
                if (File.Exists(path)) imageData = File.ReadAllBytes(path);
            }
            catch (IOException)
            {
                imageData = null;
            }
            catch (UnauthorizedAccessException)
            {
                imageData = null;
            }

            result.Add(new PdfAttachment(attachment.FileName, imageData));
        }

        return result;
    }

    private string ResolveAttachmentPath(string storedPath)
    {
        var root = Path.GetFullPath(_config["FileUpload:StoragePath"] ?? Path.GetTempPath());
        var candidate = Path.GetFullPath(Path.Combine(root, storedPath));
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Invalid attachment storage path");
        return candidate;
    }

    private static void ComposeAttachmentPages(
        IDocumentContainer document,
        string referenceNumber,
        IReadOnlyList<PdfAttachment> attachments,
        DateTime printedAt)
    {
        var images = attachments.Where(x => x.ImageData is { Length: > 0 }).ToList();
        foreach (var group in images.Chunk(2))
        {
            document.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(Body);
                page.Header().Column(header =>
                {
                    header.Item().DefaultTextStyle(DocTitle).Text("ไฟล์แนบ - รูปภาพ");
                    header.Item().PaddingTop(2).DefaultTextStyle(Small).Text($"รหัสเรื่อง : {referenceNumber}");
                    header.Item().PaddingTop(6).LineHorizontal(0.5f);
                });
                page.Content().PaddingTop(10).Column(col =>
                {
                    foreach (var image in group)
                    {
                        col.Item().PaddingBottom(4).DefaultTextStyle(Small).Text(image.FileName);
                        col.Item().Height(300).Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(6).AlignCenter().AlignMiddle().Image(image.ImageData!).FitArea();
                        col.Item().PaddingBottom(10);
                    }
                });
                page.Footer().Column(col => ComposeAttachmentFooter(col, printedAt));
            });
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  เอกสารเรื่องร้องเรียนทั่วไป
    // ──────────────────────────────────────────────────────────────────
    public byte[] GenerateComplaintPdf(Models.Complaint complaint, string? officerName = null,
        bool maskReporter = false, PdfSignerProfile? signer = null,
        string? recipientDepartment = null)
    {
        var logo = GetLogo();
        var printedAt = DateTime.Now;
        var attachments = LoadAttachments(complaint);
        var subject = complaint.SubCategory != null
            ? $"{complaint.Category?.Name} - {complaint.SubCategory.Name}"
            : complaint.Category?.Name ?? "-";

        return Document.Create(document =>
        {
            document.Page(page =>
            {
            page.Margin(40);
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(Body);

            // ══ HEADER ════════════════════════════════════════════════
            page.Header().Column(col =>
            {
                // แถว 1: โลโก้ (ซ้าย) + ชื่อเอกสาร + ชื่อหน่วยงาน (กลาง)
                col.Item().Row(row =>
                {
                    if (logo != null)
                        row.ConstantItem(68).AlignMiddle().Image(logo).FitWidth();

                    row.RelativeItem().AlignMiddle().Column(c =>
                    {
                        c.Item().AlignCenter().DefaultTextStyle(DocTitle)
                            .Text("ใบบันทึกเรื่องร้องเรียน");
                        c.Item().PaddingTop(2).AlignCenter().DefaultTextStyle(OrgName)
                            .Text("การรถไฟแห่งประเทศไทย");
                    });
                });

                col.Item().PaddingTop(5);

                // แถว 2: ศูนย์ประชาสัมพันธ์
                col.Item().DefaultTextStyle(H1).Text("ศูนย์ประชาสัมพันธ์");

                // แถว 3: เบอร์โทร (ชิดซ้าย) | อีเมล (ชิดขวา)
                col.Item().PaddingTop(1).Row(row =>
                {
                    row.RelativeItem().DefaultTextStyle(Small)
                        .Text("โทรศัพท์ / โทรสาร : 0 2220 4261, 4268, 4297");
                    row.RelativeItem().AlignRight().DefaultTextStyle(Small)
                        .Text("E-Mail : pr.railway.srt@gmail.com");
                });

                // เส้นคู่
                col.Item().PaddingTop(5).LineHorizontal(2f);
                col.Item().PaddingTop(2).LineHorizontal(0.5f);
                col.Item().PaddingTop(6);
            });

            // ══ CONTENT ═══════════════════════════════════════════════
            page.Content().Column(col =>
            {
                // แถวอ้างอิง 2 คอลัมน์
                col.Item().Row(row =>
                {
                    row.RelativeItem().Row(r =>
                    {
                        r.AutoItem().PaddingRight(6).DefaultTextStyle(H1).Text("รหัสเรื่อง :");
                        r.RelativeItem().DefaultTextStyle(Body).Text(complaint.ReferenceNumber);
                    });
                    row.RelativeItem().Row(r =>
                    {
                        r.AutoItem().PaddingRight(6).DefaultTextStyle(H1).Text("วันที่รับเรื่อง :");
                        r.RelativeItem().DefaultTextStyle(Body)
                            .Text(complaint.CreatedAt.ToString("d MMMM yyyy", ThaiCulture));
                    });
                });

                col.Item().PaddingTop(3).Row(row =>
                {
                    row.AutoItem().PaddingRight(6).DefaultTextStyle(H1).Text("เรื่อง :");
                    row.RelativeItem().DefaultTextStyle(Body).Text(subject);
                });

                col.Item().PaddingTop(8).LineHorizontal(0.5f);
                col.Item().PaddingTop(6);

                // ข้อมูลผู้ร้องเรียน
                if (maskReporter)
                {
                    Field(col, "ชื่อผู้ร้องเรียน :", PdpaNotice);
                }
                else
                {
                    Field(col, "ชื่อผู้ร้องเรียน :", complaint.ReporterName);
                    col.Item().PaddingTop(3);
                    FieldPair(col,
                        "เบอร์โทรศัพท์ :", complaint.ReporterPhone,
                        !string.IsNullOrEmpty(complaint.ReporterEmail) ? "อีเมล :" : null,
                        !string.IsNullOrEmpty(complaint.ReporterEmail) ? complaint.ReporterEmail : null);
                }

                var hasStation = !string.IsNullOrEmpty(complaint.SubjectStation);
                var hasDate    = complaint.IncidentDate.HasValue;
                if (hasStation || hasDate)
                {
                    col.Item().PaddingTop(3);
                    FieldPair(col,
                        hasStation ? "สถานที่ / สถานี :" : "วัน / เวลาเกิดเหตุ :",
                        hasStation ? complaint.SubjectStation! : complaint.IncidentDate!.Value.ToString("d MMMM yyyy", ThaiCulture),
                        hasStation && hasDate ? "วัน / เวลาเกิดเหตุ :" : null,
                        hasStation && hasDate ? complaint.IncidentDate!.Value.ToString("d MMMM yyyy", ThaiCulture) : null);
                }

                col.Item().PaddingTop(8).LineHorizontal(0.5f);
                col.Item().PaddingTop(6);

                var department = string.IsNullOrWhiteSpace(recipientDepartment)
                    ? complaint.Category?.DepartmentName
                    : recipientDepartment.Trim();
                if (!string.IsNullOrWhiteSpace(department))
                {
                    col.Item().DefaultTextStyle(H1)
                        .Text($"เรียน {department}");
                    col.Item().PaddingTop(6);
                }

                // รายละเอียดคำร้อง
                col.Item().DefaultTextStyle(H1).Text("รายละเอียดคำร้อง");
                col.Item().PaddingTop(4);
                col.Item().PaddingLeft(8).DefaultTextStyle(Body).Text(complaint.Description);

                col.Item().PaddingTop(10).LineHorizontal(0.5f);
                col.Item().PaddingTop(8);
                CheckedStatement(col, maskReporter ? ConcealmentStatement : DisclosureStatement);

                col.Item().PaddingTop(8);
                CheckedStatement(col, ActionRequestStatement);

                col.Item().PaddingTop(8);
                CheckedStatement(col, InformationStatement);

                ComposeSignature(col, signer);

            });

            // ══ FOOTER ════════════════════════════════════════════════
            page.Footer().Column(col => ComposeMainFooter(col, officerName, printedAt));
            });

            ComposeAttachmentPages(document, complaint.ReferenceNumber, attachments, printedAt);
        }).GeneratePdf();
    }

    // ──────────────────────────────────────────────────────────────────
    //  เอกสารแจ้งเบาะแสทุจริต (ลับ)
    // ──────────────────────────────────────────────────────────────────
    public byte[] GenerateComplaintReportPdf(IReadOnlyList<Models.Complaint> complaints, string? exportedBy = null)
    {
        var generatedAt = DateTime.Now;
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(Body);

                page.Header().Column(column =>
                {
                    column.Item().DefaultTextStyle(DocTitle).Text("รายงานรายละเอียดเรื่องร้องเรียน");
                    column.Item().DefaultTextStyle(Small)
                        .Text($"จำนวน {complaints.Count:N0} รายการ · ส่งออกเมื่อ {generatedAt:dd/MM/yyyy HH:mm}");
                    column.Item().PaddingTop(6).LineHorizontal(1);
                });

                page.Content().PaddingVertical(10).Column(column =>
                {
                    column.Spacing(8);
                    foreach (var complaint in complaints)
                    {
                        column.Item().Border(0.7f).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(card =>
                        {
                            card.Spacing(3);
                            card.Item().Row(row =>
                            {
                                row.RelativeItem().DefaultTextStyle(H1).Text(complaint.ReferenceNumber);
                                row.AutoItem().DefaultTextStyle(Small)
                                    .Text(complaint.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                            });
                            card.Item().DefaultTextStyle(Small).Text(
                                $"ประเภท: {complaint.Category?.Name ?? "-"}" +
                                (complaint.SubCategory is null ? string.Empty : $" / {complaint.SubCategory.Name}"));
                            card.Item().DefaultTextStyle(Small).Text(
                                $"ฝ่ายรับผิดชอบ: {complaint.ResponsibleDepartment ?? complaint.Category?.DepartmentName ?? "-"} · สถานะ: {complaint.Status}");
                            card.Item().DefaultTextStyle(Small).Text(
                                $"ผู้ร้อง: {complaint.ReporterName} · โทร: {complaint.ReporterPhone} · อีเมล: {complaint.ReporterEmail ?? "-"}");
                            card.Item().DefaultTextStyle(Small).Text(
                                $"วัตถุประสงค์: {ComplaintSubmissionPurposes.Label(complaint.SubmissionPurpose)}");
                            card.Item().PaddingTop(2).DefaultTextStyle(Small).Text(complaint.Description);
                        });
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().DefaultTextStyle(Tiny)
                        .Text(string.IsNullOrWhiteSpace(exportedBy) ? "ระบบรับเรื่องร้องเรียน รฟท." : $"ส่งออกโดย {exportedBy}");
                    row.AutoItem().DefaultTextStyle(Tiny).Text(text =>
                    {
                        text.Span("หน้า ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    public byte[] GenerateCorruptionReportPdf(CorruptionReport report, string? officerName = null)
    {
        var logo = GetLogo();

        return Document.Create(c => c.Page(page =>
        {
            page.Margin(40);
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(Body);

            // ══ HEADER ════════════════════════════════════════════════
            page.Header().Column(col =>
            {
                col.Item().AlignRight().DefaultTextStyle(H1).Text("ลับ");

                col.Item().PaddingTop(2).Row(row =>
                {
                    if (logo != null)
                        row.ConstantItem(68).AlignMiddle().Image(logo).FitWidth();

                    row.RelativeItem().AlignMiddle().Column(c =>
                    {
                        c.Item().AlignCenter().DefaultTextStyle(DocTitle)
                            .Text("บันทึกรับแจ้งเบาะแสการทุจริต");
                        c.Item().PaddingTop(2).AlignCenter().DefaultTextStyle(OrgName)
                            .Text("การรถไฟแห่งประเทศไทย");
                    });
                });

                col.Item().PaddingTop(5);

                col.Item().Row(row =>
                {
                    row.RelativeItem().DefaultTextStyle(H1).Text("ศูนย์ประชาสัมพันธ์");
                    row.RelativeItem().AlignRight().DefaultTextStyle(H1).Text("กระทรวงคมนาคม");
                });

                col.Item().PaddingTop(1).Row(row =>
                {
                    row.RelativeItem().DefaultTextStyle(Small)
                        .Text("โทรศัพท์ / โทรสาร : 0 2220 4297");
                    row.RelativeItem().AlignRight().DefaultTextStyle(Small)
                        .Text("E-Mail : complaint@railway.co.th");
                });

                col.Item().PaddingTop(5).LineHorizontal(2f);
                col.Item().PaddingTop(2).LineHorizontal(0.5f);
                col.Item().PaddingTop(6);
            });

            // ══ CONTENT ═══════════════════════════════════════════════
            page.Content().Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Row(r =>
                    {
                        r.AutoItem().PaddingRight(6).DefaultTextStyle(H1).Text("เลขที่รายงาน :");
                        r.RelativeItem().DefaultTextStyle(Body).Text(report.ReferenceNumber);
                    });
                    row.RelativeItem().Row(r =>
                    {
                        r.AutoItem().PaddingRight(6).DefaultTextStyle(H1).Text("วันที่รับเรื่อง :");
                        r.RelativeItem().DefaultTextStyle(Body)
                            .Text(report.CreatedAt.ToString("d MMMM yyyy", ThaiCulture));
                    });
                });

                col.Item().PaddingTop(3).Row(row =>
                {
                    row.RelativeItem().Row(r =>
                    {
                        r.AutoItem().PaddingRight(6).DefaultTextStyle(H1).Text("ประเภทการทุจริต :");
                        r.RelativeItem().DefaultTextStyle(Body).Text(report.SubjectType);
                    });
                    row.RelativeItem().Row(r =>
                    {
                        r.AutoItem().PaddingRight(6).DefaultTextStyle(H1).Text("วันที่พิมพ์ :");
                        r.RelativeItem().DefaultTextStyle(Body)
                            .Text(DateTime.Now.ToString("d MMMM yyyy", ThaiCulture));
                    });
                });

                col.Item().PaddingTop(8).LineHorizontal(0.5f);
                col.Item().PaddingTop(6);

                Field(col, "ชื่อผู้แจ้ง :", report.ReporterNameMasked);
                Field(col, "เบอร์โทรศัพท์ :", report.ReporterPhoneMasked);
                if (!string.IsNullOrEmpty(report.SubjectPersonName))
                    Field(col, "ผู้ถูกกล่าวหา :", report.SubjectPersonName);
                if (!string.IsNullOrEmpty(report.SubjectDepartment))
                    Field(col, "หน่วยงาน :", report.SubjectDepartment);
                if (report.IncidentDate.HasValue)
                    Field(col, "วันที่เกิดเหตุ :",
                        report.IncidentDate.Value.ToString("d MMMM yyyy", ThaiCulture));

                col.Item().PaddingTop(8).LineHorizontal(0.5f);
                col.Item().PaddingTop(6);

                col.Item().DefaultTextStyle(H1).Text("รายละเอียดการแจ้งเบาะแส");
                col.Item().PaddingTop(4);
                col.Item().PaddingLeft(8).DefaultTextStyle(Body).Text(report.Description);

                col.Item().PaddingTop(8).LineHorizontal(0.5f);
                col.Item().PaddingTop(6);
                col.Item().DefaultTextStyle(H1)
                    .Text("เอกสารนี้มีความลับ — ห้ามเปิดเผยแก่บุคคลที่ไม่เกี่ยวข้อง");

            });

            // ══ FOOTER ════════════════════════════════════════════════
            page.Footer().Column(col =>
            {
                col.Item().LineHorizontal(0.5f);
                col.Item().PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().DefaultTextStyle(Small).Text(t =>
                    {
                        t.Span("เอกสารลับ — ออกโดยระบบรับเรื่องร้องเรียนออนไลน์ การรถไฟแห่งประเทศไทย");
                        if (!string.IsNullOrEmpty(officerName))
                            t.Span($"  ·  พิมพ์เอกสารโดย : {officerName}");
                    });
                    r.AutoItem().Text(t =>
                    {
                        t.DefaultTextStyle(Small);
                        t.Span("หน้า ");
                        t.CurrentPageNumber();
                        t.Span(" / ");
                        t.TotalPages();
                    });
                });
            });
        })).GeneratePdf();
    }

    private byte[]? GetLogo()
    {
        var path = Path.Combine(_env.WebRootPath, "images", "srt-logo.png");
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}
