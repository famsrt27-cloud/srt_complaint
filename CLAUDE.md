# ระบบรับเรื่องร้องเรียน การรถไฟแห่งประเทศไทย

## ภาพรวมโปรเจกต์

ระบบรับเรื่องร้องเรียนออนไลน์ของ รฟท. รองรับ 2 Track:
- **เรื่องทั่วไป:** ยื่นเรื่อง ติดตาม และ จัดการโดยเจ้าหน้าที่
- **เรื่องทุจริต:** Track แยก เข้ารหัสข้อมูลผู้แจ้ง ใช้ DB Schema แยก

URL: `https://www.railway.co.th/complaint/`
Deploy: IIS Application บน GDCC

## Tech Stack

- **Framework:** ASP.NET Core 8 (Razor Pages)
- **Frontend:** Tailwind CSS + HTMX
- **Database:** SQL Server (EF Core 8 + Dapper)
- **Auth:** ASP.NET Core Identity (Cookie) + JWT (API)
- **PDF:** QuestPDF
- **Email:** MailKit
- **SMS:** HTTP Client → SMS Gateway

## โครงสร้างโปรเจกต์

```
SRT.Complaint/
├── Controllers/Api/        ← API endpoints สำหรับ External
├── Data/
│   ├── AppDbContext.cs     ← EF Core DbContext หลัก
│   └── CorruptionDbContext.cs ← DbContext แยกสำหรับทุจริต
├── Models/
├── Services/               ← Business logic แยกออกจาก Pages
├── Pages/
│   ├── Public/             ← ยื่นเรื่อง, ติดตาม (ไม่ต้อง Login)
│   ├── Staff/              ← เจ้าหน้าที่ทั่วไป
│   ├── Corruption/         ← เจ้าหน้าที่ทุจริต
│   └── Admin/              ← Super Admin
└── wwwroot/
```

## Database Schema Guideline

- เรื่องทั่วไป: ใช้ Schema `dbo.*`
- เรื่องทุจริต: ใช้ Schema `corruption.*` (แยกสมบูรณ์)
- ข้อมูลผู้แจ้งทุจริต: เข้ารหัส AES-256, เก็บ Masked version แยก

### ตัวอย่างตารางหลัก

- `dbo.StaffUsers` — เจ้าหน้าที่ (Login ด้วยเลขพนักงาน 7 หลัก)
- `dbo.Complaints` — เรื่องร้องเรียนทั่วไป
- `corruption.Reports` — เรื่องทุจริต (Schema แยก)
- `dbo.ApiKeys` — API Management
- `dbo.ApiRequestLogs` — Log ทุก API request

ดูรายละเอียดเต็มใน `srt-complaint-system-spec.md`

## Coding Standards

### C# Style

- **Naming:** PascalCase สำหรับ class/method/property, camelCase สำหรับ local variables
- **Async:** ทุก I/O operation ต้องเป็น async/await
- **Null handling:** ใช้ nullable reference types (`#nullable enable`)
- **Error handling:** ใช้ try-catch ที่ Controller/Service layer พร้อม logging

### Razor Pages

- **One PageModel per feature** — ไม่ควรมี PageModel ใหญ่เกินไป แยก Service ออกมา
- **ViewModel pattern:** สร้าง ViewModel สำหรับ binding form แทนการใช้ Model ตรง ๆ
- **CSRF:** ทุก Form ต้องมี `@Html.AntiForgeryToken()`

### Frontend

- **Tailwind CSS:** ใช้ utility classes ไม่เขียน custom CSS เว้นแต่จำเป็น
- **HTMX:** สำหรับ partial update, form submission แบบ AJAX
- **JavaScript:** เขียนน้อยที่สุด ใช้ HTMX attributes เป็นหลัก

## Architecture Rules

- **Service Layer Pattern:** Business logic อยู่ใน `Services/` ไม่ใช่ใน PageModel
- **Data access:** ใช้ EF Core ทั้งหมด (ไม่มี Dapper ในโปรเจกต์นี้) — query ที่ซับซ้อนอยู่ใน Service layer ไม่ใช่ PageModel
- **Separation of Concerns:** เรื่องทั่วไปกับเรื่องทุจริตไม่ share Service — แยกสมบูรณ์ (cross-context lookup เช่นชื่อเจ้าหน้าที่ ให้ผ่าน `IStaffDirectoryService` ไม่ inject `AppDbContext` ตรงเข้า Corruption PageModel)

## Testing Strategy

