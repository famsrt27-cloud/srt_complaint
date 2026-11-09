# รายงานตรวจสอบและปรับปรุง Partner API v1

วันที่ตรวจ: 3 พฤศจิกายน 2026  
ขอบเขต: REST API `/api/v1`, API key/scopes, Partner isolation, idempotency, rate limiting, webhooks, OpenAPI และเอกสารสำหรับ Partner  
แนวทาง: OWASP API Security Top 10:2023  
ผลการทดสอบล่าสุด: `dotnet test SRT.Complaint.Tests/SRT.Complaint.Tests.csproj --configuration Release --no-restore` ผ่าน 102/102 tests

## Executive summary

API มีมาตรการหลักที่เหมาะสมอยู่แล้ว ได้แก่ API key ที่ผูก environment/Partner/scopes, tenant filtering, pagination limit, idempotency, DTO allowlist, SSRF-safe webhook transport และ OpenAPI inventory

รอบนี้แก้ finding ที่ยืนยันได้ 6 รายการ โดยเน้น race condition ของ rate limiter, webhook ownership, validation ที่สัมพันธ์กับฐานข้อมูล, error contract และความตรงกันของเอกสารกับ API v1/workflow ใหม่ ไม่พบ endpoint ที่ไม่มี API key filter หรือ scope requirement

ข้อเสนอ production-scale 3 รายการได้รับอนุมัติและ implement แล้ว ได้แก่ distributed rate limiting, shared Data Protection key ring และ workflow-aware status transition ส่วนที่ยังต้องทำคือ deploy/configure infrastructure และรัน acceptance test บน topology จริง

## Endpoint inventory

| กลุ่ม | จำนวน | ผลตรวจ |
|---|---:|---|
| Complaint | 7 | Audited — validation, BOLA, DTO exposure, idempotency, pagination และ workflow fields |
| Metadata | 3 | Audited — API key + `metadata:read`, คืนเฉพาะข้อมูล metadata |
| Statistics | 3 | Audited — Partner filtering; corruption endpoint ต้อง scope + `DataAccessMode=All` |
| Webhook management | 3 | Audited — ownership, SSRF, event allowlist, quota และ one-time secret |
| รวม | 16 | ทุก endpoint อยู่ใต้ `/api/v1`, API key filter และ scope/data-access filter |

## Findings ที่แก้แล้ว

### [HIGH] API1: Webhook delete ownership ไม่ atomic

**Endpoint:** `DELETE /api/v1/webhooks/{id}`  
**Handler:** `Controllers/Api/WebhooksController.cs`  
**CWE:** CWE-367 / CWE-639

**Description:** เดิม controller list webhook ของ API key แล้วจึงสั่ง delete ด้วย `id` เพียงค่าเดียว ทำให้ ownership check และ mutation เป็นคนละ query

**Remediation:** เพิ่ม `DeleteForApiKeyAsync(id, apiKeyId)` ซึ่ง query ด้วยทั้ง webhook id และ API key id ก่อนปิดรายการใน operation เดียว Controller ไม่เรียก unscoped delete อีก

**Verification:** regression test ใช้ key A ลบ webhook ของ key B แล้วได้ `false` และ webhook B ยัง active

**Disposition:** Fixed

### [MEDIUM] API4: Rate-limit bucket มี race ตอนสร้างพร้อมกัน

**Endpoint:** ทุก `/api/v1/*` endpoint  
**Handler:** `Filters/ApiKeyAuthFilter.cs`  
**CWE:** CWE-362

**Description:** `IMemoryCache.GetOrCreate` ไม่รับประกันว่า factory ทำงานครั้งเดียว เมื่อ requests ชุดแรกมาพร้อมกันอาจได้คนละ bucket และผ่าน quota เกินกำหนด

**Remediation:** ใช้ singleton `ApiRateLimitStore` ที่มีหนึ่ง window ต่อ API key และ serialize การเพิ่ม count ด้วย `SemaphoreSlim` เพิ่ม `Remaining`, `Reset` และคำนวณ `Retry-After` ตามเวลาคงเหลือจริง

