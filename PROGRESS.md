# Progress — ระบบรับเรื่องร้องเรียน รฟท.

## ✅ เสร็จแล้ว

### Phase 1: Core Infrastructure
- [x] Models ทั้งหมด (Complaint, StaffUser, Category, SLA, ฯลฯ)
- [x] AppDbContext + CorruptionDbContext + Migrations
- [x] Services Layer (ComplaintService, SlaService, NotificationService, ฯลฯ)
- [x] Program.cs — Auth, DI, Rate Limiting, Middleware
- [x] _Layout.cshtml (Public) + _StaffLayout.cshtml
- [x] SlaBackgroundService — ตรวจ SLA breach ทุก 30 นาที

### Phase 2: Staff Pages
- [x] Pages/Staff/Login.cshtml + .cs — เข้าสู่ระบบ (BCrypt verify)
- [x] Pages/Staff/Logout.cshtml + .cs — ออกจากระบบ
- [x] Pages/Staff/Dashboard.cshtml + .cs — Stat cards, 7-day chart, SLA warnings
- [x] Pages/Staff/Queue.cshtml + .cs — คิวเรื่อง + filter + pagination + claim
- [x] Pages/Staff/CaseDetail.cshtml + .cs — รายละเอียด + claim/note/transfer/close + PDF download
- [x] Pages/Staff/Workload.cshtml + .cs — Workload ทีม + progress bars

### Phase 3: Corruption Track
- [x] Pages/Corruption/Dashboard.cshtml + .cs
- [x] Pages/Corruption/Queue.cshtml + .cs
- [x] Pages/Corruption/CaseDetail.cshtml + .cs — + PDF download + Decrypt reporter

### Phase 4: Admin Pages
- [x] Pages/Admin/Dashboard.cshtml + .cs
- [x] Pages/Admin/Users.cshtml + .cs — staff CRUD
- [x] Pages/Admin/Categories.cshtml + .cs
- [x] Pages/Admin/SlaSettings.cshtml + .cs
- [x] Pages/Admin/Notifications.cshtml + .cs
- [x] Pages/Admin/ApiKeys.cshtml + .cs — create/revoke, raw key shown once + Usage link
- [x] Pages/Admin/AuditLog.cshtml + .cs — paginated + filter + Excel export
- [x] Pages/Admin/Reports.cshtml + .cs — Chart.js charts + ClosedXML Excel export
- [x] Pages/Admin/Webhooks.cshtml + .cs — ดู/ลบ webhook ที่ลงทะเบียนผ่าน API
- [x] Pages/Admin/ApiKeyUsage.cshtml + .cs — ดู request log ของแต่ละ API key

### Phase 5: Public Pages
- [x] Pages/Index.cshtml — หน้าแรก + Quick track form ใน hero section
- [x] Pages/Public/Submit.cshtml + .cs — ยื่นเรื่องร้องเรียนทั่วไป + Rate limiting
- [x] Pages/Public/Track.cshtml + .cs — ติดตามสถานะ (GEN- และ COR-) + satisfaction form
- [x] Pages/Public/SubmitCorruption.cshtml + .cs — แจ้งเบาะแสทุจริต
- [x] Pages/Public/CorruptionSubmitted.cshtml + .cs — หน้ายืนยันหลังส่งเรื่องทุจริต

### Phase 6: API Controllers
- [x] Filters/ApiKeyAuthFilter.cs — X-API-Key auth + IP whitelist + rate limit
- [x] Filters/RequireScopeAttribute.cs — scope enforcement
- [x] Controllers/Api/ComplaintsController.cs — GET/POST/edoc-payload + GET status + PUT status
- [x] Controllers/Api/StatsController.cs — GET /api/stats/summary + /api/stats/detailed
- [x] Controllers/Api/CorruptionStatsController.cs — GET /api/stats/corruption
- [x] Controllers/Api/WebhooksController.cs — GET/POST/DELETE /api/webhooks
- [x] Services/IApiRequestLogService.cs + ApiRequestLogService.cs

### Phase 7: PDF + Email + SMS
- [x] PdfExportService — QuestPDF (GenerateComplaintPdf + GenerateCorruptionReportPdf)
- [x] NotificationService — MailKit + SMS Gateway

### Phase 8: Webhook Infrastructure
- [x] Services/IWebhookService.cs — interface
- [x] Services/WebhookService.cs — HTTP delivery + HMAC-SHA256 signing
- [x] Services/WebhookRetryService.cs — BackgroundService ลอง retry ทุก 5 นาที
- [x] Registered in Program.cs (Scoped + HostedService + HttpClient "Webhook")
- [x] Webhook triggers in ComplaintService (complaint.created, complaint.status_changed, complaint.closed)

### Phase 10: UX Improvements
- [x] AuditLog — แปล Action + Detail เป็นภาษาไทยที่คนทั่วไปอ่านได้ (ทั้งใน table และ Excel export)
- [x] Admin/Users — Modal popup แสดงรหัสผ่านชั่วคราว (ปิดได้เฉพาะกด "รับทราบ") + ปุ่มคัดลอก
- [x] StaffUser.TempPasswordExpiresAt — temp password หมดอายุ 8 ชั่วโมง + migration AddTempPasswordExpiry
- [x] Login — ตรวจสอบ TempPasswordExpiresAt หมดอายุ → แจ้ง error ให้ติดต่อ admin
- [x] ChangePassword — clear TempPasswordExpiresAt เมื่อเปลี่ยนรหัสผ่านสำเร็จ
- [x] Admin/Users — ปุ่ม "ดูรหัสผ่าน" (สีม่วง) สำหรับ user ที่ยังไม่เปลี่ยนรหัสผ่าน (MustChangePassword=true + ยังไม่หมดอายุ) — ใช้ AES decrypt จาก TempPasswordEncrypted field + migration AddTempPasswordEncrypted