### Unit Tests (ใช้ xUnit)

- ทดสอบ Services logic ทั้งหมด
- Mock DbContext ด้วย InMemory Database หรือ Moq

### Integration Tests

- ทดสอบ API endpoints ด้วย WebApplicationFactory
- ทดสอบ Razor Pages ที่มี database interaction

### Manual Testing Checklist

- [ ] ยื่นเรื่องทั่วไป → รับ SMS/Email
- [ ] ยื่นเรื่องทุจริต → ข้อมูลถูก Mask
- [ ] เจ้าหน้าที่รับเรื่อง → Auto-assign ถ้าไม่มีคนรับ
- [ ] Super Admin สร้าง API Key → Test ด้วย Postman

## Build & Run

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run (Development)
dotnet run --project SRT.Complaint

# Run migrations
dotnet ef database update --project SRT.Complaint

# Create migration
dotnet ef migrations add MigrationName --project SRT.Complaint
```

## Migration Process (บังคับทำตามขั้นตอนนี้ทุกครั้ง)

ทุกครั้งที่สร้าง Migration ใหม่ ให้ทำตามลำดับนี้เสมอ — ห้ามข้ามขั้นตอน:

### ขั้นที่ 1: ตรวจสอบก่อน apply

ก่อน `dotnet ef database update` ให้วิเคราะห์ migration file ที่สร้างขึ้นมาก่อนเสมอ โดยตรวจสอบประเด็นต่อไปนี้:

1. **Cascade path (SQL Server ข้อจำกัด)**
   - SQL Server ไม่อนุญาต `ON DELETE CASCADE` หรือ `ON DELETE SET NULL` ที่ทำให้เกิด multiple cascade paths ไปยัง table เดียวกัน
   - ถ้า FK ใหม่ชี้ไปยัง table ที่มี cascade path อยู่แล้ว → ใช้ `DeleteBehavior.ClientSetNull` หรือ `DeleteBehavior.NoAction` แทน `SetNull` / `Cascade`
   - Pattern ที่เสี่ยง: `Complaints → ComplaintCategories → ComplaintSubCategories → Complaints` (loop)

2. **Breaking changes**
   - Column ที่เปลี่ยน type หรือลด maxLength อาจทำให้ข้อมูลเดิมเสียหาย
   - ลบ column ที่ยังมีข้อมูลอยู่

3. **Index / Unique constraint ซ้ำ**
   - ตรวจว่า migration ไม่พยายาม create index ที่มีอยู่แล้ว

### ขั้นที่ 2: แจ้งผลการวิเคราะห์

หลังตรวจสอบ migration file แล้ว **ให้หยุดและแจ้งผลก่อน** ในรูปแบบ:

```
✅ ตรวจสอบ migration แล้ว — ไม่พบปัญหา
   - สร้างตาราง: [รายการ]
   - เพิ่ม column: [รายการ]
   - FK / cascade: [ระบุ behavior]
   พร้อม apply — รอการยืนยัน
```

หรือถ้าพบปัญหา:

```
⚠️ พบปัญหาใน migration — ยังไม่ apply
   สาเหตุ: [อธิบาย]
   วิธีแก้: [วิธีแก้ไข AppDbContext / migration]
