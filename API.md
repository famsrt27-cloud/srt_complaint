# SRT Complaint Partner API v1

เอกสารนี้เป็น API contract หลักของระบบรับเรื่องร้องเรียน รฟท. สำหรับการเชื่อมต่อกับ Partner ภายนอกและระบบภายใน

- API version: `v1`
- Route prefix: `/api/v1`
- OpenAPI document: `/openapi/v1.json`
- คู่มือเชื่อมต่อฉบับละเอียด: [`docs/partner-api-v1-integration-guide.md`](docs/partner-api-v1-integration-guide.md)
- Postman collection: [`docs/srt-complaint-api-v1.postman_collection.json`](docs/srt-complaint-api-v1.postman_collection.json)

> Base URL ของ Sandbox และ Production ต้องรับจากผู้ดูแลระบบ ห้ามสมมติ hostname จากตัวอย่างในเอกสาร

## Authentication และการแยกข้อมูล

ทุก endpoint ต้องส่ง API key ผ่าน header:

```http
X-API-Key: srt_test_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

API key แต่ละตัวผูกกับ:

- environment: `Sandbox` หรือ `Production`
- Partner เจ้าของข้อมูล
- scopes ที่ได้รับอนุญาต
- data access mode: `Own` หรือ `All`
- rate limit ต่อหนึ่งนาที
- วันหมดอายุ สถานะ revoke และ IP allowlist (ถ้ามี)

External key อนุญาตเฉพาะ `Own` จึงอ่านหรือแก้ได้เฉพาะ complaint ของ Partner ตนเอง หากขอ resource ของ Partner อื่น ระบบตอบ `404` เพื่อลดการเดาหรือไล่เลขอ้างอิง

### Scopes

| Scope | ใช้กับ |
|---|---|
| `complaints:read` | รายการและรายละเอียด complaint |
| `complaints:write` | สร้าง complaint |
| `complaints:status` | อ่านสถานะแบบย่อ |
| `complaints:update` | อัปเดตสถานะภาพรวม; อนุญาตเฉพาะ Internal key |
| `complaints:edoc` | อ่าน eDOC payload ซึ่งมี PII |
| `metadata:read` | category, status และ webhook event metadata |
| `stats:summary` | สถิติสรุป |
| `stats:detailed` | สถิติแยกรายละเอียด |
| `corruption:stats` | สถิติทุจริต; ต้องเป็น Internal key และ `DataAccessMode=All` |
| `webhooks:manage` | ลงทะเบียน ดู และปิด webhook |

## Request และ response มาตรฐาน

ส่ง JSON ด้วย UTF-8:

```http
Accept: application/json
Content-Type: application/json; charset=utf-8
X-API-Key: <secret>
```

วันเวลาใช้ ISO 8601 และ response ของระบบเป็น UTC ตัวอย่าง `2026-11-03T04:30:00Z` ส่วน `updatedSince` ควรมี offset หรือ `Z` ชัดเจน

### Error envelope

```json
{
  "error": {
    "code": "validation_error",
    "message": "Request validation failed",
    "details": {
      "dto.description": ["The field Description must be a string with a minimum length of 10."]
    },
    "traceId": "0HNA..."
  }
}
```

`details` มีเฉพาะ error บางประเภท เมื่อแจ้งปัญหาให้ส่ง `traceId`, เวลา UTC, method, path และ HTTP status โดยห้ามส่ง API key

| Status | ความหมายและการจัดการ |
|---:|---|
| `200` | สำเร็จ |
| `201` | สร้างสำเร็จ |
| `204` | ปิด webhook สำเร็จและไม่มี response body |
| `400` | รูปแบบ request, query หรือ header ไม่ถูกต้อง; แก้ก่อนส่งใหม่ |
| `401` | API key หาย ไม่ถูกต้อง หมดอายุ ถูก revoke หรือผิด environment |
| `403` | scope, data-access mode หรือ IP allowlist ไม่ผ่าน |
| `404` | ไม่พบ endpoint/resource หรือ resource ไม่ใช่ของ Partner |
| `405` | HTTP method ไม่ถูกต้อง |
| `409` | duplicate external reference หรือ idempotency conflict/in progress |
| `412` | `If-Match` ไม่ตรงกับ version ปัจจุบัน; อ่านข้อมูลล่าสุดแล้วพิจารณาคำสั่งใหม่ |
| `415` | endpoint เขียนข้อมูลต้องใช้ `application/json` |
| `422` | business validation ไม่ผ่าน เช่น category/subcategory/status ไม่ถูกต้อง |
| `428` | ไม่ส่ง `If-Match` สำหรับคำสั่งอัปเดตสถานะ |
| `429` | เกิน rate limit; รอตาม `Retry-After` พร้อม jitter |
| `503` | distributed rate limiter ไม่พร้อมใช้งาน; คำสั่งเขียนถูกปฏิเสธชั่วคราว |
| `500` | ระบบขัดข้อง; retry แบบ exponential backoff |

### Rate limiting

ระบบใช้ fixed window หนึ่งนาทีต่อ API key ภายใน application instance และส่ง header หลัง authentication สำเร็จ:

```http
X-RateLimit-Limit: 60
X-RateLimit-Remaining: 42
X-RateLimit-Reset: 1793673060
```

เมื่อเกินกำหนด:

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 23
X-RateLimit-Remaining: 0
```

