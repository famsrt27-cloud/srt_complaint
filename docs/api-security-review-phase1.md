# รายงานตรวจสอบความปลอดภัย API — Phase 1

วันที่ตรวจ: 27 ตุลาคม 2026  
ขอบเขต: REST API `/api/v1`, API Key, Partner ownership, Idempotency, Webhook/Outbox, OpenAPI และหน้าจอบริหาร Integration  
แนวทาง: OWASP API Security Top 10:2023  
สถานะโดยรวม: **พร้อมทดสอบในสภาพแวดล้อม Sandbox หลังรัน migration และตั้งค่าความลับของแต่ละ environment**

## Executive summary

Phase 1 ปิดความเสี่ยงสำคัญที่พบในโครงสร้างเดิม ได้แก่ การอ่านข้อมูลข้าม Partner (BOLA), การกระจาย webhook ไปผิดเจ้าของข้อมูล, SSRF ผ่าน URL ของ webhook, การส่งคำขอซ้ำ, API ที่ไม่มี version/inventory และรหัสผ่านเริ่มต้นที่ฝังใน source code

ยังมีงานระดับ production operations ที่ต้องทำก่อน scale หลาย instance: ใช้ distributed rate limiter, ใช้ shared Data Protection key ring และเชื่อมระบบ alert/monitoring กลาง รายการเหล่านี้ไม่ขัดขวางการทดสอบ Sandbox แบบ single instance

## สรุปตาม OWASP API Security Top 10

| หมวด | ผล | หลักฐาน/มาตรการ |
|---|---|---|
| API1 Broken Object Level Authorization | ผ่าน | ทุก complaint query ตรวจ `PartnerId` และ `DataAccessMode`; ข้อมูลคนละ Partner ตอบ 404 |
| API2 Broken Authentication | ผ่านสำหรับ Phase 1 | key สุ่มแบบ CSPRNG, เก็บเฉพาะ SHA-256, ผูก Sandbox/Production instance, expiry/revoke/rotation, partner status, IP allowlist และ rate limit |
| API3 Broken Object Property Level Authorization | ผ่าน | ใช้ request/response DTO ที่ระบุ field ชัดเจน ไม่ bind entity โดยตรง |
| API4 Unrestricted Resource Consumption | ผ่านแบบ single instance | จำกัด page size, request fields, rate ต่อ key, timeout และ response log; งาน distributed ระบุใน residual risk |
| API5 Broken Function Level Authorization | ผ่าน | scope filter ราย endpoint และ `DataAccessMode=All` จำกัดเฉพาะ Internal key |
| API6 Unrestricted Access to Sensitive Business Flows | ผ่าน | POST complaint บังคับ `Idempotency-Key`, unique constraint และ replay/conflict semantics |
| API7 Server Side Request Forgery | ผ่าน | HTTPS only, DNS/IP validation, block private/reserved ranges, no redirects และ revalidate IP ตอน connect |
| API8 Security Misconfiguration | ผ่านเมื่อ deploy ตาม runbook | ไม่มี bootstrap password ใน source, API error ไม่เผย stack trace, มี security headers และ secret protection |
| API9 Improper Inventory Management | ผ่าน | ทุก public endpoint อยู่ใต้ `/api/v1` และเผย OpenAPI document `/openapi/v1.json` |
| API10 Unsafe Consumption of APIs | ผ่าน | webhook timeout, no redirect/cookie/decompression, จำกัด response ที่บันทึก และ retry แบบควบคุม |

## Findings ที่แก้แล้ว

### API-SEC-001 — Complaint BOLA / ข้อมูลข้าม Partner

- Severity เดิม: Critical
- OWASP: API1:2023 Broken Object Level Authorization
- CWE: CWE-639 Authorization Bypass Through User-Controlled Key
- ตำแหน่ง: `Controllers/Api/ComplaintsController.cs`, `Controllers/Api/StatsController.cs`
- หลักฐานเดิม: การค้นจาก reference และสถิติไม่ได้ผูกกับเจ้าของ API key
- รูปแบบโค้ดเสี่ยงเดิม: query ใช้เพียง `ReferenceNumber`/`ExternalId` โดยไม่มีเงื่อนไข tenant ownership
- สถานการณ์โจมตี: Partner A เดาหรือได้รับเลขอ้างอิงของ Partner B แล้วเรียก endpoint อ่านรายละเอียด/สถานะ ทำให้ข้อมูลผู้ร้องเรียนรั่วไหล
- การแก้ไข: ผูก API key กับ Partner; filter ทุก query ด้วย `SourcePartnerId` เมื่อ key เป็น `Own`; คืน 404 เมื่อ resource ไม่ได้เป็นของ Partner
- สถานะ: Remediated