### Phase 9: Auth & User Management Fixes
- [x] Bug fix: Admin/Dashboard.cshtml.cs — concurrent EF Core queries บน DbContext เดียว → เปลี่ยนเป็น sequential await
- [x] Feature: MustChangePassword — เพิ่ม field ใน StaffUser + migration AddMustChangePassword
- [x] Feature: Pages/Staff/ChangePassword.cshtml + .cs — หน้าเปลี่ยนรหัสผ่าน (verify current + new + confirm)
- [x] Feature: Login redirect → /Staff/ChangePassword เมื่อ MustChangePassword = true พร้อม claim
- [x] Feature: Middleware ใน Program.cs — บังคับ redirect ทุก route เมื่อ MustChangePassword claim set
- [x] Feature: Admin/Users — auto-generate temp password (8 อักขระ alphanumeric) แทนให้ admin กรอก
- [x] Feature: Admin/Users — Reset Password เป็นปุ่มเดียว auto-generate + set MustChangePassword = true
- [x] Feature: Admin/Users — แสดง TempPassword banner หลัง create/reset

### Phase 13: Reference Number — SQL SEQUENCE + New Format
- [x] Migration AddComplaintSeq: CREATE SEQUENCE dbo.ComplaintSeq (AppDbContext) — applied
- [x] Migration AddCorruptionSeq: CREATE SEQUENCE corruption.CorruptionSeq (CorruptionDbContext) — applied
- [x] ComplaintService: GenerateReferenceNumberAsync → NEXT VALUE FOR dbo.ComplaintSeq → format SRT-COMPL-{year}-{seq:D4}
- [x] CorruptionService: GenerateReferenceNumberAsync → NEXT VALUE FOR corruption.CorruptionSeq → format SRT-CORUPT-{year}-{seq:D4}
- [x] Placeholder อัปเดตทั้ง Index.cshtml และ Track.cshtml
- Note: Sequence ไม่ reset ตามปี — เลขวิ่งต่อเนื่อง (ปลอดภัยกว่า, ไม่มี race condition)
- Note: เรื่องเก่าใน DB ยังเป็น format GEN-/COR- ต้องลบทิ้งหรือปล่อยไว้ (ไม่กระทบระบบ)

### Phase 12: Track Page — Phone Verification + Timeline
- [x] ComplaintNote.AuthorId → nullable (int?) เพื่อรองรับ system-generated notes
- [x] AppDbContext: FK config สำหรับ ComplaintNote.Author → ClientSetNull (NO ACTION)
- [x] Migration: MakeNoteAuthorNullable — applied
- [x] Program.cs: AddSession (30 นาที, HttpOnly, SameSite=Strict) + UseSession
- [x] ComplaintService.UpdateStatusAsync: บันทึก StatusChange note ทุกครั้งที่เปลี่ยนสถานะ
- [x] ComplaintService.ClaimAsync: บันทึก StatusChange note "Pending→InProgress" เมื่อ claim
- [x] Track.cshtml.cs: 2-step phone verification (session-based) + BuildTimeline() + OnPostVerifyAsync
- [x] Track.cshtml: หน้ายืนยันตัวตน (phone 4 ตัวท้าย) + timeline ใหม่ + ข้อความ "ลืม ref ☎ 1690"
- Note: COR- reports ไม่ต้องยืนยัน (ข้อมูลแสดงน้อยอยู่แล้ว + anonymous)
- Note: Timeline format: "OldStatus→NewStatus" เก็บใน ComplaintNote.Content

### Phase 11: Sub-Category (หัวข้อย่อย)
- [x] Models: ComplaintSubCategory (new) + update ComplaintCategory + Complaint
- [x] AppDbContext: DbSet + entity config + FK (SubCategoryId nullable, SetNull on delete)
- [x] Migration: AddSubCategories
- [x] IComplaintService: SubmitComplaintRequest เพิ่ม SubCategoryId?
- [x] ComplaintService: set SubCategoryId บน complaint
- [x] Submit.cshtml.cs: โหลด sub-categories เป็น JSON grouped by categoryId + SubCategoryId ใน ViewModel
- [x] Submit.cshtml: dropdown หัวข้อย่อย show/hide ด้วย JS ตาม category ที่เลือก
- [x] Admin/Categories.cshtml + .cs: แสดง sub-categories ใต้แต่ละ category + CRUD (create/edit/toggle/delete)

### Phase 15: Track Page — Timeline + Satisfaction UX
- [x] Track.cshtml: Status Stepper — 4 ขั้น (รับเรื่อง/ดำเนินการ/พิจารณา/เสร็จสิ้น) พร้อม checkmark/X icon และ progress line
- [x] Track.cshtml: Timeline redesign — icon สี per event type, vertical line, reply content ใน blue box
- [x] Track.cshtml: Satisfaction form fix — เปลี่ยนจาก radio (ไม่ visible) เป็น `<button>` ที่ควบคุมด้วย JS, hidden input รับค่า, client-side validate ก่อน submit, hover preview + score label

### Phase 14: Password Validation
- [x] Validation/PasswordStrengthAttribute.cs — custom DataAnnotation attribute ตรวจสอบ: ห้ามไทย, ≥8 ตัว, ตัวพิมพ์ใหญ่, พิมพ์เล็ก, ตัวเลข, อักขระพิเศษ
- [x] ChangePassword.cshtml.cs — แทน [MinLength(8)] ด้วย [PasswordStrength]
- [x] ChangePassword.cshtml — เพิ่ม real-time checklist แสดงเงื่อนไขรหัสผ่านขณะพิมพ์ (JS oninput)

### Phase 16: Form Validation & Input Masks
- [x] Validation/ThaiIdAttribute.cs — ตรวจ checksum เลขบัตรประชาชน 13 หลัก (Σd[i]×(13-i) mod 11)
- [x] Models/Complaint.cs — เพิ่ม ReporterIdCard? (nullable)
- [x] AppDbContext.cs — HasMaxLength(20) สำหรับ ReporterIdCard
- [x] IComplaintService / ComplaintService — เพิ่ม ReporterIdCard? ใน SubmitComplaintRequest
- [x] Submit.cshtml.cs — property setter strip dashes สำหรับ Phone + เพิ่ม ReporterIdCard? field ใหม่
- [x] SubmitCorruption.cshtml.cs — property setter strip dashes สำหรับ Phone + ID Card, เปลี่ยน [RegularExpression] เป็น [ThaiId]
- [x] Submit.cshtml — เพิ่ม field เลขบัตรประชาชน (optional) + JS applyPhoneMask / applyIdCardMask + strip on submit capture
- [x] SubmitCorruption.cshtml — JS masks เดียวกัน + อัปเดต placeholder ใส่ dash format
- [x] Migration AddReporterIdCard — applied (dbo.Complaints.ReporterIdCard nvarchar(20) NULL)

