# ระบบรับเรื่องร้องเรียนออนไลน์ — การรถไฟแห่งประเทศไทย

ระบบรับและจัดการเรื่องร้องเรียนออนไลน์ของการรถไฟแห่งประเทศไทย (รฟท.) รองรับทั้งเรื่องร้องเรียนทั่วไปและการแจ้งเบาะแสทุจริต/ประพฤติมิชอบ พร้อมระบบ API สำหรับเชื่อมต่อกับระบบภายนอก

---

## สารบัญ

- [ภาพรวมระบบ](#ภาพรวมระบบ)
- [Tech Stack](#tech-stack)
- [โครงสร้างโปรเจกต์](#โครงสร้างโปรเจกต์)
- [ฟีเจอร์หลัก](#ฟีเจอร์หลัก)
- [หน้าทั้งหมดในระบบ](#หน้าทั้งหมดในระบบ)
- [REST API](#rest-api)
- [ความปลอดภัย](#ความปลอดภัย)
- [การตั้งค่า (Configuration)](#การตั้งค่า-configuration)
- [การพัฒนาบนเครื่องท้องถิ่น](#การพัฒนาบนเครื่องท้องถิ่น)
- [การ Deploy บน IIS (Production)](#การ-deploy-บน-iis-production)
- [การ Migrate ฐานข้อมูล](#การ-migrate-ฐานข้อมูล)
- [บัญชีผู้ใช้เริ่มต้น](#บัญชีผู้ใช้เริ่มต้น)
- [Roles & Permissions](#roles--permissions)

---

## ภาพรวมระบบ

ระบบแบ่งการทำงานออกเป็น 2 Track ที่แยกกันโดยสมบูรณ์:

| Track | คำอธิบาย | DB Schema |
|---|---|---|
| **เรื่องร้องเรียนทั่วไป** | ประชาชนยื่นเรื่อง ติดตาม เจ้าหน้าที่จัดการ | `dbo.*` |
| **แจ้งเบาะแสทุจริต** | ช่องทางแยก ข้อมูลผู้แจ้งเข้ารหัส AES-256 | `corruption.*` |

URL จริง: `https://www.railway.co.th/complaint/`
Deploy: IIS Application บน GDCC

---

## Tech Stack

| ส่วน | เทคโนโลยี | เวอร์ชัน |
|---|---|---|
| Framework | ASP.NET Core (Razor Pages) | .NET 9 |
| Frontend | Tailwind CSS + HTMX + SweetAlert2 | Tailwind 4 / SweetAlert2 11.26.25 |
| Database | SQL Server + EF Core + Dapper | EF Core 9 |
| Auth (Web) | ASP.NET Core Cookie Auth | — |
| Auth (API) | JWT Bearer | — |
| PDF Export | QuestPDF | 2026.5.0 |
| Email | MailKit | 4.16.0 |
| Excel Export | ClosedXML | 0.105.0 |
| Password Hash | BCrypt.Net-Next (cost=12) | 4.2.0 |
| HTML Sanitize | HtmlSanitizer (Ganss.Xss) | 9.0.892 |
| Logging | Serilog (Console + File) | 10.0.0 |
| Bot Protection | Cloudflare Turnstile | — |

---

## โครงสร้างโปรเจกต์

```
srt_complaint_newversion/
├── SRT.Complaint/
│   ├── Controllers/
│   │   └── Api/                    ← REST API endpoints
│   │       ├── ComplaintsController.cs
│   │       ├── StatsController.cs
│   │       ├── CorruptionStatsController.cs
│   │       └── WebhooksController.cs
│   ├── Data/
│   │   ├── AppDbContext.cs          ← EF Core context หลัก (dbo.*)
│   │   ├── CorruptionDbContext.cs   ← EF Core context ทุจริต (corruption.*)
│   │   └── Migrations/             ← EF Core migrations
│   ├── Filters/
│   │   ├── ApiKeyAuthFilter.cs     ← API Key authentication
│   │   └── RequireScopeAttribute.cs
│   ├── Fonts/
│   │   ├── Sarabun-Regular.ttf     ← ฟอนต์ภาษาไทยสำหรับ PDF
│   │   └── Sarabun-Bold.ttf
│   ├── Models/                     ← EF Core entity models
│   ├── Pages/
│   │   ├── Public/                 ← หน้าสาธารณะ (ไม่ต้อง Login)
│   │   ├── Staff/                  ← เจ้าหน้าที่ทั่วไป
│   │   ├── Corruption/             ← เจ้าหน้าที่ทุจริต
│   │   ├── Admin/                  ← Super Admin
│   │   └── Shared/                 ← Layout, Partials
│   ├── Services/                   ← Business logic ทั้งหมด
│   ├── Validation/                 ← Custom validation attributes
│   ├── appsettings.json
│   ├── Program.cs
│   ├── tailwind.config.js
│   └── wwwroot/
│       ├── css/
│       ├── js/
│       └── images/
└── README.md
```

---

## ฟีเจอร์หลัก

### ประชาชน (Public)
- ยื่นเรื่องร้องเรียนทั่วไป พร้อมแนบไฟล์สูงสุด 10 ไฟล์ (JPG, PNG, PDF, DOC, DOCX, ≤10 MB/ไฟล์ และรวมไม่เกิน 50 MB)
- แจ้งเบาะแสทุจริต — ข้อมูลผู้แจ้งเข้ารหัส AES-256 ทันทีก่อนบันทึก
- ติดตามสถานะเรื่องด้วยเลขอ้างอิง + เบอร์โทร 4 หลักท้าย
- ตอบคำขอข้อมูลเพิ่มเติมและแนบเอกสารผ่านหน้าติดตามได้ โดยไม่ต้องสร้างเรื่องใหม่
- รับอีเมล/SMS แจ้งผลทุกขั้นตอน

### เจ้าหน้าที่ (Staff)
- Dashboard แสดง Queue เรื่องที่รอดำเนินการ
- ดำเนินเรื่องตาม Guided Workflow 6 ขั้น โดยสถานะเปลี่ยนตามขั้นตอนที่อนุญาต
- ใช้ Quick View ในคิวเพื่อดูงานของฉัน งานรอข้อมูล ข้อมูลเข้าใหม่ งานเกิน SLA และงานพร้อมปิด
- รับเรื่อง โอนเรื่อง เพิ่มบันทึกภายใน และตอบกลับผู้ร้อง
- ค้นหาเรื่องจากแถบด้านบน และแนบไฟล์หลักฐานได้เมื่อเจ้าหน้าที่สร้างเรื่องแทนประชาชน
- มอบหมายเรื่องอัตโนมัติให้เจ้าหน้าที่ทั่วไปที่มีงานค้างน้อยที่สุดตามค่า `AutoAssignAfterHours`
- Export PDF รายงานเรื่องร้องเรียน
- **ดึงคำร้องจากระบบภายนอก** — ปุ่ม "ดึงคำร้อง" ใน Queue เปิด modal เลือกหน่วยงานที่มี adapter และตั้งค่าพร้อมใช้งาน

### เจ้าหน้าที่ทุจริต (Corruption Officer)
- Dashboard และ Queue แยกจากเรื่องทั่วไปสมบูรณ์
- เปิดดูและดาวน์โหลดไฟล์หลักฐานของเรื่องทุจริตได้ โดยบันทึกการดาวน์โหลดใน Audit Log
- บังคับลำดับสถานะ `Pending → InProgress → UnderReview → Closed/Rejected` และเก็บสรุปผลก่อนปิดเรื่อง
- เข้าถึงข้อมูลผู้แจ้งได้เฉพาะเมื่อมีเหตุผลอันสมควร (มีการบันทึก)

### Super Admin
- จัดการผู้ใช้งานระบบ (สร้าง รีเซ็ตรหัสผ่าน เปิด/ปิด)
- จัดการประเภทเรื่องร้องเรียนและหัวข้อย่อย
- กำหนด SLA (Service Level Agreement) แต่ละประเภทเรื่อง
- จัดการ API Keys สำหรับระบบภายนอก
- กำหนด Webhook endpoints
- แก้ไขเนื้อหาหน้าเว็บ (Terms, Content Blocks)
- ดู Audit Log และ API Request Log

### API (External)
- REST API พร้อม API Key authentication
- รองรับ Webhook แจ้งเตือน external systems เมื่อสถานะเปลี่ยน
- Rate limiting แบบ Sliding Window

### External System Integration
- **Pluggable Adapter Pattern** — รองรับเพิ่มระบบภายนอกได้ง่ายผ่าน `IExternalSystemAdapter`
- Deduplication อัตโนมัติ — ข้ามรายการซ้ำโดยตรวจ `(ExternalSystem, ExternalId)` unique index
- การเชื่อมต่อเฉพาะระบบจะเปิดใช้งานเมื่อมี adapter และ credentials ของระบบนั้นเท่านั้น
- **eDOC outbound** — เจ้าหน้าที่เลือก Basket และสร้างเอกสารจากหน้า Case Detail โดยระบบใช้ service account เบื้องหลัง ไม่ต้อง login eDOC ซ้ำ
- คู่มือตั้งค่า: [`docs/edoc-outbound-integration.md`](docs/edoc-outbound-integration.md) · รายงานความปลอดภัย: [`docs/edoc-api-security-review.md`](docs/edoc-api-security-review.md)

---

## หน้าทั้งหมดในระบบ

### Public (ไม่ต้อง Login)
| URL | คำอธิบาย |
|---|---|
| `/` | หน้าแรก |
| `/Public/Submit` | ยื่นเรื่องร้องเรียนทั่วไป |
| `/Public/Track` | ติดตามเรื่องร้องเรียน |
| `/Public/SubmitCorruption` | แจ้งเบาะแสทุจริต |
| `/Public/CorruptionSubmitted` | ยืนยันการแจ้งเบาะแสสำเร็จ |

### Staff (ต้อง Login — Role: GeneralOfficer, SuperAdmin)
| URL | คำอธิบาย |
|---|---|
| `/Staff/Login` | เข้าสู่ระบบ |
| `/Staff/Dashboard` | หน้าแรกเจ้าหน้าที่ |
| `/Staff/Queue` | คิวเรื่องร้องเรียน |
| `/Staff/CaseDetail?id={id}` | รายละเอียดเรื่อง |
| `/Staff/CreateComplaint` | สร้างเรื่องแทนประชาชน |
| `/Staff/ChangePassword` | เปลี่ยนรหัสผ่าน |

### Corruption (ต้อง Login — Role: CorruptionOfficer, SuperAdmin)
| URL | คำอธิบาย |
|---|---|
| `/Corruption/Dashboard` | Dashboard ทุจริต |
| `/Corruption/Queue` | คิวเรื่องทุจริต |
| `/Corruption/CaseDetail?id={id}` | รายละเอียดเรื่องทุจริต |

### Admin (ต้อง Login — Role: SuperAdmin)
| URL | คำอธิบาย |
|---|---|
| `/Admin/Dashboard` | Dashboard ภาพรวมทั้งระบบ |
| `/Admin/Users` | จัดการผู้ใช้งาน |
| `/Admin/Categories` | จัดการประเภทเรื่อง |
| `/Admin/SlaSettings` | กำหนด SLA |
| `/Admin/Terms` | แก้ไขข้อตกลงและเงื่อนไข |
| `/Admin/HomeContent` | แก้ไขเนื้อหาหน้าแรก |
| `/Admin/Notifications` | ตั้งค่าการแจ้งเตือน |
| `/Admin/ApiKeys` | จัดการ API Keys |
| `/Admin/ApiKeyUsage` | ดู API Usage Log |
| `/Admin/Webhooks` | จัดการ Webhook |
| `/Admin/Reports` | รายงานสถิติ |
| `/Admin/AuditLog` | บันทึกการใช้งาน |
| `/Admin/ExternalSync` | ประวัติการดึงข้อมูลจากระบบภายนอก |

---

## REST API

> **API ปัจจุบันคือ Partner API v1 เท่านั้น:** ใช้ route prefix `/api/v1` และดู contract ล่าสุดที่ [`API.md`](API.md) หรือ OpenAPI runtime ที่ `/openapi/v1.json` ข้อความ route `/api/...` ที่ไม่มี `/v1` ในเอกสารหรือ integration รุ่นเก่าไม่ใช่ public contract ปัจจุบัน

ภาพรวม API v1:

- 16 endpoints ครอบคลุม complaints, metadata, statistics และ signed webhooks
- Authentication ด้วย `X-API-Key` ผูก Partner, environment, scopes, expiry, IP allowlist และ rate limit
- External key เห็นเฉพาะข้อมูล Partner ตนเอง (`DataAccessMode=Own`); การขอข้อมูลข้าม Partner ตอบ `404`
- `POST /api/v1/complaints` บังคับ `Idempotency-Key` และตรวจ category/subcategory ที่ active
- Complaint response มีทั้งสถานะภาพรวม `status` และขั้นตอน back-office `workflowStep`
- Status update สงวนให้ Internal key, บังคับ transition matrix และใช้ `ETag`/`If-Match` ป้องกัน lost update
- Rate-limit response ส่ง `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset` และ `Retry-After` เมื่อได้ `429`
- โหมดหลาย instance ใช้ Redis quota/session ร่วม; Data Protection key ring เก็บในฐานข้อมูลและเข้ารหัสด้วย certificate ใน Production
- Webhook ใช้ HMAC-SHA256, ป้องกัน SSRF, ตรวจ ownership ตอนลบ และจำกัด 10 active webhooks ต่อ API key
- Error ใช้ envelope `{ "error": { "code", "message", "traceId" } }`

ตัวอย่าง:

```http
GET /api/v1/complaints?page=1&pageSize=20
Accept: application/json
X-API-Key: <secret>
```

เอกสารที่เกี่ยวข้อง:

- [API contract และ endpoint inventory](API.md)
- [คู่มือ Partner API v1](docs/partner-api-v1-integration-guide.md)
- [Operations runbook](docs/api-v1-operations-runbook.md)
- [Security review](docs/api-security-review-phase1.md)
- [API review update — 2026-11-03](docs/api-security-review-2026-11-03.md)
- [Postman collection](docs/srt-complaint-api-v1.postman_collection.json)

### หมายเหตุเกี่ยวกับเนื้อหารุ่นเดิมด้านล่าง

ส่วนด้านล่างเก็บไว้เพื่ออ้างอิงระบบเดิมเท่านั้น หาก route หรือ response ขัดกับ `API.md` ให้ยึด `API.md` และ `/openapi/v1.json` เป็นหลัก

### Authentication
ทุก endpoint ต้องส่ง Header:
```
X-API-Key: {your-api-key}
```

### Endpoints

#### Complaints
```
POST   /api/complaints                         สร้างเรื่องร้องเรียนใหม่
GET    /api/complaints/{referenceNumber}        ดูรายละเอียดเรื่อง
GET    /api/complaints/{referenceNumber}/status ดูสถานะเรื่อง
PUT    /api/complaints/{referenceNumber}/status อัปเดตสถานะ
GET    /api/complaints/{referenceNumber}/edoc-payload ดึงข้อมูลสำหรับ e-Document
```

#### Statistics
```
GET    /api/stats/summary    สรุปสถิติเรื่องร้องเรียนทั่วไป
GET    /api/stats/detailed   สถิติละเอียด
GET    /api/stats/corruption สถิติเรื่องทุจริต
```

#### Webhooks
```
GET    /api/webhooks         รายการ Webhook ที่ลงทะเบียน
POST   /api/webhooks         ลงทะเบียน Webhook ใหม่
DELETE /api/webhooks/{id}    ลบ Webhook
```

### Scopes
| Scope | สิทธิ์ |
|---|---|
| `complaints:read` | GET ดูรายละเอียดเรื่องร้องเรียน |
| `complaints:write` | POST สร้างเรื่องร้องเรียนใหม่ |
| `complaints:status` | GET ดูสถานะเรื่องร้องเรียน |
| `complaints:update` | PUT อัปเดตสถานะเรื่องร้องเรียน (Internal key เท่านั้น) |
| `complaints:edoc` | GET ดึงข้อมูล e-Document payload |
| `stats:summary` | GET สถิติสรุปเรื่องร้องเรียน |
| `stats:detailed` | GET สถิติละเอียดแยกตามประเภท/ลำดับความสำคัญ |
| `corruption:stats` | GET สถิติเรื่องทุจริต |
| `webhooks:manage` | GET/POST/DELETE จัดการ Webhook |

---

## ความปลอดภัย

| มาตรการ | รายละเอียด |
|---|---|
| Password Hashing | BCrypt cost factor 12 |
| Encryption | AES-256-CBC สำหรับข้อมูลผู้แจ้งทุจริต |
| Session Cookie | HttpOnly + Secure + SameSite=Strict |
| CSRF | Anti-forgery token ทุก Form |
| XSS Prevention | HtmlSanitizer 9.0.892 ก่อนบันทึก HTML ลง DB |
| Bot Protection | Cloudflare Turnstile บนทุก Form สำคัญ |
| Rate Limiting | Submit: 5 ครั้ง/ชม., Login: 10 ครั้ง/15 นาที, Track: 10 ครั้ง/15 นาที |
| IDOR Protection | ตรวจสิทธิ์การแก้ไขเรื่องทุก handler |
| File Upload | ตรวจ extension whitelist + ขนาดไม่เกิน 10 MB |
| API Key | Hash ก่อนเก็บ + ตรวจ IP Whitelist + Scope |
| HSTS | เปิดใช้งานใน Production |

---

## การตั้งค่า (Configuration)

ไฟล์ `appsettings.json` (สำหรับ Development):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=SRT_Complaint;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Encryption": {
    "Key": "<Base64 ของ random 32 bytes — ดูวิธีสร้างด้านล่าง>"
  },
  "Jwt": {
    "Secret": "<random string ยาว 64+ ตัวอักษร>",
    "Issuer": "railway.co.th",
    "ExpiryDays": 365
  },
  "Notifications": {
    "SmsGatewayUrl": "https://sms-gateway.example.com/send",
    "SmsApiKey": "<SMS API Key>",
    "SmtpHost": "smtp.example.com",
    "SmtpPort": 587,
    "SmtpUser": "noreply@railway.co.th",
    "SmtpPassword": "<SMTP Password>"
  },
  "FileUpload": {
    "StoragePath": "C:\\SRT_Uploads\\Complaints\\"
  },
  "Turnstile": {
    "SiteKey": "<Cloudflare Site Key — เว้นว่างเพื่อข้ามใน dev>",
    "SecretKey": "<Cloudflare Secret Key — เว้นว่างเพื่อข้ามใน dev>"
  }
}
```

### สร้าง Encryption Key

```powershell
# PowerShell — สร้าง AES-256 key (32 bytes → Base64)
[Convert]::ToBase64String((1..32 | % { [byte](Get-Random -Max 256) }))
```

### ขอ Cloudflare Turnstile Keys

1. ไปที่ [Cloudflare Dashboard](https://dash.cloudflare.com/) → **Turnstile**
2. คลิก **Add Site** → กรอก Domain: `www.railway.co.th`
3. เลือก Widget Type: **Managed** (แนะนำ)
4. คัดลอก **Site Key** และ **Secret Key**

---

## การพัฒนาบนเครื่องท้องถิ่น

### สิ่งที่ต้องติดตั้งก่อน

| ซอฟต์แวร์ | เวอร์ชันขั้นต่ำ | ดาวน์โหลด |
|---|---|---|
| .NET SDK | 9.0 | https://dotnet.microsoft.com/download |
| Node.js | 18 LTS | https://nodejs.org/ |
| SQL Server | 2019 / Express | https://www.microsoft.com/sql-server |
| SQL Server Management Studio | 20+ (แนะนำ) | https://aka.ms/ssms |

### ขั้นตอน

```bash
# 1. Clone repository
git clone https://github.com/Thipokcpe27/srt_complaint_newversion.git
cd srt_complaint_newversion

# 2. กำหนด Connection String ใน appsettings.json
#    แก้ Server= ให้ตรงกับเครื่องตัวเอง

# 3. Restore dependencies (.NET + npm)
dotnet restore
cd SRT.Complaint && npm install && cd ..

# 4. Migrate ฐานข้อมูล
dotnet ef database update --project SRT.Complaint --context AppDbContext

# 5. Run
dotnet run --project SRT.Complaint
```

แอปจะรันที่ `https://localhost:5001` (หรือ port ที่ระบุใน `launchSettings.json`)

> **หมายเหตุ:** บัญชี SuperAdmin เริ่มต้นจะถูกสร้างอัตโนมัติเมื่อรันครั้งแรก  
> EmployeeCode: `0000001` / Password: `Admin@1234` — **ต้องเปลี่ยนรหัสผ่านทันที**

---

## การ Deploy บน IIS (Production)

### ข้อกำหนดเซิร์ฟเวอร์

| รายการ | ข้อกำหนด |
|---|---|
| OS | Windows Server 2019 / 2022 |
| Web Server | IIS 10+ |
| Runtime | .NET 9 Hosting Bundle |
| Database | SQL Server 2019+ |
| RAM | 4 GB ขั้นต่ำ (แนะนำ 8 GB) |
| Disk | 20 GB ขั้นต่ำ |

---

### ขั้นที่ 1 — ติดตั้ง .NET 9 Hosting Bundle บนเซิร์ฟเวอร์

1. ดาวน์โหลด **.NET 9 Hosting Bundle** จาก https://dotnet.microsoft.com/download/dotnet/9.0
   - เลือกไฟล์ชื่อ `dotnet-hosting-9.x.x-win.exe`
2. รันติดตั้งในฐานะ Administrator
3. Restart IIS หลังติดตั้ง:
   ```cmd
   net stop was /y
   net start w3svc
   ```
4. ตรวจสอบ:
   ```cmd
   dotnet --info
   ```

---

### ขั้นที่ 2 — เปิดใช้งาน IIS Features

เปิด **Server Manager** → **Add Roles and Features** → เลือก:

- **Web Server (IIS)**
  - Common HTTP Features: Static Content, Default Document, HTTP Errors
  - Performance: Static Content Compression, Dynamic Content Compression
  - Security: Request Filtering, Windows Authentication (ถ้าต้องการ)
  - Application Development: ASP.NET 4.8, ISAPI Extensions, ISAPI Filters
- **Management Tools**: IIS Management Console

หรือใช้ PowerShell (Run as Administrator):
```powershell
Install-WindowsFeature -Name Web-Server, Web-Common-Http, Web-Static-Content,
  Web-Default-Doc, Web-Http-Errors, Web-Performance, Web-Stat-Compression,
  Web-Dyn-Compression, Web-Security, Web-Filtering, Web-App-Dev,
  Web-Asp-Net45, Web-ISAPI-Ext, Web-ISAPI-Filter, Web-Mgmt-Tools,
  Web-Mgmt-Console -IncludeManagementTools
```

---

### ขั้นที่ 3 — Publish แอปพลิเคชัน

บนเครื่อง Developer (หรือ CI/CD):

```bash
dotnet publish SRT.Complaint/SRT.Complaint.csproj \
  -c Release \
  -r win-x64 \
  --self-contained false \
  -o ./publish
```

ผลลัพธ์จะอยู่ที่โฟลเดอร์ `publish/`

---

### ขั้นที่ 4 — สร้าง Application Pool

1. เปิด **IIS Manager**
2. คลิกขวาที่ **Application Pools** → **Add Application Pool**
3. กำหนดค่า:
   - **Name:** `SRTComplaint`
   - **.NET CLR version:** `No Managed Code`
   - **Managed pipeline mode:** `Integrated`
4. คลิกขวา Application Pool ที่สร้าง → **Advanced Settings**:
   - **Identity:** `ApplicationPoolIdentity` (หรือ service account ที่กำหนด)
   - **Start Mode:** `AlwaysRunning`
   - **Idle Time-out:** `0` (ไม่หยุด)
   - **Regular Time Interval (minutes):** `1440` (Recycle วันละครั้ง)

---

### ขั้นที่ 5 — สร้าง Website / Application

**ตัวเลือก A — Deploy เป็น Sub-Application (แนะนำสำหรับ `/complaint/`)**

1. ใน IIS Manager → 展開 **Sites** → คลิกขวา `Default Web Site` (หรือ site หลัก) → **Add Application**
2. กำหนดค่า:
   - **Alias:** `complaint`
   - **Application pool:** `SRTComplaint`
   - **Physical path:** `C:\inetpub\wwwroot\complaint`
3. คัดลอกไฟล์จาก `publish/` ไปที่ `C:\inetpub\wwwroot\complaint\`

**ตัวเลือก B — Deploy เป็น Website แยก**

1. คลิกขวา **Sites** → **Add Website**
2. กำหนดค่า:
   - **Site name:** `SRTComplaint`
   - **Application pool:** `SRTComplaint`
   - **Physical path:** `C:\inetpub\wwwroot\complaint`
   - **Port:** `443` (HTTPS)
   - **Host name:** `www.railway.co.th`

---

### ขั้นที่ 6 — กำหนดสิทธิ์โฟลเดอร์

```powershell
# 1. สิทธิ์อ่านโฟลเดอร์แอป
icacls "C:\inetpub\wwwroot\complaint" /grant "IIS AppPool\SRTComplaint:(OI)(CI)RX" /T

# 2. สร้างและกำหนดสิทธิ์โฟลเดอร์ Upload
New-Item -ItemType Directory -Force -Path "C:\SRT_Uploads\Complaints"
icacls "C:\SRT_Uploads\Complaints" /grant "IIS AppPool\SRTComplaint:(OI)(CI)M" /T

# 3. สร้างและกำหนดสิทธิ์โฟลเดอร์ Logs
New-Item -ItemType Directory -Force -Path "C:\inetpub\wwwroot\complaint\logs"
icacls "C:\inetpub\wwwroot\complaint\logs" /grant "IIS AppPool\SRTComplaint:(OI)(CI)M" /T
```

---

### ขั้นที่ 7 — กำหนด Environment Variables (Secrets)

**ห้ามใส่ค่า Secret ใน appsettings.json ที่ deploy** — ให้ใช้ Environment Variables แทน

**วิธีที่ 1: ผ่าน IIS Manager (แนะนำ)**

1. IIS Manager → เลือก Application `complaint`
2. ดับเบิลคลิก **Configuration Editor**
3. ไปที่ `system.webServer/aspNetCore`
4. คลิก `environmentVariables` → `[...]`
5. เพิ่มตัวแปรต่อไปนี้ทีละรายการ:

| Name | Value | หมายเหตุ |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | บังคับ |
| `ConnectionStrings__DefaultConnection` | `Server=...;Database=SRT_Complaint;...` | Connection string จริง |
| `Encryption__Key` | `<Base64 32 bytes>` | สร้างด้วย PowerShell |
| `Jwt__Secret` | `<random 64+ chars>` | |
| `Notifications__SmtpHost` | `smtp.railway.co.th` | |
| `Notifications__SmtpUser` | `noreply@railway.co.th` | |
| `Notifications__SmtpPassword` | `<SMTP password>` | |
| `Notifications__SmsGatewayUrl` | `https://sms.gdcc.go.th/send` | |
| `Notifications__SmsApiKey` | `<SMS API Key>` | |
| `Turnstile__SiteKey` | `<Cloudflare Site Key>` | |
| `Turnstile__SecretKey` | `<Cloudflare Secret Key>` | |
| `FileUpload__StoragePath` | `C:\SRT_Uploads\Complaints\` | |
| `DistributedRuntime__Mode` | `MultiInstance` | ใช้เมื่อ deploy มากกว่าหนึ่ง instance |
| `DistributedRuntime__RateLimitProvider` | `Redis` หรือ `Gateway` | ต้องเป็น distributed เมื่อใช้ MultiInstance |
| `DistributedRuntime__RedisConnection` | `<Redis connection secret>` | ใช้กับ distributed session และ Redis limiter |
| `DataProtection__ApplicationName` | `SRT.Complaint.Production` | ต้องตรงกันทุก instance |
| `DataProtection__CertificateThumbprint` | `<certificate thumbprint>` | บังคับใน Production เพื่อป้องกัน key at rest |
| `DataProtection__CertificateStoreLocation` | `LocalMachine` | หรือ `CurrentUser` ตามบัญชีที่รันแอป |
> ใช้ `__` (สองขีดล่าง) แทน `:` ใน key name เสมอ

**วิธีที่ 2: แก้ไข applicationHost.config โดยตรง**

ไฟล์อยู่ที่ `C:\Windows\System32\inetsrv\config\applicationHost.config`
```xml
<system.webServer>
  <aspNetCore processPath="dotnet" arguments=".\SRT.Complaint.dll" stdoutLogEnabled="true">
    <environmentVariables>
      <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
      <environmentVariable name="ConnectionStrings__DefaultConnection" value="Server=...;..." />
      <!-- เพิ่มตัวแปรอื่นๆ ที่นี่ -->
    </environmentVariables>
  </aspNetCore>
</system.webServer>
```

---

### ขั้นที่ 8 — Migrate ฐานข้อมูล

รันบนเซิร์ฟเวอร์ production หรือรันจากเครื่อง developer โดยชี้ Connection String ไปยัง production DB:

```bash
# ตั้ง Connection String ชั่วคราว
$env:ConnectionStrings__DefaultConnection = "Server=prod-db;Database=SRT_Complaint;..."

# Migrate
dotnet ef database update --project SRT.Complaint --context AppDbContext
```

หรือ Migrate ด้วย SQL Script:
```bash
dotnet ef migrations script --project SRT.Complaint --context AppDbContext -o migrate.sql
# นำ migrate.sql ไป run บน SQL Server Management Studio
```

---

### ขั้นที่ 9 — ตั้งค่า HTTPS (SSL Certificate)

**กรณีมี Certificate แล้ว (PFX file):**

1. IIS Manager → **Server Certificates** → **Import**
2. เลือกไฟล์ `.pfx` และกรอก password
3. ใน Site → **Bindings** → Add:
   - Type: `https`
   - Port: `443`
   - SSL certificate: เลือก cert ที่ import
   - Host name: `www.railway.co.th`

**เพิ่ม HTTP → HTTPS Redirect:**

1. เพิ่ม Binding HTTP port 80
2. IIS Manager → **URL Rewrite** → Add Rule:
   - Rule type: **Blank rule**
   - Match URL: `.*`
   - Conditions: Add → `{HTTPS}` / Is Not Equal To / `on`
   - Action: Redirect → `https://{HTTP_HOST}{REQUEST_URI}` (301)

---

### ขั้นที่ 10 — ตรวจสอบ web.config

ไฟล์ `web.config` จะถูกสร้างอัตโนมัติเมื่อ `dotnet publish` ตรวจสอบว่ามีเนื้อหาประมาณนี้:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\SRT.Complaint.dll"
                  stdoutLogEnabled="false"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
```

> **ข้อสำคัญ:** อย่าใส่ Secret ในไฟล์ `web.config` โดยตรง ให้ใช้ Environment Variables ผ่าน IIS Manager แทน

---

### ขั้นที่ 11 — ตั้งค่า Request Size Limit (สำหรับ File Upload)

เพิ่มใน `web.config` หรือผ่าน IIS Manager:

```xml
<system.webServer>
  <security>
    <requestFiltering>
      <!-- อนุญาต upload สูงสุด 52 MB (5 ไฟล์ × 10 MB + overhead) -->
      <requestLimits maxAllowedContentLength="54525952" />
    </requestFiltering>
  </security>
</system.webServer>
```

---

### ขั้นที่ 12 — ทดสอบหลัง Deploy

```
[ ] เปิดหน้า https://www.railway.co.th/complaint/ — โหลดได้ปกติ
[ ] ทดสอบยื่นเรื่องร้องเรียนทั่วไป — รับเลขอ้างอิง
[ ] ทดสอบติดตามเรื่อง — ดูสถานะได้
[ ] ทดสอบขอข้อมูลเพิ่มเติม — ผู้ร้องเห็นคำขอ ส่งข้อความ/ไฟล์ได้ และเจ้าหน้าที่พบในคิว “ข้อมูลเข้าใหม่”
[ ] ทดสอบยื่นเรื่องทุจริต — ข้อมูลถูก mask ในหน้ารายการ
[ ] Login เจ้าหน้าที่ด้วย EmployeeCode 0000001 — ระบบบังคับเปลี่ยนรหัสผ่าน
[ ] เปลี่ยนรหัสผ่าน Admin — Login ใหม่ได้
[ ] ทดสอบ HTTPS redirect — HTTP → HTTPS อัตโนมัติ
[ ] ตรวจสอบ Logs ที่ C:\inetpub\wwwroot\complaint\logs\
[ ] ตรวจสอบ Turnstile Widget แสดงบนหน้า Submit และ Login
```

---

### แก้ไขปัญหาที่พบบ่อย

**แอปไม่ start / 502 Bad Gateway**
```powershell
# ดู stdout log
cat "C:\inetpub\wwwroot\complaint\logs\stdout*.log" | Select-Object -Last 50

# ตรวจสอบ .NET version
dotnet --list-runtimes

# ตรวจสอบ AspNetCoreModule ติดตั้งหรือยัง
Get-WebConfiguration "system.webServer/globalModules" | Where-Object { $_.name -like "*AspNetCore*" }
```

**500.19 — web.config ผิดพลาด**
- ตรวจสอบ syntax ของ `web.config`
- ตรวจสอบ IIS URL Rewrite Module ติดตั้งหรือยัง

**ไม่สามารถเขียนไฟล์ / 500 ขณะ Upload**
```powershell
# ตรวจสอบสิทธิ์โฟลเดอร์ Upload
icacls "C:\SRT_Uploads\Complaints"
# ต้องมี IIS AppPool\SRTComplaint (M) — Modify
```

**ฐานข้อมูลต่อไม่ได้**
- ตรวจสอบ `ConnectionStrings__DefaultConnection` ใน Environment Variables
- ตรวจสอบ SQL Server Firewall อนุญาต port 1433
- ตรวจสอบว่า Application Pool Identity มีสิทธิ์เข้าถึง SQL Server

**Turnstile ไม่แสดง / ผ่านไม่ได้**
- ตรวจสอบ `Turnstile__SiteKey` ตั้งค่าถูกต้อง
- ตรวจสอบ domain ใน Cloudflare Dashboard ตรงกับ domain จริง
- ตรวจสอบ `Turnstile__SecretKey` ถูกต้อง (ใช้สำหรับ server-side verify)

---

## การ Redirect URL เดิม (`/complain/`) ไปยังระบบใหม่

ระบบเดิมใช้ URL: `https://www.railway.co.th/complain/call.asp` (Classic ASP)
ระบบใหม่ใช้ URL: `https://www.railway.co.th/complaint/` (ASP.NET Core)

มี 3 วิธีในการจัดการ เลือกตามสถานการณ์:

---

### วิธีที่ 1 — Deploy ระบบใหม่ที่ path `/complain/` เลย (แนะนำที่สุด)

ใช้เมื่อ: ระบบเดิมไม่มีคนใช้งานแล้ว ต้องการให้ URL เดิมยังใช้ได้โดยไม่ต้องแก้อะไรเพิ่ม

**โครงสร้างโฟลเดอร์ที่ต้องการ:**

```
C:\inetpub\wwwroot\
└── complain\                   ← โฟลเดอร์เดิมของระบบ Classic ASP
    ├── SRT.Complaint.dll       ← ไฟล์จาก dotnet publish (วางแทนของเดิม)
    ├── SRT.Complaint.runtimeconfig.json
    ├── web.config              ← สร้างอัตโนมัติโดย dotnet publish
    ├── appsettings.json
    ├── wwwroot\
    │   ├── css\
    │   ├── js\
    │   └── images\
    └── [ไฟล์อื่น ๆ จาก publish]
```

**ขั้นตอน:**

1. **Publish แอปบนเครื่อง Dev ก่อน**
   ```powershell
   dotnet publish SRT.Complaint/SRT.Complaint.csproj -c Release -o C:\publish
   ```
   ผลลัพธ์จะอยู่ที่ `C:\publish\` ประกอบด้วยไฟล์ทั้งหมดที่ต้องการ

2. **Copy ไฟล์ publish ไปยัง Server** (ผ่าน RDP File Copy, FTP, หรือ Network Share)
   ```
   จาก: C:\publish\          (เครื่อง Dev)
   ไปที่: C:\inetpub\wwwroot\complain_new\    (Server — วางไว้ก่อน ยังไม่ใช่ชื่อจริง)
   ```

3. **สำรองโฟลเดอร์ระบบเดิม**
   ```powershell
   # รันบน Server
   Copy-Item "C:\inetpub\wwwroot\complain" "C:\backup\complain_old_$(Get-Date -Format 'yyyyMMdd')" -Recurse
   ```

4. **ลบไฟล์ระบบเดิมออก**
   ```powershell
   # รันบน Server
   Remove-Item "C:\inetpub\wwwroot\complain\*" -Recurse -Force
   ```

5. **ย้ายไฟล์ใหม่เข้าที่**
   ```powershell
   # รันบน Server
   Copy-Item "C:\inetpub\wwwroot\complain_new\*" "C:\inetpub\wwwroot\complain\" -Recurse -Force
   Remove-Item "C:\inetpub\wwwroot\complain_new" -Recurse -Force
   ```

   โครงสร้างสุดท้ายที่ `C:\inetpub\wwwroot\complain\`:
   ```
   complain\
   ├── SRT.Complaint.dll
   ├── web.config
   ├── appsettings.json
   ├── wwwroot\
   └── [ไฟล์อื่น ๆ]
   ```

6. **เปลี่ยน Application Pool ใน IIS Manager**
   - IIS Manager → **Sites** → `Default Web Site` → `complain`
   - คลิกขวา → **Manage Application** → **Advanced Settings**
   - เปลี่ยน **Application Pool** จาก Pool เดิม (Classic ASP) → `SRTComplaint` (No Managed Code)

7. **กำหนดสิทธิ์โฟลเดอร์**
   ```powershell
   icacls "C:\inetpub\wwwroot\complain" /grant "IIS AppPool\SRTComplaint:(OI)(CI)RX" /T
   New-Item -ItemType Directory -Force -Path "C:\SRT_Uploads\Complaints"
   icacls "C:\SRT_Uploads\Complaints" /grant "IIS AppPool\SRTComplaint:(OI)(CI)M" /T
   ```

8. **กำหนด Environment Variables** ใน IIS Manager
   - IIS Manager → `complain` → **Configuration Editor**
   - Section: `system.webServer/aspNetCore` → `environmentVariables`
   - เพิ่มตัวแปรทั้งหมดตามตารางในขั้นที่ 7 ของหัวข้อ Deploy

9. **Restart Application Pool**
   ```powershell
   Restart-WebAppPool -Name "SRTComplaint"
   ```

10. **ทดสอบ**
    - เปิด `https://www.railway.co.th/complain/` → ควรเข้าระบบใหม่ได้เลย
    - เปิด `https://www.railway.co.th/complain/Staff/Login` → ควรเห็นหน้า Login

---

### วิธีที่ 2 — IIS URL Rewrite Rule (ไม่แตะโค้ดเดิมเลย)

ใช้เมื่อ: ระบบเดิมยังต้องเปิดให้บริการอยู่ หรือต้องการ deploy ระบบใหม่ที่ `/complaint/` แล้วให้ URL เดิม redirect มา

**โครงสร้างโฟลเดอร์:**

```
C:\inetpub\wwwroot\
├── complain\                   ← ระบบเดิม (Classic ASP) ยังคงอยู่ครบ
│   ├── call.asp
│   └── [ไฟล์เดิมทั้งหมด]
├── complaint\                  ← ระบบใหม่ (ASP.NET Core) วางตรงนี้
│   ├── SRT.Complaint.dll
│   ├── web.config
│   ├── appsettings.json
│   └── wwwroot\
└── web.config                  ← web.config ของ Site หลัก (ใส่ Rewrite Rule ที่นี่)
```

> **หมายเหตุ:** `web.config` ที่ใส่ Rewrite Rule ต้องอยู่ระดับ **root ของ Site** ไม่ใช่ใน `/complaint/` หรือ `/complain/`

**ขั้นตอน:**

**ส่วนที่ 1: วาง publish folder ระบบใหม่**

1. **Publish แอปบนเครื่อง Dev**
   ```powershell
   dotnet publish SRT.Complaint/SRT.Complaint.csproj -c Release -o C:\publish
   ```

2. **สร้างโฟลเดอร์ปลายทางบน Server**
   ```powershell
   New-Item -ItemType Directory -Force -Path "C:\inetpub\wwwroot\complaint"
   ```

3. **Copy ไฟล์ publish ไปที่ `complaint\`**
   ```powershell
   Copy-Item "C:\publish\*" "C:\inetpub\wwwroot\complaint\" -Recurse -Force
   ```

4. **ใน IIS Manager** → `Default Web Site` → คลิกขวา → **Add Application**
   - Alias: `complaint`
   - Application Pool: `SRTComplaint` (No Managed Code)
   - Physical path: `C:\inetpub\wwwroot\complaint`

5. **กำหนดสิทธิ์โฟลเดอร์**
   ```powershell
   icacls "C:\inetpub\wwwroot\complaint" /grant "IIS AppPool\SRTComplaint:(OI)(CI)RX" /T
   New-Item -ItemType Directory -Force -Path "C:\SRT_Uploads\Complaints"
   icacls "C:\SRT_Uploads\Complaints" /grant "IIS AppPool\SRTComplaint:(OI)(CI)M" /T
   ```

6. **กำหนด Environment Variables** ใน IIS Manager → Application `complaint` → Configuration Editor

**ส่วนที่ 2: ตั้งค่า URL Rewrite**

> ต้องติดตั้ง **IIS URL Rewrite Module** ก่อน  
> ดาวน์โหลด: https://www.iis.net/downloads/microsoft/url-rewrite  
> ตรวจสอบ: IIS Manager → คลิก Server (ไม่ใช่ Site) → ต้องมี icon **URL Rewrite** ในหน้า Features

**วิธีที่ 2A: ผ่าน IIS Manager**

1. IIS Manager → คลิก **Default Web Site** (Site หลัก)
2. ดับเบิลคลิก **URL Rewrite**
3. คลิก **Add Rule(s)...** → เลือก **Blank rule** → **OK**
4. กำหนดค่า:

   | ฟิลด์ | ค่า |
   |---|---|
   | Name | `Redirect complain to complaint` |
   | Requested URL | `Matches the Pattern` |
   | Using | `Regular Expressions` |
   | Pattern | `^complain/(.*)` |
   | Ignore case | ✅ เปิด |
   | Action type | `Redirect` |
   | Redirect URL | `/complaint/{R:1}` |
   | Append query string | ✅ เปิด |
   | Redirect type | `Permanent (301)` |

5. คลิก **Apply** → เพิ่ม Rule ที่สองสำหรับ root path:
   - Pattern: `^complain$`
   - Redirect URL: `/complaint/`
   - Redirect type: `Permanent (301)`

**วิธีที่ 2B: แก้ไข `web.config` ของ Site หลักโดยตรง**

เปิดไฟล์ `C:\inetpub\wwwroot\web.config` (สร้างใหม่ถ้าไม่มี) แล้วใส่:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="Redirect complain to complaint" stopProcessing="true">
          <match url="^complain/(.*)" />
          <action type="Redirect"
                  url="/complaint/{R:1}"
                  appendQueryString="true"
                  redirectType="Permanent" />
        </rule>
        <rule name="Redirect complain root" stopProcessing="true">
          <match url="^complain$" />
          <action type="Redirect"
                  url="/complaint/"
                  redirectType="Permanent" />
        </rule>
      </rules>
    </rewrite>
  </system.webServer>
</configuration>
```

**ทดสอบ:**
```powershell
# ทดสอบ redirect (ควรได้ 301 + Location header)
Invoke-WebRequest -Uri "https://www.railway.co.th/complain/" `
  -MaximumRedirection 0 -ErrorAction SilentlyContinue |
  Select-Object StatusCode, @{n="Location";e={$_.Headers.Location}}
```

---

### วิธีที่ 3 — ไฟล์ redirect.asp ในโฟลเดอร์เดิม

ใช้เมื่อ: ไม่มีสิทธิ์ติดตั้ง URL Rewrite Module และยังมี Classic ASP engine บน Server

**โครงสร้างโฟลเดอร์:**

```
C:\inetpub\wwwroot\
├── complain\                   ← โฟลเดอร์ระบบเดิม (แก้ไขบางไฟล์)
│   ├── default.asp             ← สร้างใหม่: redirect ทุก request ไป /complaint/
│   ├── call.asp                ← แก้ไข: เปลี่ยนเป็น redirect แทน
│   └── [ไฟล์เดิมอื่น ๆ]
└── complaint\                  ← ระบบใหม่ (ASP.NET Core) วางตรงนี้
    ├── SRT.Complaint.dll
    ├── web.config
    ├── appsettings.json
    └── wwwroot\
```

**ขั้นตอน:**

**ส่วนที่ 1: วาง publish folder ระบบใหม่ (เหมือนวิธีที่ 2)**

1. **Publish แอปบนเครื่อง Dev**
   ```powershell
   dotnet publish SRT.Complaint/SRT.Complaint.csproj -c Release -o C:\publish
   ```

2. **สร้างโฟลเดอร์และ copy ไฟล์บน Server**
   ```powershell
   New-Item -ItemType Directory -Force -Path "C:\inetpub\wwwroot\complaint"
   Copy-Item "C:\publish\*" "C:\inetpub\wwwroot\complaint\" -Recurse -Force
   ```

3. **ใน IIS Manager** → `Default Web Site` → **Add Application**
   - Alias: `complaint`
   - Application Pool: `SRTComplaint` (No Managed Code)
   - Physical path: `C:\inetpub\wwwroot\complaint`

4. **กำหนดสิทธิ์**
   ```powershell
   icacls "C:\inetpub\wwwroot\complaint" /grant "IIS AppPool\SRTComplaint:(OI)(CI)RX" /T
   New-Item -ItemType Directory -Force -Path "C:\SRT_Uploads\Complaints"
   icacls "C:\SRT_Uploads\Complaints" /grant "IIS AppPool\SRTComplaint:(OI)(CI)M" /T
   ```

**ส่วนที่ 2: เพิ่มไฟล์ redirect ในโฟลเดอร์ `/complain/`**

5. **สำรองไฟล์เดิม**
   ```powershell
   $date = Get-Date -Format 'yyyyMMdd'
   Copy-Item "C:\inetpub\wwwroot\complain" "C:\backup\complain_$date" -Recurse
   ```

6. **สร้างไฟล์ `default.asp`** ที่ `C:\inetpub\wwwroot\complain\default.asp`:
   ```asp
   <%
   Response.Status = "301 Moved Permanently"
   Response.AddHeader "Location", "https://www.railway.co.th/complaint/"
   Response.End
   %>
   ```

7. **แก้ไขไฟล์ `call.asp`** ที่ `C:\inetpub\wwwroot\complain\call.asp` ให้เหลือแค่:
   ```asp
   <%
   Response.Status = "301 Moved Permanently"
   Response.AddHeader "Location", "https://www.railway.co.th/complaint/"
   Response.End
   %>
   ```

8. **ตั้ง Default Document** ใน IIS:
   - IIS Manager → `complain` → ดับเบิลคลิก **Default Document**
   - คลิก **Add...** → พิมพ์ `default.asp`
   - ใช้ลูกศรขึ้นเพื่อย้าย `default.asp` ขึ้นบนสุด

9. **ทดสอบ:**
   - เปิด `https://www.railway.co.th/complain/` → ควร redirect ไป `/complaint/`
   - เปิด `https://www.railway.co.th/complain/call.asp` → ควร redirect ไป `/complaint/`

---

### สรุปการเลือกวิธี

| สถานการณ์ | วิธีที่แนะนำ | โฟลเดอร์ระบบใหม่ |
|---|---|---|
| ระบบเดิมหยุดใช้แล้ว URL เดิมยังต้องใช้ได้ | **วิธีที่ 1** | `C:\inetpub\wwwroot\complain\` |
| ระบบใหม่อยู่ที่ `/complaint/` มี URL Rewrite | **วิธีที่ 2** | `C:\inetpub\wwwroot\complaint\` |
| ไม่มี URL Rewrite Module มี Classic ASP | **วิธีที่ 3** | `C:\inetpub\wwwroot\complaint\` |

---

## การ Migrate ฐานข้อมูล

### สร้าง Migration ใหม่
```bash
dotnet ef migrations add MigrationName \
  --project SRT.Complaint \
  --context AppDbContext
```

### Apply Migration
```bash
dotnet ef database update \
  --project SRT.Complaint \
  --context AppDbContext

dotnet ef database update \
  --project SRT.Complaint \
  --context CorruptionDbContext
```

> รุ่นนี้ต้อง apply **ทั้ง 2 context**: `AppDbContext` เพิ่ม `StaffUsers.AuthVersion`, `NotificationDeliveryLogs` และตัวติดตามการเตือน SLA; `CorruptionDbContext` เพิ่มตาราง `corruption.Attachments`

### ย้อนกลับ Migration
```bash
dotnet ef database update PreviousMigrationName \
  --project SRT.Complaint \
  --context AppDbContext
```

### รายการ Migrations ทั้งหมด
| Migration | รายละเอียด |
|---|---|
| `AddMustChangePassword` | เพิ่มฟิลด์บังคับเปลี่ยนรหัสผ่านครั้งแรก |
| `AddTempPasswordExpiry` | เพิ่มวันหมดอายุรหัสผ่านชั่วคราว |
| `AddTempPasswordEncrypted` | เพิ่มการเก็บรหัสผ่านชั่วคราวแบบ encrypted |
| `AddSubCategories` | เพิ่มตารางหัวข้อย่อยเรื่องร้องเรียน |
| `MakeNoteAuthorNullable` | แก้ไข author ของ Note เป็น nullable |
| `AddComplaintSeq` | เพิ่ม sequence สำหรับเลขอ้างอิงเรื่องร้องเรียน |
| `AddReporterIdCard` | เพิ่มฟิลด์เลขบัตรประชาชนผู้แจ้ง |
| `AddComplaintTerms` | เพิ่มตารางข้อตกลงและเงื่อนไข |
| `AddContentBlocks` | เพิ่มตารางเนื้อหาหน้าแรก |
| `Corruption/AddCorruptionSeq` | เพิ่ม sequence สำหรับเรื่องทุจริต |
| `AddStaffAuthVersion` | เพิ่ม version ของ session เพื่อยกเลิก cookie เดิมทันทีเมื่อบัญชี/สิทธิ์เปลี่ยน |
| `AddNotificationDeliveryLog` | เพิ่มประวัติผลส่ง Email/SMS แบบปิดบังข้อมูลผู้รับ |
| `AddSlaWarningTracking` | ป้องกันการส่งเตือน SLA ซ้ำและใช้ค่า WarningThresholdPercent จริง |
| `Corruption/AddCorruptionAttachments` | เพิ่มที่เก็บ metadata ไฟล์หลักฐานของเรื่องทุจริต |

---

## บัญชีผู้ใช้เริ่มต้น

สร้างอัตโนมัติเมื่อรันครั้งแรก (ถ้าไม่มี SuperAdmin ในระบบ):

| รายการ | ค่า |
|---|---|
| Employee Code | `0000001` |
| Password | `Admin@1234` |
| Role | `SuperAdmin` |

> **สำคัญมาก:** เปลี่ยนรหัสผ่านทันทีหลัง login ครั้งแรก ระบบจะบังคับเปลี่ยนอัตโนมัติ

---

## Roles & Permissions

| Role | สิทธิ์ |
|---|---|
| `GeneralOfficer` | จัดการเรื่องร้องเรียนทั่วไปที่รับผิดชอบ |
| `CorruptionOfficer` | จัดการเรื่องทุจริต (แยกสมบูรณ์จากทั่วไป) |
| `SuperAdmin` | สิทธิ์ทั้งหมด + จัดการผู้ใช้งาน, ตั้งค่าระบบ, API Keys |

หน้าเรื่องร้องเรียนทั่วไปใช้ policy `GeneralComplaintAccess` (`GeneralOfficer`, `SuperAdmin`) และหน้าทุจริตใช้ `CorruptionAccess` (`CorruptionOfficer`, `SuperAdmin`) การเข้าผ่าน URL ตรงที่ไม่ตรงกับบทบาทจะถูกส่งไปหน้า Access Denied

---

## License

ระบบนี้พัฒนาสำหรับการรถไฟแห่งประเทศไทย (รฟท.) เท่านั้น