**Verification:** regression test ส่ง 100 operations พร้อมกันที่ limit 10 และอนุญาต 10 ครั้งเท่านั้น

**Disposition:** Fixed สำหรับ single instance

### [MEDIUM] API4: Webhook registration ไม่มี quota

**Endpoint:** `POST /api/v1/webhooks`  
**CWE:** CWE-770

**Description:** key ที่มี `webhooks:manage` สามารถสร้าง active webhook ได้ไม่จำกัด ทำให้เพิ่ม fan-out และ retry workload

**Remediation:** จำกัด 10 active webhooks ต่อ API key และ serialize ขั้นตอน count + insert ต่อ key ภายใน application instance พร้อม regression test

**Disposition:** Fixed สำหรับ single instance; deployment หลาย instance ควรบังคับ quota ที่ฐานข้อมูลหรือ API gateway เพิ่มเติม

### [MEDIUM] API3/API6: Category และ subcategory ไม่ตรวจความสัมพันธ์ใน API

**Endpoint:** `POST /api/v1/complaints`  
**CWE:** CWE-20

**Description:** Data annotations ตรวจเพียงชนิด/range แต่ไม่ยืนยันว่า category active หรือ subcategory อยู่ใต้ category ที่ส่งมา อาจสร้างข้อมูล business ที่ไม่สอดคล้องกัน

**Remediation:** ตรวจ active category และ active subcategory + parent category ก่อนเริ่ม idempotent write ตอบ `422 invalid_category` หรือ `422 invalid_subcategory`

**Disposition:** Fixed

### [LOW] API8: Error envelope ไม่สม่ำเสมอ

**Endpoint:** unhandled API errors, unknown routes/methods/media types และ corruption statistics  
**CWE:** CWE-755

**Description:** บางกรณีคืน string/HTML หรือไม่มี `traceId` ทำให้ client parse ต่างกัน และ developer exception page อาจตอบไม่เป็น API contract ใน Development

**Remediation:** เพิ่ม API exception middleware, JSON status-code pages สำหรับ 404/405/415 และปรับ controller error เป็น `{ error: { code, message, traceId } }`

**Disposition:** Fixed

### [LOW] API9: เอกสารหลักยังอ้าง route รุ่นเก่า

**Endpoint:** เอกสารทั้งหมด  
**CWE:** CWE-1059

**Description:** `API.md` และ README บางส่วนยังแสดง `/api/...` และอธิบาย rate limit เป็น sliding window ซึ่งไม่ตรง runtime `/api/v1/...`

**Remediation:** สร้าง `API.md` เป็น canonical v1 contract, เพิ่ม endpoint inventory 16 รายการ, workflow fields, headers/error contract/quota และชี้ README ไปยังเอกสารกับ OpenAPI runtime

**Disposition:** Fixed; ส่วน legacy ใน README ถูกทำเครื่องหมายว่าใช้อ้างอิงเท่านั้น

## Quality finding ที่แก้แล้ว

### API request log บันทึก status ของ ContentResult ผิด

`POST /api/v1/complaints` คืน `ContentResult` status 201 แต่ resolver เดิมรองรับเฉพาะ `ObjectResult` และ `StatusCodeResult` จึงบันทึกเป็น 200 แก้ให้ใช้ `IStatusCodeActionResult` ซึ่งครอบคลุม ContentResult และ action result มาตรฐาน

## Production hardening decisions (implemented)

### [HIGH] Distributed rate limiting

เพิ่ม runtime mode `SingleInstance`/`MultiInstance` และ provider `Local`/`Redis`/`Gateway` แล้ว โหมด MultiInstance ปฏิเสธการเริ่มระบบหากไม่มี Redis สำหรับ shared session หรือยังใช้ Local quota โดย Redis limiter ใช้ atomic `INCR` + expiry ต่อ API key/window

เมื่อ distributed limiter ล้มเหลว write จะ fail closed ด้วย `503`; safe read ใช้ emergency local limit ที่เข้มกว่าเพื่อรักษาความพร้อมใช้แบบจำกัด

**Disposition:** Remediated in code — ต้องกำหนด Redis/gateway และทดสอบ quota ผ่าน load balancer ก่อน Production