### Phase 17: PDPA Cookie Banner + Terms Modal
- [x] Models/ComplaintTerms.cs — Id, Title, Content (nvarchar max), IsActive, UpdatedAt, UpdatedById
- [x] Services/ITermsService.cs + TermsService.cs — GetTermsAsync (auto-seed default content), SaveTermsAsync
- [x] Data/AppDbContext.cs — DbSet<ComplaintTerms> + entity config (ClientSetNull FK)
- [x] Migration: AddComplaintTerms — applied
- [x] Pages/Shared/_TermsModal.cshtml — modal overlay, checkbox, ยอมรับ/ยกเลิก, JS: onTermsAccepted()
- [x] Pages/Shared/_CookieBanner.cshtml — PDPA essential cookies notice, localStorage srt_pdpa_consent
- [x] Pages/Admin/Terms.cshtml + .cs — edit Title/Content (HTML), IsActive toggle, preview tab, demo preview
- [x] Pages/Public/Submit.cshtml + .cs — inject ITermsService, Terms property, modal + form-content-wrapper hidden
- [x] Pages/Public/SubmitCorruption.cshtml + .cs — เดียวกัน (OnGet→OnGetAsync)
- [x] Pages/Shared/_Layout.cshtml — <partial name="_CookieBanner" /> ก่อน </body>
- [x] Pages/Shared/_StaffLayout.cshtml — เพิ่ม "หลักเกณฑ์ร้องเรียน" ใน Admin sidebar
- [x] Program.cs — AddScoped<ITermsService, TermsService>

### Phase 18: External Sync — Traffy Fondue Integration
- [x] Bug: Track.cshtml — RZ1010 `@{}` inside `@if{}` Razor syntax error → ลบ wrapper `@{}` ออก ตัวแปร C# อยู่ตรง ๆ ใน `@if`
- [x] Bug: ThaiIdAttribute — ลบ mod-11 checksum (บัตรเก่าบางใบผ่านไม่ได้) เหลือแค่ตรวจ 13 หลัก
- [x] Bug: jQuery Validate phone/ID card error on blur — เพิ่ม `normalizer` strip non-digits ก่อน validate (Submit.cshtml + SubmitCorruption.cshtml)
- [x] Bug: Track page corruption — prefix check ผิด (`COR-` → `SRT-CORUPT-`) แก้ใน Track.cshtml.cs
- [x] Bug: Corruption CaseDetail status loop — `UnderReview` ไม่มี `Closed` → แก้ AllowedNextStatuses + เพิ่มปุ่ม "ส่งกลับสืบสวน" + `OnPostSendBackAsync`
- [x] Feature: Track.cshtml corruption section — เพิ่ม stepper 4 ขั้น + timeline + status descriptions (ละเอียดขึ้น)
- [x] Feature: Apply migration AddCorruptionSeq (CorruptionDbContext) — `dotnet ef database update --no-build`
- [x] Feature: TraffyFonduAdapter.cs — implement เต็มรูปแบบตาม Traffy Exchange API Spec:
  - JWT auth `POST /get-auth/v1` + token cache in-memory (SemaphoreSlim thread-safe, refresh 60s ก่อนหมด)
  - FetchNewAsync `GET /get-issues/v1?org_id={orgId}&duration=week`
  - PushStatusAsync `PATCH /update-issue/v1` + SRT status → Traffy status_id mapping
  - Phone normalise (strip non-digits, ตรวจ 9–10 หลัก)
- [x] Feature: TraffyWebhookController.cs — real-time push จาก Traffy
  - `POST /api/traffy-webhook/new-issue` → import เรื่องใหม่เข้า DB ทันที (dedup check ก่อน)
  - `PATCH /api/traffy-webhook/update-status` → sync สถานะกลับเข้า complaint (AuthorId null = system)
  - Webhook secret validation via `X-Traffy-Secret` header หรือ query `?secret=`
- [x] Config: appsettings.json เพิ่ม `TraffyFondue` block (ApiUrl, Username, Password, OrgId, WebhookSecret)
- [x] Feature: Staff/Queue.cshtml — ย้ายปุ่ม "ดึงคำร้อง" + modal หน่วยงาน จาก Admin → Staff Queue
  - เจ้าหน้าที่กดดึงได้เองจากหน้าคิวโดยตรง
  - `OnPostSyncAsync` ใน QueueModel — บันทึก log + result banner
  - Admin/ExternalSync ยังอยู่ไว้ดู history log