เมื่อ `DistributedRuntime:Mode=MultiInstance` ระบบบังคับให้เลือก `RateLimitProvider=Redis` หรือ `Gateway` และใช้ Redis เป็น distributed session cache หากเลือก Redis ตัวนับ quota จะเป็น atomic และรวมกันทุก instance; หาก Redis ขัดข้อง คำสั่งเขียนตอบ `503 rate_limit_unavailable` ส่วน safe read ใช้ emergency local limit ที่เข้มกว่าชั่วคราว

## Endpoint inventory

ทุก endpoint ด้านล่างผ่าน API key authentication, scope check และ request logging

| Method | Path | Scope | หมายเหตุ |
|---|---|---|---|
| `GET` | `/api/v1/complaints` | `complaints:read` | Sync/reconciliation; แบ่งหน้า 1–100 รายการ |
| `GET` | `/api/v1/complaints/by-external-reference/{externalReference}` | `complaints:read` | ค้นด้วยเลขของ Partner |
| `GET` | `/api/v1/complaints/{referenceNumber}` | `complaints:read` | รายละเอียดรวม PII |
| `POST` | `/api/v1/complaints` | `complaints:write` | ต้องมี `Idempotency-Key` |
| `GET` | `/api/v1/complaints/{referenceNumber}/status` | `complaints:status` | สถานะและ workflow แบบย่อ |
| `PUT` | `/api/v1/complaints/{referenceNumber}/status` | `complaints:update` | Internal key เท่านั้น; ต้องส่ง `If-Match` และผ่าน transition policy |
| `GET` | `/api/v1/complaints/{referenceNumber}/edoc-payload` | `complaints:edoc` | ข้อมูลสำหรับ eDOC; มี PII |
| `GET` | `/api/v1/metadata/categories` | `metadata:read` | เฉพาะ category/subcategory ที่ active |
| `GET` | `/api/v1/metadata/statuses` | `metadata:read` | status codes ที่ API รองรับ |
| `GET` | `/api/v1/metadata/webhook-events` | `metadata:read` | event codes ที่ลงทะเบียนได้ |
| `GET` | `/api/v1/stats/summary` | `stats:summary` | สถิติตาม data-access mode |
| `GET` | `/api/v1/stats/detailed` | `stats:detailed` | แยก category, priority, status |
| `GET` | `/api/v1/stats/corruption` | `corruption:stats` | Internal + All เท่านั้น |
| `GET` | `/api/v1/webhooks` | `webhooks:manage` | รายการของ API key ปัจจุบัน |
| `POST` | `/api/v1/webhooks` | `webhooks:manage` | สูงสุด 10 active webhooks ต่อ API key |
| `DELETE` | `/api/v1/webhooks/{id}` | `webhooks:manage` | ปิดเฉพาะ webhook ของ API key ปัจจุบัน |

## Complaint API

### List / reconciliation