### API-SEC-002 — Webhook ส่งข้อมูลไปยัง subscriber ที่ไม่ใช่เจ้าของ

- Severity เดิม: Critical
- OWASP: API1/API5
- CWE: CWE-862 Missing Authorization
- ตำแหน่ง: `Services/WebhookService.cs`, `Controllers/Api/WebhooksController.cs`
- หลักฐานเดิม: event สามารถ fan-out ไปยัง webhook โดยไม่แยก tenant
- รูปแบบโค้ดเสี่ยงเดิม: เลือก webhook ด้วย event type/สถานะ แต่ไม่มี `PartnerId` ในเงื่อนไข
- สถานการณ์โจมตี: webhook ของ Partner หนึ่งได้รับ payload ที่สร้างจาก complaint ของอีก Partner โดยไม่ต้องร้องขอ resource โดยตรง
- การแก้ไข: webhook ทุกตัวมี `PartnerId`; dispatcher เลือกเฉพาะ Partner เจ้าของ event หรือ Internal key ที่ได้รับ `All`; CRUD webhook ตรวจ ownership
- สถานะ: Remediated

### API-SEC-003 — SSRF ผ่าน Webhook URL

- Severity เดิม: High
- OWASP: API7:2023 Server Side Request Forgery
- CWE: CWE-918 Server-Side Request Forgery
- ตำแหน่ง: `Services/WebhookUrlValidator.cs`, `Services/WebhookHttpMessageHandlerFactory.cs`
- หลักฐานเดิม: ผู้ใช้กำหนด callback URL ที่ server เรียกออกไป
- รูปแบบโค้ดเสี่ยงเดิม: ส่ง HTTP request ไปยัง URL ที่ผู้ใช้กำหนดโดยไม่ตรวจ scheme, resolved IP หรือ redirect
- สถานการณ์โจมตี: Partner ลงทะเบียน URL ไปยัง loopback, private service หรือ cloud metadata เพื่อให้ application server อ่าน/กระตุ้นระบบภายใน
- การแก้ไข: อนุญาต HTTPS เท่านั้น; ห้าม userinfo; resolve DNS และ block loopback/private/link-local/reserved; ปิด redirect; ตรวจและเลือก safe IP ซ้ำใน `ConnectCallback` เพื่อลด DNS rebinding/TOCTOU; private host ใช้ได้เมื่ออยู่ใน allowlist ของผู้ดูแลเท่านั้น
- สถานะ: Remediated

### API-SEC-004 — Duplicate submission / replay ของการสร้างเรื่อง

- Severity เดิม: High
- OWASP: API6:2023 Unrestricted Access to Sensitive Business Flows
- CWE: CWE-799 Improper Control of Interaction Frequency
- ตำแหน่ง: `Services/IdempotencyService.cs`, `Controllers/Api/ComplaintsController.cs`
- รูปแบบโค้ดเสี่ยงเดิม: ทุก retry ของ `POST` สร้าง business record ใหม่โดยไม่มี transaction identity
- สถานการณ์โจมตี/ความผิดพลาด: network timeout ทำให้ Partner retry และสร้างเรื่องซ้ำจำนวนมาก หรือ client ส่งซ้ำโดยตั้งใจ
- การแก้ไข: บังคับ `Idempotency-Key` 8–128 ตัวอักษร; เก็บ request hash และผลลัพธ์ 24 ชั่วโมง; key เดิม+payload เดิม replay ผล; payload ต่างกันตอบ 409; unique DB constraint ป้องกัน race
- สถานะ: Remediated

### API-SEC-005 — ไม่มี API version และ machine-readable inventory

- Severity เดิม: Medium
- OWASP: API9:2023 Improper Inventory Management
- CWE: CWE-1059 Incomplete Documentation
- ตำแหน่ง: `Controllers/Api/*`, `OpenApi/ApiOpenApiTransformer.cs`, `Program.cs`
- รูปแบบโค้ดเสี่ยงเดิม: endpoint ใช้ route แบบไม่ระบุ version และไม่มี inventory ที่สร้างจาก runtime metadata
- สถานการณ์ความเสี่ยง: Partner ใช้ endpoint เก่าที่ควบคุม/ติดตามได้ยาก หรือทีมปฏิบัติการไม่ทราบ API ที่เปิดอยู่ทั้งหมด
- การแก้ไข: ย้าย public routes ไป `/api/v1`; กำหนด API group `v1`; เปิด OpenAPI JSON ที่ `/openapi/v1.json`; ระบุ API-key security scheme
- สถานะ: Remediated

### API-SEC-006 — Bootstrap administrator password ใน source/config