### Phase 19: AGENT REVIEW — Fix ทุกข้อจากรายงานตรวจสอบทั้งระบบ
- [x] Program.cs — Rate limiter (Submit/TrackVerify/Login) เปลี่ยนจาก `AddFixedWindowLimiter` (global bucket เดียว) เป็น `AddPolicy` + `RateLimitPartition.GetFixedWindowLimiter` แยกตาม client IP
- [x] Controllers/Api/TraffyWebhookController.cs — ย้าย business logic (dedup, import, status update) ออกไปที่ `IExternalSyncService.ImportFromWebhookAsync` / `ApplyStatusUpdateAsync` + เพิ่ม try/catch ให้ error response เป็น `{error: ...}` เหมือน controller อื่น
- [x] Validation/FileSignatureValidator.cs (ใหม่) — ตรวจ magic-byte ของไฟล์แนบ (jpg/png/pdf/doc/docx) เพิ่มจาก extension-only check ใน Submit.cshtml.cs + SubmitCorruption.cshtml.cs
- [x] Program.cs — เอา public `/uploads` static file mapping ออก, ย้ายเป็น `OnGetAttachmentAsync` handler ใน Staff/CaseDetail.cshtml.cs ที่ต้อง `[Authorize(Policy="StaffOnly")]` ก่อนถึงจะโหลดไฟล์แนบได้
- [x] SRT.Complaint.Tests/ (โปรเจกต์ใหม่) — xUnit + EF InMemory + Moq, 30 tests ครอบคลุม StaffUserService, SlaService, WebhookService, MaskingService, ExternalSyncService, FileSignatureValidator
- [x] ลบ `Dapper` package reference ที่ไม่ได้ใช้ (ขัดกับ CLAUDE.md "ไม่มี Dapper")
- [x] ComplaintService.cs / CorruptionService.cs — `NextSequenceAsync` เลิกรับ `sequenceName` เป็น parameter (เคย string-interpolate เข้า SQL), hardcode literal ในแต่ละ service แทน
- [x] Models/Corruption/{CorruptionReport,DecryptionLog,InvestigationLog}.cs — namespace แก้เป็น `SRT.Complaint.Models.Corruption` ให้ตรงกับ folder จริง (ก่อนหน้านี้ย้าย folder แต่ลืมเปลี่ยน namespace) + migration `FixCorruptionModelsNamespace` (Up/Down ว่างเปล่า — sync แค่ entity type name ใน EF snapshot, **ยังไม่ได้ apply ต้องรอ confirm**)
- [x] Services/WebhookService.cs — `WebhookMatchesEvent` เปลี่ยนจาก `catch { return false; }` เงียบ ๆ เป็น log warning พร้อม webhook id
- [x] Services/SlaService.cs — `CalculateDeadline` → `CalculateDeadlineAsync` (ใช้ `FirstOrDefaultAsync` แทน sync `FirstOrDefault`)
- [x] wwwroot/css/app.css — เพิ่ม `.input-validation-error` border/background (ไม่ใช้สีอย่างเดียวบอก error, WCAG 1.4.1) + `.form-input-compact`/`.form-select-compact`/`.form-textarea-compact` (unify input styling ที่เคย copy-paste ข้าม SubmitCorruption/Staff-CaseDetail/Corruption-CaseDetail/Admin-Users — คง theme สี amber/red ของแต่ละหน้าไว้ผ่าน utility class เพิ่มท้าย)
- [x] Pages/Public/Submit.cshtml — drop-zone `<div>` เพิ่ม `role="button" tabindex="0"` + keydown handler (เดิมกด keyboard ไม่ได้ ต้องคลิกอย่างเดียว) — SubmitCorruption.cshtml ใช้ `<label>` + sr-only input อยู่แล้ว ไม่ต้องแก้
- [x] Pages/Admin/Users.cshtml — temp-password modal เพิ่ม focus trap (Tab วนใน dialog) + auto-focus ปุ่มแรกตอนเปิด + Escape ปิด modal
- [x] Pages/Admin/Users.cshtml.cs — `OnPostToggleActiveAsync` เปลี่ยนจาก raw `NotFound()` เป็น TempData error + redirect ให้ behavior เหมือน Edit/ResetPassword/ViewPassword
- [x] Program.cs — `Jwt:Secret` เช็ค `string.IsNullOrWhiteSpace` แล้ว throw ถ้าไม่ตั้งค่า (เดิม fallback เป็น `""` เงียบ ๆ) — ตั้ง user-secret ให้ local dev แล้ว; session cookie เพิ่ม `CookieSecurePolicy.Always` ให้ตรงกับ Identity cookie
- [x] Pages/Staff/ChangePassword.cshtml.cs — `[Authorize]` เปลี่ยนเป็น `[Authorize(Policy = "StaffOnly")]` ให้ตรง convention หน้าอื่น
- [x] Pages/Shared/_NotificationBell.cshtml + _NotificationBellLink.cshtml (ใหม่) — แยก `<a id="notif-bell">` ออกจาก wrapper `<div aria-live="polite">` เพื่อไม่ให้ htmx outerHTML swap ทุก 20s สร้าง nested div เพิ่มเรื่อย ๆ, screen reader จะประกาศเมื่อ count เปลี่ยน
- [x] ตรวจสอบ Pages/Staff/CaseDetail.cshtml div nesting (เคยถูกรายงานว่า mismatch แถวบรรทัด 185-362) ด้วย script นับ depth — **เป็น false positive**, โครงสร้างสมดุลจริง ไม่ได้แก้อะไร
- Note: Terms/HomeContent ผ่าน `Sanitizer.Sanitize()` อยู่แล้วใน TermsService/ContentBlockService — รายงานเดิมที่บอกว่าไม่มี sanitize เป็น false positive เช่นกัน (เช็คแค่ ViewModel validation attribute ไม่ได้ตาม flow ไปที่ Service)
- **ค้างอยู่ (ต้องยืนยันก่อน apply):** `dotnet ef database update --context CorruptionDbContext` สำหรับ migration `FixCorruptionModelsNamespace` — Up/Down ว่างเปล่า ไม่กระทบ schema จริง แค่ sync EF snapshot metadata ตาม CLAUDE.md migration protocol ต้องรอ user confirm ก่อน