```

### ขั้นที่ 3: Apply เมื่อได้รับการยืนยัน

```bash
dotnet ef database update --project SRT.Complaint --context AppDbContext
```

## Common Tasks

### สร้าง Razor Page ใหม่

```bash
# สร้าง Page พร้อม PageModel
dotnet new page -n PageName -o Pages/FolderName
```

### เพิ่ม NuGet Package

```bash
dotnet add package PackageName
```

### สร้าง Service ใหม่

1. สร้าง Interface ใน `Services/IServiceName.cs`
2. สร้าง Implementation ใน `Services/ServiceName.cs`
3. Register ใน `Program.cs`: `builder.Services.AddScoped<IServiceName, ServiceName>()`

## Security Checklist

- [ ] Password hashing: BCrypt cost factor 12
- [ ] ข้อมูลทุจริต: AES-256-CBC encryption
- [ ] Session: HttpOnly + Secure + SameSite=Strict
- [ ] CSRF: Anti-forgery token ทุก Form
- [ ] File upload: ตรวจ MIME type + Extension whitelist
- [ ] SQL Injection: Parameterized query เสมอ
- [ ] Rate Limiting: จำกัด Submit 5 ครั้ง/IP/ชั่วโมง

## API Development

### Endpoint Structure

```
POST   /api/complaints                    (Scope: complaints:write)
GET    /api/complaints/{ref}              (Scope: complaints:read)
GET    /api/complaints/{ref}/edoc-payload (Scope: complaints:edoc)
```

### API Key Validation Flow

1. อ่าน Key จาก Header `X-API-Key`
2. Hash Key → เทียบกับ DB
3. ตรวจ IsActive + ExpiresAt
4. ตรวจ IP Whitelist
5. ตรวจ Rate Limit (Sliding Window 1 นาที)
6. ตรวจ Scope
7. บันทึก ApiRequestLog

## Deployment (IIS on GDCC)

### Pre-deployment Checklist

- [ ] ได้ Connection String จาก GDCC
- [ ] ได้ SMTP credentials
- [ ] ได้ SMS Gateway API
- [ ] กำหนด AES Encryption Key
- [ ] ตั้ง Environment Variables ใน IIS

### Deploy Steps

1. `dotnet publish -c Release -o ./publish`
2. Copy `./publish` → `C:\inetpub\wwwroot\complaint\`
3. ตั้งค่า Environment Variables
4. Run Migration: `dotnet ef database update`
5. ตรวจสอบ `https://www.railway.co.th/complaint/`

## Key Files Reference

- **Spec:** `srt-complaint-system-spec.md` — Technical Specification ฉบับเต็ม
- **Design:** ดู Visualization ที่สร้างไว้สำหรับ API Management, Queue System, Workflow

## Notes for Claude

- **อย่าเขียน inline SQL strings** — ใช้ EF Core หรือ Dapper กับ parameterized query
- **ทุก Service ต้องมี Interface** — เพื่อ testability
- **เรื่องทุจริตห้าม share code กับเรื่องทั่วไป** — แม้ว่า logic คล้ายกัน
- **Secret/Key ห้ามอยู่ใน code** — ต้องอยู่ใน Environment Variables หรือ `appsettings.Production.json` (ไม่ commit)

## Operational Notes (สิ่งที่ต้องทำหลังแก้ไข/เพิ่มฟีเจอร์)

ทุกครั้งที่มีการแก้ไขหรือเพิ่มฟีเจอร์ ให้เพิ่มหมายเหตุปฏิบัติการที่นี่ด้วย เพื่อให้คนที่ใช้งานระบบหรือ deploy รู้ว่าต้องทำอะไรเพิ่ม

### ปุ่ม "ดูรหัสผ่าน" — Admin/Users (เพิ่ม 2026-05-26)

**ฟีเจอร์:** ปุ่มสีม่วง "ดูรหัสผ่าน" จะแสดงในแถว user ที่ยัง `MustChangePassword = true` และรหัสผ่านยังไม่หมดอายุ ช่วยให้ Admin ดูรหัสผ่านชั่วคราวได้อีกครั้งหลังพลาดจาก modal ครั้งแรก

**สิ่งที่ต้องทำหลัง deploy:**
1. รัน migration: `dotnet ef database update --project SRT.Complaint --context AppDbContext`
2. User เก่าที่สร้างก่อน migration นี้จะไม่มี `TempPasswordEncrypted` → ปุ่มจะไม่แสดง ต้องกด **"รีเซ็ต PW"** หนึ่งครั้งเพื่อให้ระบบ encrypt และเก็บรหัสผ่านใหม่

**Encryption Key (อัปเดต 2026-06-25 — ย้ายออกจาก appsettings.json):**
- `Encryption:Key` **ไม่อยู่ใน `appsettings.json` แล้ว** (เคยมี dev key คอมมิตอยู่ในนั้น ถูกย้ายออกเพราะเป็น secret ที่ไม่ควรอยู่ใน git — ค่าเดิมยังหลงเหลือใน git history เก่า ไม่ได้ scrub)
- **Local dev:** ใช้ `dotnet user-secrets set "Encryption:Key" "<base64-32-bytes>"` (ทำครั้งเดียวต่อเครื่อง) — ค่าจะถูกเก็บนอก repo ที่ `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json` และ ASP.NET Core โหลดให้อัตโนมัติเมื่อ `ASPNETCORE_ENVIRONMENT=Development`
- **Clone repo ใหม่ / เครื่องใหม่:** ต้องรัน `dotnet user-secrets set "Encryption:Key" "..."` เองก่อน ไม่งั้น decrypt `TempPasswordEncrypted` / corruption reporter data ที่เข้ารหัสด้วย key เดิมจะไม่ได้ (ขอ key จากทีมที่ทำงานอยู่ก่อนแล้ว)
- **Production:** สร้าง key ใหม่ด้วย `[Convert]::ToBase64String((1..32 | % { [byte](Get-Random -Max 256) }))` แล้วเก็บเป็น Environment Variable `Encryption__Key` ใน IIS — ห้าม commit key จริงลง Git
- Key นี้ใช้ encrypt ทั้ง `TempPasswordEncrypted` (StaffUsers) และข้อมูลผู้แจ้งทุจริต (CorruptionReports) — ถ้าเปลี่ยน key จะ decrypt ข้อมูลเก่าไม่ได้

