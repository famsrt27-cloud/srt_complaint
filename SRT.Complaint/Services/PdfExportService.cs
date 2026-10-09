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
    private static bool _fontsRegistered;
    private static readonly Lock _fontLock = new();

    static PdfExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PdfExportService(IWebHostEnvironment env)
    {
        _env = env;
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

    private const string PdpaNotice =
        "ศูนย์ประชาสัมพันธ์ขอปกปิดชื่อผู้ร้องเรียน และหมายเลขโทรศัพท์ " +
        "ตาม พรบ.คุ้มครองข้อมูลส่วนบุคคล พ.ศ. 2562";

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

    // ──────────────────────────────────────────────────────────────────
    //  เอกสารเรื่องร้องเรียนทั่วไป
    // ──────────────────────────────────────────────────────────────────
    public byte[] GenerateComplaintPdf(Models.Complaint complaint, string? officerName = null, bool maskReporter = false)
    {
        var logo = GetLogo();
        var subject = complaint.SubCategory != null
            ? $"{complaint.Category?.Name} - {complaint.SubCategory.Name}"
            : complaint.Category?.Name ?? "-";

        return Document.Create(c => c.Page(page =>
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
                        .Text("โทรศัพท์ / โทรสาร : 0 2220 4297");
                    row.RelativeItem().AlignRight().DefaultTextStyle(Small)
                        .Text("E-Mail : complaint@railway.co.th");
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
                    row.RelativeItem().Row(r =>
                    {
                        r.AutoItem().PaddingRight(6).DefaultTextStyle(H1).Text("เรื่อง :");
                        r.RelativeItem().DefaultTextStyle(Body).Text(subject);
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

                // รายละเอียดคำร้อง
                col.Item().DefaultTextStyle(H1).Text("รายละเอียดคำร้อง");
                col.Item().PaddingTop(4);
                col.Item().PaddingLeft(8).DefaultTextStyle(Body).Text(complaint.Description);

            });

            // ══ FOOTER ════════════════════════════════════════════════
            page.Footer().Column(col =>
            {
                col.Item().LineHorizontal(0.5f);
                col.Item().PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().DefaultTextStyle(Small).Text(t =>
                    {
                        t.Span("ออกโดยระบบรับเรื่องร้องเรียนออนไลน์ การรถไฟแห่งประเทศไทย");
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

    // ──────────────────────────────────────────────────────────────────
    //  เอกสารแจ้งเบาะแสทุจริต (ลับ)
    // ──────────────────────────────────────────────────────────────────
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