```http
GET /api/v1/complaints?updatedSince=2026-11-03T00:00:00Z&status=InProgress&page=1&pageSize=20
X-API-Key: <secret>
```

Query:

- `updatedSince`: optional ISO 8601 timestamp พร้อม offset
- `status`: optional; ต้องอยู่ใน metadata status list
- `page`: ค่าเริ่มต้น `1`, ต้องไม่น้อยกว่า `1`
- `pageSize`: ค่าเริ่มต้น `20`, ช่วง `1–100`

```json
{
  "data": [
    {
      "referenceNumber": "SRT-COMPL-2569-0123",
      "externalReference": "PARTNER-2026-000123",
      "status": "InProgress",
      "workflowStep": "Investigation",
      "workflowStepTh": "กำลังดำเนินการ",
      "workflowUpdatedAt": "2026-11-03T04:20:00Z",
      "priority": "Normal",
      "category": "การให้บริการ",
      "subjectStation": "กรุงเทพอภิวัฒน์",
      "createdAt": "2026-11-02T03:00:00Z",
      "updatedAt": "2026-11-03T04:20:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "total": 1,
    "totalPages": 1
  }
}
```

`status` เป็นสถานะภาพรวมเพื่อความเข้ากันได้กับระบบเดิม ส่วน `workflowStep` เป็นขั้นตอน back-office ปัจจุบัน Partner ต้องไม่ใช้สอง field นี้แทนกัน

### Create complaint

```http
POST /api/v1/complaints
Content-Type: application/json
X-API-Key: <secret>
Idempotency-Key: 6f8c4de2-7d8b-4ae7-ae70-df33c8fe15ad
```

```json
{
  "externalReference": "PARTNER-2026-000123",
  "reporterName": "สมชาย ใจดี",
  "reporterPhone": "0812345678",
  "reporterEmail": "somchai@example.com",
  "categoryId": 1,
  "subCategoryId": 3,
  "subjectStation": "กรุงเทพอภิวัฒน์",
  "incidentDate": "2026-11-02",
  "description": "รายละเอียดเรื่องร้องเรียนอย่างน้อย 10 ตัวอักษร",
  "latitude": 13.803,
  "longitude": 100.539
}
```

Validation สำคัญ:

- `externalReference`: บังคับ, สูงสุด 100 ตัวอักษร และ unique ต่อ Partner
- `reporterName`: บังคับ, สูงสุด 200 ตัวอักษร
- `reporterPhone`: บังคับ, สูงสุด 20 ตัวอักษร
- `reporterEmail`: optional, รูปแบบอีเมล, สูงสุด 200 ตัวอักษร
- `categoryId`: ต้องเป็น active category
- `subCategoryId`: ถ้ามีต้อง active และอยู่ใต้ `categoryId` ที่ส่งมา
- `description`: 10–5,000 ตัวอักษร
- `latitude`: `-90..90`; `longitude`: `-180..180`
- request body จำกัด 256 KiB และ endpoint นี้ไม่รับไฟล์แนบ

Response `201`:

```json
{
  "referenceNumber": "SRT-COMPL-2569-0123",
  "externalReference": "PARTNER-2026-000123",
  "status": "Pending",
  "slaDeadline": "2026-11-10T04:30:00Z",
  "resourceUrl": "/api/v1/complaints/SRT-COMPL-2569-0123",
  "message": "รับเรื่องร้องเรียนเรียบร้อยแล้ว"
}
```

#### Idempotency

- `Idempotency-Key` บังคับและยาว 8–128 ตัวอักษร; แนะนำ UUID v4
- ระบบเก็บ request hash และผลลัพธ์ 24 ชั่วโมงต่อ API key
- key เดิม + payload เดิม: replay response เดิมและส่ง `Idempotent-Replayed: true`
- key เดิม + payload ต่าง: `409 idempotency_conflict`
- request เดิมยังประมวลผล: `409 request_in_progress`
- เมื่อ client timeout ให้ retry payload เดิมด้วย key เดิม

### Read by reference

```http
GET /api/v1/complaints/SRT-COMPL-2569-0123
GET /api/v1/complaints/by-external-reference/PARTNER-2026-000123
```