### ยกเลิก Traffy Fondue Integration (อัปเดต 2026-10-27)

- ถอด Traffy webhook endpoints, adapter, settings service และหน้าตั้งค่าออกแล้ว
- หลัง deploy ไม่ต้องกำหนด Traffy credentials หรือ Webhook Secret อีก
- ควรแจ้งผู้ดูแล Traffy/NECTEC ให้ยกเลิก webhook URLs เดิม หากเคยลงทะเบียนไว้ เพื่อลด request ที่จะได้รับ `404 Not Found`
- ยังไม่มี database migration สำหรับลบตาราง `dbo.TraffySettings`; ตารางถูกเก็บไว้แบบ inactive เพื่อป้องกันการลบข้อมูลโดยไม่ตั้งใจ
- หากต้องการลบตารางดังกล่าวในอนาคต ต้องสร้างและตรวจ destructive migration แล้วรอการยืนยันก่อน apply ตามขั้นตอนของโปรเจกต์

### Update 2 — ระบบรับเรื่องร้องเรียน (เพิ่ม 2026-10-29)

**ฟีเจอร์:** สีเมนูหน้าหลักแยกตามประเภท, ไฟล์แนบเลือกสะสม/พรีวิวได้, วัตถุประสงค์การแจ้งเรื่อง, ใบรับเรื่องพร้อมคัดลอกรหัส, คิวเก่าไปใหม่, ค้นหาหลายฟิลด์, ส่งออก Excel/PDF/Word, แก้ประเภท/ฝ่ายรับผิดชอบ และปิดงานขั้นตอนเดียวโดยเลือกประโยคขึ้นต้นแล้วกรอกรายละเอียดสรุปผล

**สิ่งที่ต้องทำหลัง deploy:**
1. ตรวจ migration `20261029093150_AddUpdate2ComplaintFeatures` แล้วรัน `dotnet ef database update --project SRT.Complaint --context AppDbContext`
2. ตรวจว่า IIS ใช้ `web.config` ที่กำหนด `requestLimits maxAllowedContentLength="57671680"` เพื่อรองรับคำขอ 55 MB
3. ตรวจสิทธิ์เขียนของโฟลเดอร์ `FileUpload__StoragePath` และพื้นที่ว่าง เพราะหนึ่งคำร้องแนบไฟล์รวมได้สูงสุด 50 MB
4. ทดสอบดาวน์โหลดรายงานภาษาไทยทั้ง Excel, PDF และ Word หลัง publish จริง
5. ทดสอบงานสถานะ `InProgress`: เลือกประโยคขึ้นต้น กรอกรายละเอียด ตรวจตัวอย่างข้อความ แล้วกด “บันทึกสรุปผลและปิดงาน”; ระบบต้องบันทึกข้อความตอบกลับและเปลี่ยนเป็น `Closed` โดยตรง

**หมายเหตุข้อมูลเดิม:** รายการที่เคยอยู่สถานะ `Resolved` จะถูกนับเป็นงานที่ยังไม่ปิดและยังแสดงฟอร์มสรุปผล เพื่อให้เจ้าหน้าที่ปิดเป็น `Closed` ได้โดยไม่ต้องแก้ข้อมูลด้วย SQL

## Session Continuity

- **อ่าน PROGRESS.md ทุกครั้งที่เริ่ม session ใหม่ แล้วทำต่อจากที่ค้างไว้โดยไม่ต้องถาม
- **ทุกครั้งที่ทำ task เสร็จ 1 อัน ให้อัปเดต PROGRESS.md ทันที โดย:
- ** ติ๊ก task ที่เสร็จแล้ว
- ** เพิ่ม task ถัดไปที่ต้องทำ
- ** บันทึก issue หรือ note ที่เจอระหว่างทำ