- Severity เดิม: Critical
- OWASP: API8:2023 Security Misconfiguration
- CWE: CWE-798 Use of Hard-coded Credentials
- ตำแหน่ง: `Program.cs`, `appsettings.json`
- รูปแบบโค้ดเสี่ยงเดิม: application สร้างผู้ดูแลเริ่มต้นด้วย credential ที่รู้ค่าได้จาก source/config ตัวอย่าง
- สถานการณ์โจมตี: ผู้ที่ทราบค่าคงที่เข้าสู่ระบบ SuperAdmin บน environment ที่ยังไม่เปลี่ยนรหัสผ่าน
- การแก้ไข: อ่านจาก `BootstrapAdmin:Password`/environment secret; production จะ fail startup หากฐานข้อมูลใหม่แต่ไม่ได้ตั้งค่า; development เตือนแทนการสร้าง account ด้วยรหัสผ่านคงที่
- สถานะ: Remediated

## Residual risks และงานก่อน Production scale

### API-SEC-007 — Rate limit เป็น in-memory ต่อ application instance

- Severity: Medium
- OWASP: API4:2023 Unrestricted Resource Consumption
- CWE: CWE-770 Allocation of Resources Without Limits or Throttling
- ตำแหน่ง: `Filters/ApiKeyAuthFilter.cs`
- ผลกระทบ: เมื่อรันหลาย instance ขีดจำกัดจริงจะคูณตามจำนวน instance และ counter ไม่แชร์กัน
- คำแนะนำ: เปลี่ยนเป็น Redis/distributed rate limiter หรือบังคับ quota ที่ API Gateway/WAF โดยใช้ key prefix/partner เป็น partition
- สถานะ: Open — ยอมรับเฉพาะ Sandbox/single instance

### API-SEC-008 — Data Protection key ring ต้องแชร์ระหว่าง instance

- Severity: Medium
- OWASP: API8
- CWE: CWE-321 Use of Hard-coded Cryptographic Key (ความเสี่ยงด้านการจัดการ key)
- ตำแหน่ง: การตั้งค่า deployment ของ ASP.NET Core Data Protection
- ผลกระทบ: instance อื่นหรือ instance หลัง redeploy อาจถอด webhook secret เดิมไม่ได้ หาก key ring เป็น ephemeral/local
- คำแนะนำ: เก็บ key ring ใน persistent shared storage และป้องกันด้วย certificate/KMS; จำกัดสิทธิ์และทำ backup
- สถานะ: Open — deployment prerequisite

### API-SEC-009 — Authentication ระดับสูงกว่า API key ยังไม่อยู่ใน Phase 1

- Severity: Low/Informational
- OWASP: API2:2023 Broken Authentication
- ตำแหน่ง: รูปแบบ integration โดยรวม
- ผลกระทบ: API key เป็น bearer credential; ผู้ที่ได้ key และผ่าน IP policy สามารถใช้สิทธิ์ของ key ได้
- คำแนะนำ: Partner ความเสี่ยงสูงควรเพิ่ม mTLS หรือ OAuth 2.0 Client Credentials ใน Phase ต่อไป; ระหว่างนี้ใช้ least-privilege scope, IP allowlist, expiry และ rotation
- สถานะ: Accepted for Phase 1

### API-SEC-010 — Monitoring/alerting ต้องเชื่อมระบบส่วนกลาง

- Severity: Medium
- OWASP: API8/API10
- CWE: CWE-778 Insufficient Logging
- ตำแหน่ง: `ApiRequestLogs`, `WebhookDeliveryLogs`, deployment monitoring
- ผลกระทบ: มี log ในฐานข้อมูลแล้ว แต่หากไม่มี alert อาจตรวจพบ brute force, 429 spike หรือ webhook failure ช้า
- คำแนะนำ: แจ้งเตือน failed authentication, 429 rate, 5xx rate, outbox backlog, webhook retry exhausted และ key ใกล้หมดอายุ; กำหนด retention/redaction
- สถานะ: Open — operations integration

## Security acceptance gate ก่อนเปิด Partner จริง

1. รัน migration และตรวจ schema/backup
2. ตั้งค่า production secrets, shared Data Protection key ring และ HTTPS/TLS ที่ reverse proxy
3. สร้าง Partner และ Sandbox key ด้วย scope ต่ำสุด; ห้ามส่ง raw key ทางอีเมลหรือแชตทั่วไป
4. ทดสอบว่า Partner A อ่าน reference ของ Partner B แล้วได้ 404
5. ทดสอบ idempotency ทั้ง replay และ payload conflict
6. ทดสอบ webhook signature, timestamp tolerance, duplicate event และ retry
7. ยืนยันว่า private/reserved webhook targets ถูกปฏิเสธ เว้นแต่ allowlist โดยผู้ดูแล
8. ต่อ dashboard/alert ตาม API-SEC-010 และทบทวน access ทุกไตรมาส