### [HIGH] Shared Data Protection key ring

Data Protection key ring ถูกเก็บในตาราง `DataProtectionKeys` ของฐานข้อมูลร่วม ใช้ application discriminator เดียวกันทุก instance และ Production บังคับ certificate เพื่อเข้ารหัส key at rest

**Disposition:** Remediated in code — ต้อง deploy migration, certificate/private-key ACL และทดสอบ cross-instance decrypt ก่อน Production

### [MEDIUM] Status transition เทียบกับ workflow ใหม่

`PUT /api/v1/complaints/{referenceNumber}/status` บังคับ transition matrix ตาม `workflowStep`, บังคับ note สำหรับสถานะที่มีผลทางธุรกิจ, ใช้ SQL Server `rowversion` ผ่าน `ETag`/`If-Match` และสงวน `complaints:update` ให้ Internal key เท่านั้น Existing External key ถูกบล็อกที่ endpoint แม้ยังมี scope ในฐานข้อมูล

**Disposition:** Remediated — External partner ใช้ read/webhook สำหรับติดตามผล; การเปลี่ยนสถานะเป็น Internal workflow command พร้อม optimistic concurrency

### [MEDIUM] OpenAPI exposure policy

`/openapi/v1.json` เปิดเป็น inventory ในทุก environment แม้ endpoint จริงยังบังคับ API key ควรกำหนดนโยบายองค์กรว่าจะเปิด public, จำกัด network หรือ publish ผ่าน developer portal

**Disposition:** Accepted temporarily — inventory ไม่มี secret แต่ต้องทบทวนก่อน Production

## OWASP API Top 10 summary

| หมวด | ผล |
|---|---|
| API1 Broken Object Level Authorization | ผ่าน; complaint/stat queries กรอง Partner และ webhook delete ผูก API key แบบ atomic |
| API2 Broken Authentication | ผ่านตาม model ปัจจุบัน; high-entropy key, hash-at-rest, expiry/revoke/environment/IP controls |
| API3 Broken Object Property Level Authorization | ผ่าน; ใช้ DTO และ explicit projections; เพิ่ม relational validation |
| API4 Unrestricted Resource Consumption | ผ่านระดับแอป; มี Redis/gateway distributed quota, fail policy, pagination/body/webhook limits; รอ infrastructure verification |
| API5 Broken Function Level Authorization | ผ่าน; ทุก endpoint มี scope และ corruption endpoint เพิ่ม data-access requirement |
| API6 Sensitive Business Flows | ผ่าน; create มี idempotency และ status update มี Internal-only transition policy + optimistic concurrency |
| API7 SSRF | ผ่าน; HTTPS, DNS/IP validation, connect-time revalidation และ no redirects |
| API8 Security Misconfiguration | ผ่านระดับแอป; error contract/security headers/forwarded proxy controls มีแล้ว |
| API9 Improper Inventory Management | ผ่าน; 16 endpoints อยู่ใต้ v1, OpenAPI + canonical API.md |
| API10 Unsafe Consumption of APIs | ผ่านสำหรับ webhook transport; timeout, limited response, no cookies/redirects/decompression |

## Production acceptance checklist

- [x] รองรับ distributed limiter/API gateway quota ใน application config
- [x] เก็บ shared Data Protection key ring ใน DB และบังคับ certificate ใน Production
- [ ] ตั้ง `ApiManagement:Environment` และ `ReverseProxy:KnownProxies`
- [ ] ทดสอบ Partner A ขอ resource ของ Partner B แล้วได้ 404
- [ ] ทดสอบ idempotency replay/conflict และ concurrent create
- [ ] ทดสอบ 429 headers และ quota ผ่าน load balancer จริง
- [ ] ทดสอบ webhook SSRF, signature, replay, duplicate event และ retry exhaustion
- [x] จำกัด `complaints:update` ให้ Internal key และบังคับ transition policy/`If-Match`
- [ ] ทดสอบ Redis quota, shared session และ Data Protection decrypt ข้าม instance บน infrastructure จริง
- [ ] ต่อ alert สำหรับ 401/403/429/5xx, outbox backlog และ webhook failure