Response มีข้อมูลผู้ร้อง จึงต้องจำกัดการเก็บ log/cache และใช้ scope `complaints:read` เฉพาะระบบที่จำเป็น Response มีทั้ง `status`, `statusTh`, `workflowStep`, `workflowStepTh` และ `workflowUpdatedAt`

### Read status

```http
GET /api/v1/complaints/SRT-COMPL-2569-0123/status
```

Response ส่ง `ETag` และ field `version` (Base64 ของ SQL Server `rowversion`) สำหรับ optimistic concurrency ตัวอย่าง `ETag: "AAAAAAAAB9E="` ต้องเก็บค่านี้ไว้ใช้กับคำสั่งอัปเดตสถานะ

```json
{
  "referenceNumber": "SRT-COMPL-2569-0123",
  "externalReference": "PARTNER-2026-000123",
  "status": "InProgress",
  "statusTh": "กำลังดำเนินการ",
  "workflowStep": "Investigation",
  "workflowStepTh": "กำลังดำเนินการ",
  "workflowUpdatedAt": "2026-11-03T04:20:00Z",
  "updatedAt": "2026-11-03T04:20:00Z",
  "closedAt": null,
  "slaBreached": false,
  "version": "AAAAAAAAB9E="
}
```

### Update summary status

```http
PUT /api/v1/complaints/SRT-COMPL-2569-0123/status
Content-Type: application/json
If-Match: "AAAAAAAAB9E="
```

```json
{
  "newStatus": "Resolved",
  "note": "Partner ดำเนินการแก้ไขแล้ว"
}
```

Endpoint นี้สงวนให้ Internal key เท่านั้น External key จะได้ `403 external_status_update_disabled` แม้เป็น key เดิมที่เคยมี scope นี้อยู่ และไม่สามารถสร้าง External key ใหม่พร้อม `complaints:update` ได้

`newStatus` ต้องอยู่ในรายการจาก `/metadata/statuses`; `note` สูงสุด 2,000 ตัวอักษรและบังคับเมื่อเปลี่ยนเป็น `WaitingInfo`, `Forwarded`, `Closed` หรือ `Rejected` Client กำหนด `workflowStep` โดยตรงไม่ได้ ระบบตรวจสถานะกับ workflow ปัจจุบันดังนี้:

| Workflow ปัจจุบัน | Status ที่อนุญาต |
|---|---|
| `Received` | `Pending`, `Rejected` |
| `Screening` | `InProgress`, `WaitingInfo`, `Rejected` |
| `WaitingInfo` | `WaitingInfo`, `InProgress`, `Rejected` |
| `Classification`, `Investigation` | `InProgress`, `Forwarded` |
| `Summary` | `Resolved` |
| `Notification`, `NotificationRetry` | `Resolved`, `Closed` |
| `Completed` | `Closed` |
| `Rejected` | `Rejected` |

หากไม่ส่ง `If-Match` ตอบ `428`; หาก version ล้าสมัยตอบ `412`; หาก transition ไม่ถูกต้องตอบ `409 invalid_status_transition` เมื่อสำเร็จ response และ `ETag` ใหม่จะมี version ล่าสุด

### eDOC payload

```http
GET /api/v1/complaints/SRT-COMPL-2569-0123/edoc-payload
```

Response รวม reporter, complaint และ resolution data และมีข้อมูลส่วนบุคคล จึงควรให้ scope นี้เฉพาะระบบ eDOC หรือระบบภายในที่จำเป็น

## Metadata API

```http
GET /api/v1/metadata/categories
GET /api/v1/metadata/statuses
GET /api/v1/metadata/webhook-events
```

Partner ควรดึง category และ status จาก metadata แทนการฝังค่าไว้ใน client

## Statistics API

```http
GET /api/v1/stats/summary
GET /api/v1/stats/detailed
GET /api/v1/stats/corruption
```

สถิติ complaint ปกติถูกกรองตาม data-access mode ของ key ส่วน corruption statistics ต้องใช้ Internal key ที่มี `DataAccessMode=All`

## Webhook API

### Register

```http
POST /api/v1/webhooks
Content-Type: application/json
```