### Phase 20: AGENT REVIEW — Traffy Fondue Exchange API vs เอกสารสเปกจริง
- [x] อ่าน `docs/traffy-fondue-exchange-api.md` (717 บรรทัด) เทียบกับ `TraffyFonduAdapter.cs` + `TraffyWebhookController.cs` ทีละ endpoint — พบจุดที่ไม่ตรงกันหลายจุด แก้ทั้งหมดแล้ว:
  - **get-auth token parsing** — โค้ดเดิม deserialize `token`/`expire_timestamp` เป็น top-level field ของ `long`, แต่สเปกบอกว่าอยู่ใต้ `results` และ `expire_timestamp` เป็น string วันที่ (ไม่ใช่ Unix timestamp) — **หมายเหตุ: เอกสาร JSON example ของจุดนี้เขียนผิด syntax เอง (ผสม array กับ object) และไม่มี Traffy credentials ให้ทดสอบจริง** จึงเขียน `ParseAuthResponse` แบบ defensive รองรับทั้ง 2 รูปแบบ (results เป็น object/array-of-one, expire_timestamp เป็น number/string) พร้อม log warning ถ้าเจอ shape ที่ไม่คาดคิด — **ต้อง verify กับ account จริงก่อนขึ้น production**
  - **status_id เดิม hardcode เป็น 1-4** — แต่จริง ๆ เป็นเลขเฉพาะแต่ละหน่วยงานจาก `get-status-list` (ตัวอย่างในสเปกเป็นเลขสุ่มเช่น 143562, 81, 192003) — เปลี่ยนเป็น cache `get-status-list` (TTL 6 ชม.) แล้ว map ผ่าน `status_type` (start/inprogress/finish/irrelevant/follow) ซึ่งเป็นค่าคงที่ข้าม org แทน
  - **Webhook update-status ใช้ `status_id` แต่ payload จริงไม่มีฟิลด์นี้เลย** — ส่งมาแค่ `status` (ข้อความ) — เปลี่ยนไปอ่าน `status` string แล้ว resolve ผ่าน `IExternalStatusResolver.ResolveSrtStatusAsync` (เทียบกับ cached get-status-list) แทน
  - **`duration=week`** ไม่ใช่ค่าที่สเปกรองรับ (มีแค่ `today`/`all`) — เปลี่ยนเป็น `today` — **หมายเหตุ: ถ้าไม่มีการ sync ทุกวัน เรื่องจากวันที่ข้ามไปจะหายถาวร (ไม่มี "yesterday"/range) ควรพิจารณาทำ background sync รายวันเป็น follow-up**
  - เพิ่ม `Complaint.Latitude`/`Longitude` (migration `AddComplaintGeoLocation` — เตรียมไว้ ยังไม่ apply) + ดาวน์โหลดรูป `photo` จาก Traffy มาเก็บเป็น `ComplaintAttachment` จริง (เดิมไม่ได้เก็บเลยทั้งพิกัดและรูป) — ตรวจ Content-Type ต้องเป็น image/* ก่อนเซฟ
  - `ExternalComplaintDto.CategoryHint` เดิมไม่ได้ใช้เลย (import เข้าหมวด default เสมอ) — เพิ่ม exact-name match กับ `ComplaintCategory.Name` ก่อน fallback default
  - ลบฟิลด์ที่ไม่มีจริงใน response ออกจาก DTO: `Name`/`Phone`/`Topic`/`StatusId` (get-issues และ webhook ไม่ส่ง reporter contact info หรือ topic เลย) — **หมายความว่า complaint ที่ import จาก Traffy จะ track ผ่านหน้า Track ด้วยเบอร์โทรไม่ได้ เพราะ ReporterPhone จะเป็น "-" เสมอ**
  - เพิ่ม `EnsureTraffySuccess` เช็ค `status`/`message` ใน response body (Traffy ส่ง error แบบ `{"status":"fail",...}` มาพร้อม HTTP 200 — เดิมเช็คแค่ HTTP status code เท่านั้น ทำให้ error ถูกมองข้ามเงียบ ๆ)
  - เพิ่ม `SRT.Complaint.Tests`: 4 test ใหม่ (category matching ×2, photo download ×2) รวมเป็น 34 tests ผ่านหมด
  - **ค้างอยู่ (ต้องยืนยันก่อน apply):** `dotnet ef database update --context AppDbContext` สำหรับ migration `AddComplaintGeoLocation` (เพิ่ม column Latitude/Longitude แบบ nullable, ไม่กระทบข้อมูลเดิม)
  - **ยังไม่มี Traffy credentials จริงในระบบ (Username/Password/OrgId ว่างหมด)** — ต้องขอจากทีมที่ประสานงานกับ Traffy แล้วทดสอบ get-auth จริงก่อน deploy เพื่อยืนยัน response shape
- [x] `Services/ExternalSyncBackgroundService.cs` (ใหม่) — sync ทุกระบบภายนอกที่ `IsConfigured` อัตโนมัติทุก 24 ชม. (แก้ปัญหา `duration=today` ที่ต้อง sync ทุกวันไม่งั้นเรื่องหาย) — ใช้ pattern เดียวกับ `SlaBackgroundService` (IServiceScopeFactory + catch ต่อรอบ)
  - `ExternalSyncLog.TriggeredById` เปลี่ยนเป็น `int?` (null = background sync, มีค่า = staff กดเอง) ตาม pattern เดียวกับ `ComplaintNote.AuthorId` (ClientSetNull) — migration `MakeExternalSyncLogTriggeredByNullable` เตรียมไว้ **รอยืนยันก่อน apply**
  - เพิ่ม test `SyncAsync_AcceptsNullTriggeredBy_ForScheduledBackgroundSync` รวมเป็น 35 tests ผ่านหมด
  - Staff/Queue.cshtml ปุ่ม "ดึงคำร้อง" (มีอยู่แล้ว) ยังใช้งานได้ปกติ ไม่กระทบ — ตอนนี้แค่เสริมด้วย background sync อัตโนมัติ ไม่ต้องพึ่งพนักงานกดทุกวัน
- [x] **หน้าตั้งค่า Traffy Fondue ใน Admin/ExternalSync** — ย้าย credentials (Username/Password/OrgId/WebhookSecret) จาก appsettings.json/user-secrets มาเป็นฟอร์มใน Admin UI แทน แก้ได้โดยไม่ต้อง redeploy:
  - Model ใหม่ `TraffySettings` (ตารางแถวเดียว แบบเดียวกับ `ComplaintTerms`) — Password/WebhookSecret เข้ารหัสด้วย `IMaskingService` (AES) ก่อนบันทึก ไม่เคยเก็บ plaintext ลง DB
  - `ITraffySettingsService`/`TraffySettingsService` — `GetAsync` (decrypted, adapter ใช้เองเท่านั้น), `GetStatusAsync` (masked, สำหรับ UI, ไม่เคยส่งค่าจริงกลับ), `SaveAsync` (เว้นว่าง Password/WebhookSecret = คงค่าเดิมไว้ ไม่ทับ)
  - `TraffyFonduAdapter` เปลี่ยนจากอ่าน `IConfiguration` ตอน construct (ครั้งเดียว, ต้อง restart ถ้าจะเปลี่ยน) เป็นอ่านจาก DB ผ่าน `IServiceScopeFactory` + cache 5 นาที (Singleton เรียก Scoped service ผ่าน scope factory เหมือน pattern เดิมของ `SlaBackgroundService`)
  - `IsConfigured` ยังเป็น sync property ตามที่ interface กำหนด (Razor view เรียกใช้ตรง ๆ) — ใช้ `.GetAwaiter().GetResult()` บน cache สั้น ๆ แทนที่จะแก้ interface ทั้งระบบ (ASP.NET Core ไม่มี SynchronizationContext เลยไม่เสี่ยง deadlock)
  - เพิ่ม `ITraffyConnectionTester` (InvalidateSettingsCache + TestConnectionAsync) ให้ Admin กด "ทดสอบการเชื่อมต่อ" ยิง get-auth จริงทันทีและเห็นผลลัพธ์/ error message จริงจาก Traffy
  - `TraffyWebhookController.ValidateSecretAsync` เปลี่ยนจากอ่าน `IConfiguration` เป็นอ่าน `ITraffySettingsService` เช่นกัน (จุดเดียวที่ตกหล่นตอนแรก)
  - ลบ section `TraffyFondue` ออกจาก appsettings.json ทั้งหมด (ไม่มีใครอ่านแล้ว)
  - เพิ่ม migration `AddTraffySettings` (สร้างตารางใหม่ ไม่กระทบข้อมูลเดิม) — **รอยืนยันก่อน apply**
  - เพิ่ม test `TraffySettingsServiceTests.cs` (4 tests: encrypt round-trip, blank=keep-existing, masked status ไม่หลุด secret, default เมื่อยังไม่ตั้งค่า) รวมเป็น 39 tests ผ่านหมด
  - **หมายเหตุ:** migration `AddComplaintGeoLocation` / `MakeExternalSyncLogTriggeredByNullable` / `AddTraffySettings` apply แล้ว (ผู้ใช้ยืนยัน "เรียบร้อย")

### Phase 21: Sidebar Menu — ExternalSync link + Collapsible sections
- [x] `Pages/Shared/_StaffLayout.cshtml` — เพิ่มลิงก์ "ดึงคำร้องจากระบบภายนอก" (`/Admin/ExternalSync`) ใต้หัวข้อ "ตั้งค่าระบบ" ถัดจาก "การแจ้งเตือน"
- [x] เมนู 3 กลุ่มหลัก (เรื่องทั่วไป/เรื่องทุจริต/ผู้ดูแลระบบ) collapse/expand ได้ — เก็บ state ผ่าน localStorage ต่อกลุ่ม (`sidebar-collapsed-{id}`) + กลุ่มที่มีหน้าปัจจุบัน (active link) จะ force-expand เสมอไม่ว่าจะเคย collapse ไว้หรือไม่ กัน user หลงทาง
- [x] ใช้ CSS `grid-template-rows: 1fr → 0fr` animation (ไม่ต้องคำนวณ height ด้วย JS) — เพิ่ม `.section-title-btn`/`.section-chevron`/`.sidebar-section-body` ใน app.css
- Note: กลุ่มย่อยภายใน "ผู้ดูแลระบบ" (ภาพรวม/ตั้งค่าระบบ/เนื้อหา/Developer) ยังเป็น label เฉย ๆ ไม่ได้ทำ collapse ซ้อนกัน — เพื่อไม่ให้ซับซ้อนเกินไป ถ้าต้องการ nested collapse ค่อยทำเพิ่มทีหลัง

### Phase 22: แนบรูป/ไฟล์ในบันทึก "ตอบกลับผู้ร้อง" ให้ขึ้นในหน้าติดตาม
- [x] `ComplaintAttachment` เพิ่ม `ComplaintNoteId` (nullable FK ไป `ComplaintNote`) — ใช้ `ClientSetNull`/NO ACTION ไม่ใช่ Cascade เพราะ `Complaint` cascade ไปหา `ComplaintAttachment` โดยตรงอยู่แล้ว ถ้า cascade ซ้อนผ่าน `ComplaintNote` จะชน SQL Server multiple cascade paths — migration `AddNoteAttachmentLink` **รอยืนยันก่อน apply**
- [x] `IComplaintService.AddNoteAsync` เพิ่ม parameter `attachments` (optional) — `ComplaintService` บันทึกไฟล์ผ่าน `SaveAttachmentsAsync` เดิม (เพิ่ม `noteId` param) หลังจากสร้าง note แล้ว (ต้องมี note.Id ก่อนถึงจะ link ได้)
- [x] `Staff/CaseDetail.cshtml.cs` — ฟอร์ม "เพิ่มบันทึก" รับไฟล์แนบได้แล้ว (`enctype="multipart/form-data"`, `NoteAttachments`) ตรวจ extension whitelist + magic-byte (`FileSignatureValidator` เดิม) + ขนาด/จำนวนไฟล์ (เหมือนฟอร์มสาธารณะ: 5 ไฟล์, 10MB/ไฟล์, jpg/png/pdf/doc/docx)
- [x] `Staff/CaseDetail.cshtml` — แสดงไฟล์แนบใต้แต่ละ note (ทั้ง internal และ public reply) ให้เจ้าหน้าที่เห็นด้วย ผ่าน handler `OnGetAttachmentAsync` เดิม (StaffOnly)
- [x] `Public/Track.cshtml.cs` — เพิ่ม `OnGetAttachmentAsync(reference, attachmentId)` endpoint ใหม่สำหรับประชาชน **จำกัดเฉพาะไฟล์แนบของ note ที่เป็น "PublicReply" เท่านั้น** (ไม่มีทางเห็นไฟล์แนบใน internal note) และต้องผ่าน session verification (`track_v_{ref}`) เดียวกับที่ใช้ดูรายละเอียดเรื่องอยู่แล้ว — กัน enumerate attachment ข้ามเรื่อง/ข้าม note type
- [x] `Public/Track.cshtml` — timeline แสดงรูป/ไฟล์แนบในบับเบิล "ข้อความจากเจ้าหน้าที่" (รูปแสดง thumbnail คลิกขยายได้ ไฟล์อื่นแสดงเป็น chip ดาวน์โหลด)
- [x] `ComplaintService.GetByIdAsync`/`GetByReferenceAsync` เพิ่ม `.ThenInclude(n => n.Attachments)` ให้โหลดไฟล์แนบของ note มาด้วย
- [x] เพิ่ม test `ComplaintServiceAddNoteTests.cs` (2 tests: บันทึกไฟล์แล้ว link เข้า note ถูกต้อง / ไม่แนบไฟล์ก็ยังสร้าง note ได้ปกติ) รวมเป็น 41 tests ผ่านหมด
- **ค้างอยู่ (ต้องยืนยันก่อน apply):** migration `AddNoteAttachmentLink` — ไม่มีผลกับข้อมูลเดิม เพิ่ม column nullable อย่างเดียว

### Phase 23: Bug fix — Admin/HomeContent อัปโหลดรูปไม่ขึ้น (path/PathBase)
- [x] `Pages/Admin/HomeContent.cshtml` — JS `fetch('/Admin/HomeContent?handler=UploadImage', ...)` เป็น hardcoded absolute path จาก root ไม่รู้จัก PathBase ของ IIS sub-application (`/complaint/`) → ตอบกลับเป็นหน้า error HTML แทน JSON (`Unexpected token '<'`) แก้เป็น `@Url.Page("HomeContent", "UploadImage")`
- [x] `Pages/Admin/HomeContent.cshtml.cs` OnPostUploadImageAsync — เดิม return `url = $"/uploads/home-content/{storedName}"` ก็ hardcoded root path เหมือนกัน (คนละบั๊ก แต่สาเหตุเดียวกัน) ทำให้ไฟล์เขียนลงดิสก์สำเร็จแต่รูปไม่ขึ้น (404) ทั้งตอน preview ทันทีและตอน reload หน้า เพราะ URL ที่เก็บลง DB ผ่าน `ImageUrl` field ก็คือ URL เดียวกับที่ endpoint ตอบกลับ แก้เป็น `Url.Content($"~/uploads/home-content/{storedName}")`
- Note: รูปที่เคย "อัปโหลดสำเร็จ" ก่อน fix นี้ ยังมี `ImageUrl` เดิม (ไม่มี prefix ที่ถูกต้อง) ค้างอยู่ใน DB — ต้องอัปโหลดรูปใหม่ทับอีกครั้งหลัง deploy fix (ไฟล์เก่าบน disk ยังอยู่ที่ `wwwroot/uploads/home-content/` ไม่หาย แค่ URL ที่ผูกไว้ผิด)

### Phase 24: Retire Traffy Fondue Integration
- [x] ลบ inbound endpoints `POST /api/traffy-webhook/new-issue` และ `PATCH /api/traffy-webhook/update-status`
- [x] ลบ `TraffyFonduAdapter`, credential/settings service, connection tester และ DI registrations ทั้งหมด
- [x] ลบฟอร์มตั้งค่า/ทดสอบ Traffy ออกจาก `Admin/ExternalSync` โดยยังคงหน้า history และ adapter framework สำหรับระบบภายนอกอื่น
- [x] ลบ Traffy API reference document และปรับ README ไม่ให้ประกาศ endpoint/config ที่เลิกใช้แล้ว
- [x] ลบ Traffy-specific tests และเปลี่ยน ExternalSync tests ให้ใช้ system key กลาง (`test_external`)
- [x] `dotnet test SRT.Complaint.sln` ผ่าน 37/37 tests (มี warning nullable เดิม 1 จุดใน WebhookServiceTests)
- Note: ยังเก็บ `TraffySettings` model/table และ migration history ไว้ชั่วคราวเพื่อไม่ให้เกิด destructive schema change โดยไม่ตั้งใจ ไม่มี runtime service หรือ endpoint ใดอ่านข้อมูลนี้แล้ว หากต้องการลบตารางให้สร้าง migration แยกและตรวจสอบก่อน apply ตาม migration protocol

### Phase 25: eDOC Outbound Integration
- [x] เพิ่มปุ่ม `สร้างเอกสาร eDOC` ใน Case Detail โดยใช้ service account เบื้องหลัง ผู้ใช้ไม่ต้อง login eDOC
- [x] รองรับหลาย Basket, server-side allowlist และ default mapping ตาม Complaint Category
- [x] รองรับ login/token cache, create, upload attachment, inquiry/reconciliation และ tracking
- [x] เพิ่ม durable state ป้องกันส่งซ้ำ: Pending/Sending/Sent/NeedsReview/Failed พร้อม unique Complaint/WID
- [x] เพิ่ม manual retry สำหรับ SuperAdmin และแยก retry ไฟล์แนบจากการสร้างเอกสาร
- [x] เพิ่ม HTTPS/hostname/DNS pinning, block private network, response/file limits และไม่ log credentials/token/body
- [x] เพิ่ม migration `AddEdocOutboundIntegration` และ SQL review artifact `db/edoc-outbound-integration.sql`
- [x] เพิ่มคู่มือตั้งค่า/UAT และ security review ใน `docs/`
- [x] Build ผ่าน 0 warnings/0 errors และ tests ผ่าน 56/56
- [ ] ก่อน Production: rotate credentials ที่อยู่ในเอกสารตัวอย่าง, ขอ Production URL, ยืนยัน WID idempotency/file limits และผ่าน UAT อย่างน้อย 2 Basket

### Phase 26: PDF Complaint Form Update ครั้งที่ 1/2569
- [x] ข้อ 1 ส่วนหัว PDF — เปลี่ยนโทรศัพท์เป็น `0 2220 4261, 4268, 4297`, อีเมลเป็น `pr.railway.srt@gmail.com` และนำวันที่พิมพ์ออกจากส่วนหัว
- [x] เพิ่มตัวเลือกบังคับในฟอร์มประชาชน `Disclose/Conceal` และบันทึกค่าไว้กับเรื่องร้องเรียน
- [x] หลังบ้านแสดง PDF เพียงรูปแบบเดียวตามที่ประชาชนเลือก และ handler อ่านค่าจากฐานข้อมูลโดยตรงเพื่อป้องกันการแก้ request; รายการเก่าหรือค่าไม่ถูกต้องใช้แบบปกปิดอัตโนมัติ
- [x] Apply migration `AddReporterPrivacyChoice` แล้ว (2026-10-29)
- [x] เพิ่มเครื่องหมายถูกและข้อความเปิดเผย/ปกปิดข้อมูลใน PDF โดยแสดงเฉพาะข้อความที่ตรงกับตัวเลือกของประชาชน
- [x] เพิ่มข้อความสั่งการ 2 รายการพร้อมช่องทำเครื่องหมายใน PDF ตามเอกสาร Update-1
- [x] เพิ่มลายเซ็น ชื่อ ตำแหน่ง และตำแหน่งปฏิบัติการแทน พร้อมหน้า Admin `PdfSettings` สำหรับเปลี่ยน Master เมื่อตำแหน่งหรือผู้ลงนามเปลี่ยน
- [x] เพิ่มข้อมูลผู้ประสานงาน เบอร์โทร หมายเหตุ เลขหน้า และวันที่พิมพ์ตัวเล็กที่ท้ายกระดาษ
- [x] นำเฉพาะไฟล์แนบรูปภาพมาแสดงใน PDF โดยสร้างหน้าภาคผนวกไม่เกินหน้าละ 2 รูป; PDF/DOCX/XLSX และไฟล์ชนิดอื่นไม่สร้างหน้าเพิ่ม
- [x] Apply migration `AddPdfSignerSettings` แล้ว (2026-10-29) และยืนยันว่า AppDbContext ไม่มี migration ค้าง
- Note: การ `database update` ก่อนหน้านี้ apply migration `AddEdocOutboundIntegration` ที่ค้างอยู่ตามลำดับ EF ด้วย
- Note: Build ผ่าน 0 warnings/0 errors และ test suite ผ่าน 65/65
- Note: ย้ายหมายเหตุ 3 ข้อไว้เหนือเส้นแบ่ง footer แล้ว และสร้าง/เรนเดอร์ตรวจ mockup ทั้งแบบเปิดเผยและปกปิด ชุดละ 3 หน้า ข้อความ/ลายเซ็น/footer ไม่ล้นหรือทับกัน ภาพแนบจัด 2 รูปในหน้าแรกกับ 1 รูปในหน้าถัดไป และ PDF/DOCX/XLSX ที่ใช้ทดสอบไม่สร้างหน้าเพิ่ม
- Note: ทดสอบรายละเอียดคำร้องยาว 22 ย่อหน้าแล้ว ระบบแบ่งเป็น 3 หน้าอัตโนมัติ โดยข้อความต่อเนื่องไม่ถูกตัด ส่วนช่องทำเครื่องหมายและลายเซ็นเลื่อนไปอยู่หลังรายละเอียดในหน้าสุดท้าย

### Phase 27: Website Update ครั้งที่ 2/2569
- [x] แยกสีเมนูหน้าหลัก: เขียว/ส้ม/เหลือง/ฟ้า พร้อม focus state และข้อความกำกับ
- [x] ฟอร์มไฟล์แนบเลือกเพิ่มได้หลายครั้ง พรีวิว/เปิดดู/ลบได้ สูงสุด 10 ไฟล์, 10 MB ต่อไฟล์, รวม 50 MB และตั้ง IIS/ASP.NET request limit 55 MB
- [x] เพิ่มวัตถุประสงค์การแจ้งเรื่อง 2 ตัวเลือกและบันทึกในคำร้อง
- [x] เพิ่มหน้าใบรับเรื่องหลังส่งสำเร็จ แสดงรหัส คัดลอก พิมพ์/บันทึก PDF และลิงก์ติดตามสถานะ
- [x] คิวหลังบ้านเรียงเรื่องที่ยังไม่เสร็จจากเก่าไปใหม่ก่อนเรื่องที่ปิดแล้ว
- [x] ค้นหาจากรายละเอียด เลขเรื่อง ชื่อ เบอร์โทร อีเมล สถานที่ ประเภท หัวข้อย่อย และฝ่าย พร้อมช่วงวันที่
- [x] ส่งออกรายละเอียด/ข้อมูลติดต่อเป็น Excel, PDF และ Word ทั้งรายเรื่องและรายงานรวม
- [x] เจ้าหน้าที่ผู้รับผิดชอบหรือ SuperAdmin แก้ประเภท หัวข้อย่อย และฝ่ายรับผิดชอบได้จาก Case Detail
- [x] ลดขั้นตอนปิดงาน: หลังรับเรื่อง เจ้าหน้าที่เลือกประโยคขึ้นต้น + กรอกรายละเอียดสรุปผล แล้วบันทึกเป็น `Closed` ได้ทันทีโดยไม่ผ่าน `Resolved`
- [x] เพิ่ม migration `20261029093150_AddUpdate2ComplaintFeatures` และตรวจ SQL idempotent script แล้ว: เพิ่ม nullable columns 2 ช่อง ไม่มี FK/cascade/destructive change
- [x] Build ผ่าน 0 warnings/0 errors และ tests ผ่าน 73/73
- [x] Apply migration `AddUpdate2ComplaintFeatures` บน LocalDB แล้ว (2026-10-29) และตรวจ migration history ยืนยันว่าเป็นรายการล่าสุด
- Note: เปิดระบบหลัง apply สำเร็จ หน้า `/` และ `/Public/Submit` ตอบ HTTP 200 และตรวจพบสีเมนูใหม่, ตัวเลือกวัตถุประสงค์ และ JavaScript เลือกไฟล์สะสมใน HTML ที่ render แล้ว

## Notes / Issues พบระหว่างทำ

- ใช้ `@page "{id:int}"` สำหรับ CaseDetail ให้ URL เป็น `/Staff/CaseDetail/123`
- Login.cshtml override layout เป็น `_Layout` (public)
- SlaBackgroundService ใช้ `IServiceScopeFactory` เพราะ DbContext เป็น Scoped service
- `ComplaintQueueFilter` เป็น positional record
- Track.cshtml ใช้ `?Ref=xxx` parameter (case-insensitive bind)
- PDF download ใช้ POST form + AntiForgeryToken เพื่อป้องกัน CSRF
- CorruptionSubmitted: TempData["CorruptionRef"] ถ้าไม่มีให้ redirect กลับ SubmitCorruption
- Track COR- detection: ถ้า ref ขึ้นต้นด้วย "COR-" ให้ query CorruptionService
- Satisfaction form แสดงเฉพาะเมื่อ Status = Closed และ SatisfactionScore = null
- Webhook SecretHash เก็บ raw secret (hex 32 bytes) สำหรับ HMAC-SHA256 signing
- Admin/Dashboard: ห้ามใช้ Task.WhenAll + ContinueWith กับ EF Core DbContext เดียวกัน — ใช้ sequential await แทน
- MustChangePassword middleware: ต้อง whitelist /Staff/ChangePassword, /Staff/Logout, /favicon, /css/, /js/, /lib/ เพื่อไม่ให้ redirect loop
- GenerateTempPassword ใช้ RandomNumberGenerator.GetInt32 (thread-safe, crypto-safe) แทน Random
- AuditLog export: ใช้ POST form พร้อม hidden filter fields (SupportsGet = true ยังรับ POST ได้)
- [EnableRateLimiting("SubmitPolicy")] ใช้ที่ method level ใน OnPostAsync
- WebhookRetryService.RetryPendingAsync เรียกผ่าน IServiceScopeFactory scope
- StatsController /detailed ใช้ in-memory average calculation แทน EF.Functions.DateDiffHour

## Tech Stack Reference

- ASP.NET Core 9 Razor Pages
- EF Core 9 + SQL Server
- Tailwind CSS + HTMX
- BCrypt.Net-Next 4.2.0
- QuestPDF + MailKit
- Serilog
- ClosedXML (Excel export — Reports + AuditLog)
- Chart.js 4.x (reports)
- HMAC-SHA256 (webhook signing)
