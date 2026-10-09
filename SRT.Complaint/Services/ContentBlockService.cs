#nullable enable
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public class ContentBlockService(AppDbContext db) : IContentBlockService
{
    // HtmlSanitizer strips the `class` attribute entirely by default. The HTML this
    // service saves is always our own template output (BuildStepsHtml/BuildContactHtml/
    // BuildTrustHtml) with all free text already HTML-encoded via RenderInline — the
    // class names below (step-list, done, contact-dl, trust-ul, sla-note, pdpa-note,
    // content-image) are hardcoded by us, never admin input, so allowing `class` here
    // doesn't reopen an XSS hole. Without it, the home page's layout CSS silently
    // never matches anything and every popup renders as unstyled, cramped text.
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedAttributes.Add("class");
        return sanitizer;
    }

    private static readonly Regex BoldMarker = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

    // camelCase so BodyJson matches the plain-JS object keys used by the
    // admin repeater UI (StepItemData.Title <-> JS item.title, etc.)
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<Dictionary<string, ContentBlock>> GetHomeBlocksAsync()
    {
        var blocks = await db.ContentBlocks
            .Where(b => b.Key.StartsWith("home_"))
            .ToDictionaryAsync(b => b.Key);

        if (!blocks.ContainsKey("home_steps"))
            blocks["home_steps"] = Defaults.Steps();
        if (!blocks.ContainsKey("home_contact"))
            blocks["home_contact"] = Defaults.Contact();
        if (!blocks.ContainsKey("home_trust"))
            blocks["home_trust"] = Defaults.Trust();

        return blocks;
    }

    public Task SaveStepsAsync(string title, StepsBlockData data, int staffId) =>
        UpsertAsync("home_steps", title, BuildStepsHtml(data), JsonSerializer.Serialize(data, JsonOpts), staffId);

    public Task SaveContactAsync(string title, ContactBlockData data, int staffId) =>
        UpsertAsync("home_contact", title, BuildContactHtml(data), JsonSerializer.Serialize(data, JsonOpts), staffId);

    public Task SaveTrustAsync(string title, TrustBlockData data, int staffId) =>
        UpsertAsync("home_trust", title, BuildTrustHtml(data), JsonSerializer.Serialize(data, JsonOpts), staffId);

    private async Task UpsertAsync(string key, string title, string bodyHtml, string bodyJson, int staffId)
    {
        var block = await db.ContentBlocks.FirstOrDefaultAsync(b => b.Key == key);
        if (block is null)
        {
            block = new ContentBlock { Key = key };
            db.ContentBlocks.Add(block);
        }
        block.Title = title;
        block.BodyHtml = Sanitizer.Sanitize(bodyHtml);
        block.BodyJson = bodyJson;
        block.UpdatedById = staffId > 0 ? staffId : null;
        block.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // ── Parse: BodyJson (fast path) → AngleSharp parse of legacy BodyHtml → hardcoded defaults ──

    public StepsBlockData ParseSteps(ContentBlock? block)
    {
        if (block is not null && TryDeserialize<StepsBlockData>(block.BodyJson, out var data) && data.Items.Count > 0)
            return data;
        if (block is not null && ParseStepsFromHtml(block.BodyHtml) is { } fromHtml)
            return fromHtml;
        return ParseStepsFromHtml(Defaults.Steps().BodyHtml) ?? new StepsBlockData([], "", null);
    }

    public ContactBlockData ParseContact(ContentBlock? block)
    {
        if (block is not null && TryDeserialize<ContactBlockData>(block.BodyJson, out var data) && data.Items.Count > 0)
            return data;
        if (block is not null && ParseContactFromHtml(block.BodyHtml) is { } fromHtml)
            return fromHtml;
        return ParseContactFromHtml(Defaults.Contact().BodyHtml) ?? new ContactBlockData([], null);
    }

    public TrustBlockData ParseTrust(ContentBlock? block)
    {
        if (block is not null && TryDeserialize<TrustBlockData>(block.BodyJson, out var data) && data.Items.Count > 0)
            return data;
        if (block is not null && ParseTrustFromHtml(block.BodyHtml) is { } fromHtml)
            return fromHtml;
        return ParseTrustFromHtml(Defaults.Trust().BodyHtml) ?? new TrustBlockData([], "", null);
    }

    private static bool TryDeserialize<T>(string? json, out T value)
    {
        value = default!;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var deserialized = JsonSerializer.Deserialize<T>(json, JsonOpts);
            if (deserialized is null) return false;
            value = deserialized;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // ── HTML <-> structured data for each block shape ──

    private static StepsBlockData? ParseStepsFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var doc = new HtmlParser().ParseDocument(html);
        var listItems = doc.QuerySelectorAll(".step-list > li");
        if (listItems.Length == 0) return null;

        var items = new List<StepItemData>();
        foreach (var li in listItems)
        {
            var titleEl = li.QuerySelector("b");
            if (titleEl is null) continue;
            var descEl = li.QuerySelector("p");
            items.Add(new StepItemData(
                ExtractInline(titleEl),
                descEl is not null ? ExtractInline(descEl) : "",
                li.ClassList.Contains("done")));
        }
        if (items.Count == 0) return null;

        var slaNoteEl = doc.QuerySelector(".sla-note");
        var imgEl = doc.QuerySelector(".content-image");
        return new StepsBlockData(items, slaNoteEl is not null ? ExtractInline(slaNoteEl) : "", imgEl?.GetAttribute("src"));
    }

    private static string BuildStepsHtml(StepsBlockData data)
    {
        var sb = new StringBuilder();
        AppendImageIfAny(sb, data.ImageUrl);

        sb.Append("<ol class=\"step-list\">");
        foreach (var item in data.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Title)) continue;
            var cls = item.Done ? " class=\"done\"" : "";
            sb.Append($"<li{cls}><b>{RenderInline(item.Title)}</b><p>{RenderInline(item.Description)}</p></li>");
        }
        sb.Append("</ol>");

        if (!string.IsNullOrWhiteSpace(data.SlaNote))
            sb.Append($"<p class=\"sla-note\">{RenderInline(data.SlaNote)}</p>");

        return sb.ToString();
    }

    private static ContactBlockData? ParseContactFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var doc = new HtmlParser().ParseDocument(html);
        var rows = doc.QuerySelectorAll(".contact-dl > div");
        if (rows.Length == 0) return null;

        var items = new List<ContactItemData>();
        foreach (var row in rows)
        {
            var dt = row.QuerySelector("dt");
            var dd = row.QuerySelector("dd");
            if (dt is null || dd is null) continue;
            items.Add(new ContactItemData(ExtractInline(dt), ExtractInline(dd)));
        }
        if (items.Count == 0) return null;

        var imgEl = doc.QuerySelector(".content-image");
        return new ContactBlockData(items, imgEl?.GetAttribute("src"));
    }

    private static string BuildContactHtml(ContactBlockData data)
    {
        var sb = new StringBuilder();
        AppendImageIfAny(sb, data.ImageUrl);

        sb.Append("<dl class=\"contact-dl\">");
        foreach (var item in data.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Label)) continue;
            sb.Append($"<div><dt>{RenderInline(item.Label)}</dt><dd>{RenderInline(item.Value)}</dd></div>");
        }
        sb.Append("</dl>");
        return sb.ToString();
    }

    private static TrustBlockData? ParseTrustFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var doc = new HtmlParser().ParseDocument(html);
        var lis = doc.QuerySelectorAll(".trust-ul > li");
        if (lis.Length == 0) return null;

        var items = lis.Select(li => new TrustItemData(ExtractInline(li))).ToList();
        var noteEl = doc.QuerySelector(".pdpa-note");
        var imgEl = doc.QuerySelector(".content-image");
        return new TrustBlockData(items, noteEl is not null ? ExtractInline(noteEl) : "", imgEl?.GetAttribute("src"));
    }

    private static string BuildTrustHtml(TrustBlockData data)
    {
        var sb = new StringBuilder();
        AppendImageIfAny(sb, data.ImageUrl);

        sb.Append("<ul class=\"trust-ul\">");
        foreach (var item in data.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Text)) continue;
            sb.Append($"<li>{RenderInline(item.Text)}</li>");
        }
        sb.Append("</ul>");

        if (!string.IsNullOrWhiteSpace(data.Note))
            sb.Append($"<p class=\"pdpa-note\">{RenderInline(data.Note)}</p>");

        return sb.ToString();
    }

    private static void AppendImageIfAny(StringBuilder sb, string? imageUrl)
    {
        if (!string.IsNullOrWhiteSpace(imageUrl))
            sb.Append($"<img class=\"content-image\" src=\"{WebUtility.HtmlEncode(imageUrl)}\">");
    }

    // Lightweight "**bold**" convention so admins get emphasis without typing HTML tags.
    private static string RenderInline(string? raw)
    {
        var encoded = WebUtility.HtmlEncode(raw ?? "");
        return BoldMarker.Replace(encoded, "<strong>$1</strong>");
    }

    private static string ExtractInline(IElement el)
    {
        var sb = new StringBuilder();
        foreach (var node in el.ChildNodes)
        {
            if (node is IElement e && (e.TagName == "STRONG" || e.TagName == "B"))
                sb.Append("**").Append(e.TextContent).Append("**");
            else
                sb.Append(node.TextContent);
        }
        return sb.ToString().Trim();
    }

    // Default content shown before admin saves anything
    public static class Defaults
    {
        public static ContentBlock Steps() => new()
        {
            Key = "home_steps",
            Title = "ขั้นตอนการร้องเรียน",
            IsActive = true,
            BodyHtml =
                """
                <ol class="step-list">
                  <li><b>ยื่นเรื่องออนไลน์</b><p>กรอกแบบฟอร์มและแนบเอกสารที่เกี่ยวข้อง ระบบออกเลขที่อ้างอิงให้ทันที</p></li>
                  <li><b>รับเรื่องและแจ้งยืนยัน</b><p>เจ้าหน้าที่รับเรื่องและแจ้งกลับผ่าน SMS หรืออีเมลที่ท่านระบุไว้</p></li>
                  <li><b>ตรวจสอบข้อเท็จจริง</b><p>สืบสวนและประสานงานกับหน่วยงานที่เกี่ยวข้อง ท่านสามารถติดตามความคืบหน้าได้ตลอดเวลา</p></li>
                  <li class="done"><b>แจ้งผลการพิจารณา</b><p>แจ้งผลให้ท่านทราบพร้อมมาตรการที่ดำเนินการ</p></li>
                </ol>
                <p class="sla-note">ระยะเวลาดำเนินการไม่เกิน 15 วันทำการ</p>
                """
        };

        public static ContentBlock Contact() => new()
        {
            Key = "home_contact",
            Title = "ช่องทางติดต่อ",
            IsActive = true,
            BodyHtml =
                """
                <dl class="contact-dl">
                  <div><dt>สายด่วน</dt><dd>1690</dd></div>
                  <div><dt>อีเมล</dt><dd>complaint@railway.co.th</dd></div>
                  <div><dt>เวลาทำการ</dt><dd>จันทร์ – ศุกร์ 08:30 – 16:30 น.</dd></div>
                </dl>
                """
        };

        public static ContentBlock Trust() => new()
        {
            Key = "home_trust",
            Title = "ความปลอดภัย & ความเป็นส่วนตัว",
            IsActive = true,
            BodyHtml =
                """
                <ul class="trust-ul">
                  <li>ข้อมูลของท่านได้รับการปกป้องตาม <strong>พ.ร.บ. คุ้มครองข้อมูลส่วนบุคคล พ.ศ. 2562</strong> (PDPA)</li>
                  <li>ข้อมูลผู้แจ้งเบาะแสทุจริตเข้ารหัส <strong>AES-256</strong> เปิดเผยได้เฉพาะเจ้าหน้าที่ผู้มีสิทธิ์เท่านั้น</li>
                  <li>ทุกเรื่องได้รับ <strong>เลขที่อ้างอิง</strong> สำหรับติดตามสถานะตลอดกระบวนการ</li>
                  <li>การเชื่อมต่อผ่าน <strong>HTTPS</strong> เข้ารหัสทุกการรับส่งข้อมูล</li>
                </ul>
                <p class="pdpa-note">หากมีข้อสงสัยเรื่องการคุ้มครองข้อมูลส่วนบุคคล กรุณาติดต่อเจ้าหน้าที่คุ้มครองข้อมูล (DPO) ที่ dpo@railway.co.th</p>
                """
        };
    }
}