```json
{
  "name": "Partner production receiver",
  "targetUrl": "https://partner.example.com/webhooks/srt",
  "events": ["complaint.created", "complaint.status_changed", "complaint.closed"]
}
```

ข้อกำหนด:

- สูงสุด 10 active webhooks ต่อ API key
- URL ต้องเป็น HTTPS, ไม่มี userinfo และผ่าน SSRF validation
- private/loopback/reserved destination ถูกปฏิเสธ เว้นแต่ผู้ดูแลเพิ่ม host ใน allowlist โดยตั้งใจ
- event ถูก normalize เป็น lowercase และตัดค่าซ้ำ
- secret แสดงครั้งเดียวใน response `201`; ต้องเก็บใน Secret Manager

### List และ delete

```http
GET /api/v1/webhooks
DELETE /api/v1/webhooks/123
```

การ list และ delete ถูกผูกกับ API key ปัจจุบันแบบ server-side; id ของ key อื่นตอบ `404`

### Outbound delivery

Headers:

```http
X-SRT-Event: complaint.status_changed
X-SRT-Event-Id: <event-guid>
X-SRT-Delivery-Id: <delivery-guid>
X-SRT-Timestamp: <unix-seconds>
X-SRT-Signature: v1=<lowercase-hex-hmac-sha256>
```

Signature input คือ `${timestamp}.${rawRequestBody}` โดยใช้ webhook secret เป็น HMAC-SHA256 key ฝั่งรับควร:

1. ใช้ raw body ก่อน parse JSON
2. ตรวจ timestamp tolerance ไม่เกิน 5 นาที
3. เปรียบเทียบ signature แบบ constant time
4. deduplicate ด้วย Event ID หรือ Delivery ID
5. ตอบ `2xx` หลังบันทึก event แบบ durable แล้ว

ระบบไม่ follow redirect, ไม่ใช้ cookie, จำกัด timeout/response body และ revalidate destination ตอน connect เพื่อลด SSRF/DNS rebinding

## Security และ production notes

- เก็บ API key และ webhook secret ใน Secret Manager; ห้ามใส่ใน URL, source code หรือ application log
- response API ส่ง `Cache-Control: no-store`
- production ต้องตั้ง `ApiManagement:Environment`, HTTPS และ `ReverseProxy:KnownProxies`
- deployment หลาย instance ต้องตั้ง `DistributedRuntime:Mode=MultiInstance`, Redis session cache และ Redis/gateway quota; ระบบ fail startup หาก config ไม่ครบ
- Data Protection key ring เก็บในฐานข้อมูลร่วมและ Production บังคับ certificate สำหรับเข้ารหัส key at rest; ทุก instance ต้องใช้ `DataProtection:ApplicationName` เดียวกัน
- ควร alert เมื่อ 401/403/429/5xx เพิ่มขึ้น, webhook retry หมด, outbox ค้าง หรือ key ใกล้หมดอายุ
- OpenAPI เป็น inventory ของ endpoint ไม่ใช่สิทธิ์เข้าถึงข้อมูลจริง; ทุก endpoint ยังบังคับ API key และ scope

## Changelog

### 2026-11-03

- ทำเอกสาร `/api/v1` ให้เป็น canonical และลบ route รุ่นเก่าจากเอกสารหลัก
- เพิ่ม `workflowStep`, `workflowStepTh`, `workflowUpdatedAt` ใน complaint list/detail/status
- เพิ่ม response headers `X-RateLimit-Remaining` และ `X-RateLimit-Reset`; `Retry-After` คำนวณตามเวลาคงเหลือจริง
- ทำ rate-limit bucket ให้ atomic ภายใน instance และแก้ request log ให้บันทึก status ของ `ContentResult` ถูกต้อง
- ทำ webhook delete โดยใส่ทั้ง webhook id และ API key id ใน ownership predicate ก่อนปิดรายการ พร้อมจำกัด 10 active webhooks ต่อ API key
- เพิ่ม validation ว่า category/subcategory ต้อง active และสัมพันธ์กัน
- ทำ error envelope ของ unhandled error, 404, 405 และ 415 ให้เป็น JSON สม่ำเสมอ
