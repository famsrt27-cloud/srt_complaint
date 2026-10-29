# คู่มือเชื่อมต่อ SRT Complaint Partner API v1

ไฟล์พร้อมนำเข้า Postman:

- [Postman Collection](srt-complaint-api-v1.postman_collection.json)
- [Sandbox Environment Template](srt-complaint-sandbox.postman_environment.json)

หลังนำเข้าให้แก้ `baseUrl` และใส่ `apiKey` ใน Current value ของ Environment โดยไม่บันทึกหรือส่งออกค่าความลับร่วมกับไฟล์

เอกสารฉบับนี้เป็นเอกสารหลักสำหรับหน่วยงานหรือระบบภายนอกที่เชื่อมต่อกับระบบรับเรื่องร้องเรียนของการรถไฟแห่งประเทศไทย ทั้งการส่งข้อมูลเข้าระบบ การอ่านสถานะ และการรับเหตุการณ์กลับผ่าน Webhook

> รุ่นเอกสาร: 1.0  
> API version: v1  
> Authentication: API Key ต่อ Partner และ Environment  
> รูปแบบข้อมูล: JSON / UTF-8 / HTTPS

## 1. ภาพรวมการเชื่อมต่อ

ระบบรองรับการเชื่อมต่อสองทิศทาง

```text
ระบบ Partner                         SRT Complaint
     |                                     |
     | POST /api/v1/complaints             |
     | X-API-Key + Idempotency-Key          |
     |------------------------------------>|
     | 201 + referenceNumber               |
     |<------------------------------------|
     |                                     |
     | POST Webhook + HMAC signature       |
     |<------------------------------------|
     | 2xx                                 |
     |------------------------------------>|
     |                                     |
     | GET รายการ/สถานะเพื่อ reconciliation|
     |------------------------------------>|
```

Partner แต่ละรายจะมีข้อมูลเจ้าของแยกจากกัน คีย์แบบ `Own` อ่านและแก้ไขได้เฉพาะเรื่องที่สร้างในนาม Partner เดียวกัน แม้ทราบเลขอ้างอิงของ Partner อื่น ระบบจะตอบ `404 not_found`

## 2. ข้อมูลที่ต้องแลกเปลี่ยนก่อนเริ่มเชื่อมต่อ

### 2.1 ข้อมูลที่ Partner ต้องแจ้งแก่ผู้ดูแล SRT

1. ชื่อหน่วยงานและชื่อระบบ
2. รหัสระบบที่ต้องการใช้ เช่น `EDOC` หรือ `CRM`
3. ชื่อ อีเมล และโทรศัพท์ของผู้ประสานงานด้านเทคนิค
4. วัตถุประสงค์การใช้ API
5. Environment ที่ต้องการ: Sandbox หรือ Production
6. รายการ Scope ที่ต้องใช้
7. Public egress IP หรือ CIDR ที่จะเรียก API
8. ปริมาณการเรียกโดยประมาณต่อนาทีและต่อวัน
9. Webhook URL สำหรับ Sandbox และ Production หากต้องการรับ Event
10. รายชื่อโดเมนหรือใบรับรองที่เกี่ยวข้องกับ Webhook

### 2.2 สิ่งที่ผู้ดูแล SRT จะส่งให้ Partner

1. Base URL ของ Environment
2. API Key ซึ่งแสดงเพียงครั้งเดียว
3. รายการ Scope และขอบเขตข้อมูลของคีย์
4. Rate limit ที่กำหนด
5. วันหมดอายุของคีย์
6. OpenAPI URL
7. Webhook signing secret ซึ่งแสดงเพียงครั้งเดียวหลังลงทะเบียน Webhook
8. ช่องทางติดต่อเมื่อเกิด Incident

ห้ามส่ง API Key หรือ Webhook secret ผ่านอีเมล แชต Ticket หรือบันทึกไว้ใน source code ควรใช้ Secret Manager ของระบบ Partner

## 3. Environment และ URL

ผู้ดูแลระบบจะระบุ hostname จริงให้แต่ละ Partner

| Environment | Base URL ตัวอย่าง | วัตถุประสงค์ |
|---|---|---|
| Sandbox | `https://sandbox.example.go.th/api/v1` | ทดสอบข้อมูลจำลอง |
| Production | `https://api.example.go.th/api/v1` | ข้อมูลใช้งานจริง |

OpenAPI document:

```text
GET https://<hostname>/openapi/v1.json
```

ห้ามนำ Sandbox API Key ไปใช้กับ Production หรือสลับกัน

## 4. Authentication

ทุก Endpoint ใน `/api/v1` ต้องส่ง API Key ผ่าน Header

```http
X-API-Key: srt_test_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

ตัวอย่าง:

```bash
curl --request GET \
  --url "https://<hostname>/api/v1/metadata/statuses" \
  --header "Accept: application/json" \
  --header "X-API-Key: $SRT_API_KEY"
```

ห้ามส่ง API Key ผ่าน Query string เช่น `?api_key=...` เพราะค่าอาจปรากฏใน access log, browser history หรือ proxy log

### 4.1 รูปแบบ API Key

| Prefix | ความหมาย |
|---|---|
| `srt_test_` | Sandbox |
| `srt_live_` | Production |

ระบบ SRT เก็บเฉพาะ SHA-256 hash ของ API Key และไม่สามารถเปิดดู Raw Key ภายหลังได้ หากคีย์สูญหายให้ Rotate หรือสร้างใหม่

### 4.2 HTTP status ที่เกี่ยวกับ Authentication

| Status | Code | ความหมาย |
|---:|---|---|
| 401 | `authentication_required` | ไม่มี Header หรือ Header ไม่ถูกต้อง |
| 401 | `invalid_api_key` | คีย์ผิด หมดอายุ Partner ถูกปิด หรือคีย์ถูกยกเลิก |
| 403 | `insufficient_scope` | คีย์ไม่มี Scope ที่ Endpoint ต้องการ |
| 403 | `insufficient_data_access` | คีย์ไม่มีขอบเขตข้อมูลระดับที่ Endpoint ต้องการ |
| 403 | `ip_not_allowed` | IP ผู้เรียกไม่อยู่ใน whitelist |
| 429 | `rate_limit_exceeded` | เรียกเกิน Rate limit |

## 5. Scope

| Scope | ใช้สำหรับ | ข้อมูลสำคัญ |
|---|---|---|
| `metadata:read` | อ่านหมวดหมู่ สถานะ และ Webhook event | แนะนำทุก Partner |
| `complaints:write` | สร้างเรื่องร้องเรียน | ต้องใช้ `Idempotency-Key` |
| `complaints:status` | อ่านสถานะอย่างย่อ | จำกัดตาม Partner |
| `complaints:read` | อ่านรายการและรายละเอียด | มีข้อมูลส่วนบุคคล |
| `complaints:update` | เปลี่ยนสถานะและเพิ่มข้อความตอบกลับ | ให้เฉพาะระบบที่ได้รับอนุญาต |
| `complaints:edoc` | อ่าน payload สำหรับ eDOC | มีข้อมูลส่วนบุคคล |
| `stats:summary` | อ่านสถิติสรุป | สรุปตามขอบเขตคีย์ |
| `stats:detailed` | อ่านสถิติแยกประเภท | สรุปตามขอบเขตคีย์ |
| `webhooks:manage` | ลงทะเบียน ดู และปิด Webhook | Webhook ผูกกับ API Key |
| `corruption:stats` | สถิติเรื่องทุจริต | Internal + All เท่านั้น |

ชุดสิทธิ์แนะนำ:

| รูปแบบ | Scope |
|---|---|
| ส่งเรื่องอย่างเดียว | `metadata:read`, `complaints:write` |
| ส่งและติดตาม | เพิ่ม `complaints:status` |
| เชื่อมต่อสองทาง | เพิ่ม `complaints:read`, `complaints:update`, `webhooks:manage` |
| ระบบรายงาน | `metadata:read`, `stats:summary`, `stats:detailed` |

## 6. ข้อกำหนด Request และ Response

### 6.1 Header มาตรฐาน

```http
Accept: application/json
Content-Type: application/json; charset=utf-8
X-API-Key: <secret>
Idempotency-Key: <unique-value>   # บังคับเฉพาะ POST /complaints
```

### 6.2 วันและเวลา

- ใช้ ISO 8601
- ระบบตอบเวลาเป็น UTC เช่น `2026-10-27T03:30:00Z`
- วันที่เกิดเหตุส่งแบบ `YYYY-MM-DD` หรือ ISO 8601 ที่ parse เป็นวันที่ได้
- Partner ควรจัดเก็บ UTC แล้วแปลงเป็น Asia/Bangkok เมื่อแสดงผล

### 6.3 Error envelope

```json
{
  "error": {
    "code": "invalid_api_key",
    "message": "Invalid or expired API key",
    "traceId": "0HNA..."
  }
}
```

เมื่อแจ้งปัญหาแก่ผู้ดูแล SRT ให้ส่ง `traceId`, เวลา UTC, Endpoint และ HTTP status โดยห้ามส่ง API Key

### 6.4 Rate limit

Response จะมี Header:

```http
X-RateLimit-Limit: 60
```

เมื่อเกินกำหนด:

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 60
```

Client ควรใช้ exponential backoff และ jitter ห้าม retry พร้อมกันจำนวนมาก

## 7. Idempotency

`POST /api/v1/complaints` บังคับใช้ `Idempotency-Key` เพื่อป้องกันการสร้างเรื่องซ้ำเมื่อ network timeout หรือ client retry

ข้อกำหนด:

- ความยาว 8–128 ตัวอักษร
- ต้องไม่ซ้ำสำหรับ business operation ใหม่
- ค่า UUID v4 เป็นรูปแบบที่แนะนำ
- คีย์เดียวกันและ payload เดิมจะได้ response เดิม
- คีย์เดียวกันแต่ payload ต่างกันจะได้ `409 idempotency_conflict`
- ระบบเก็บผลไว้ 24 ชั่วโมง

ตัวอย่าง:

```http
Idempotency-Key: 6f8c4de2-7d8b-4ae7-ae70-df33c8fe15ad
```

เมื่อเป็น response ที่ replay ระบบส่ง:

```http
Idempotent-Replayed: true
```

แนวทาง retry:

1. ถ้าไม่ได้รับ response เพราะ timeout ให้ส่ง payload เดิมพร้อม Idempotency-Key เดิม
2. ถ้าได้รับ `409 request_in_progress` ให้รอแล้วลองใหม่ด้วยคีย์เดิม
3. ถ้าได้รับ validation error ให้แก้ payload และใช้ Idempotency-Key ใหม่
4. อย่าสร้างคีย์ใหม่ทุกครั้งที่ timeout เพราะอาจสร้างข้อมูลซ้ำ

## 8. Metadata API

### 8.1 หมวดหมู่

```http
GET /api/v1/metadata/categories
Scope: metadata:read
```

```json
{
  "data": [
    {
      "id": 1,
      "name": "ความตรงต่อเวลา",
      "departmentName": "ฝ่ายการเดินรถ",
      "subCategories": [
        { "id": 1, "name": "รถล่าช้า" }
      ]
    }
  ]
}
```

Partner ควรดึงข้อมูลนี้แทนการ hard-code Category ID

### 8.2 สถานะ

```http
GET /api/v1/metadata/statuses
Scope: metadata:read
```

Status code ที่รองรับ:

| Code | ความหมาย |
|---|---|
| `Pending` | รอดำเนินการ |
| `InProgress` | กำลังดำเนินการ |
| `WaitingInfo` | รอข้อมูลเพิ่มเติม |
| `Forwarded` | ส่งต่อหน่วยงาน |
| `Resolved` | แก้ไขแล้ว |
| `Closed` | ปิดเรื่อง |
| `Rejected` | ปฏิเสธ |

### 8.3 Webhook events

```http
GET /api/v1/metadata/webhook-events
Scope: metadata:read
```

## 9. Complaint API

### 9.1 สร้างเรื่องร้องเรียน

```http
POST /api/v1/complaints
Scope: complaints:write
Idempotency-Key: required
```

Request:

```json
{
  "externalReference": "EDOC-2026-00001234",
  "reporterName": "สมชาย ใจดี",
  "reporterPhone": "0812345678",
  "reporterEmail": "somchai@example.com",
  "categoryId": 1,
  "subCategoryId": 1,
  "subjectStation": "สถานีกรุงเทพอภิวัฒน์",
  "incidentDate": "2026-10-26",
  "description": "รายละเอียดเรื่องร้องเรียนอย่างน้อย 10 ตัวอักษร",
  "latitude": 13.8032,
  "longitude": 100.5394
}
```

Validation สำคัญ:

| Field | ข้อกำหนด |
|---|---|
| `externalReference` | บังคับ, ไม่เกิน 100 ตัวอักษร, ต้องไม่ซ้ำภายใน Partner |
| `reporterName` | บังคับ, ไม่เกิน 200 ตัวอักษร |
| `reporterPhone` | บังคับ, ไม่เกิน 20 ตัวอักษร |
| `reporterEmail` | รูปแบบอีเมล, ไม่เกิน 200 ตัวอักษร |
| `categoryId` | ต้องเป็น Category ที่มีอยู่ |
| `subCategoryId` | ไม่บังคับ |
| `subjectStation` | ไม่เกิน 200 ตัวอักษร |
| `description` | 10–5,000 ตัวอักษร |
| `latitude` | -90 ถึง 90 |
| `longitude` | -180 ถึง 180 |

Response `201 Created`:

```json
{
  "referenceNumber": "SRT-COMPL-2569-0123",
  "externalReference": "EDOC-2026-00001234",
  "status": "Pending",
  "slaDeadline": "2026-10-30T10:00:00Z",
  "resourceUrl": "/api/v1/complaints/SRT-COMPL-2569-0123",
  "message": "รับเรื่องร้องเรียนเรียบร้อยแล้ว"
}
```

ตัวอย่าง cURL:

```bash
curl --request POST \
  --url "https://<hostname>/api/v1/complaints" \
  --header "X-API-Key: $SRT_API_KEY" \
  --header "Idempotency-Key: 6f8c4de2-7d8b-4ae7-ae70-df33c8fe15ad" \
  --header "Content-Type: application/json" \
  --data '{
    "externalReference":"EDOC-2026-00001234",
    "reporterName":"สมชาย ใจดี",
    "reporterPhone":"0812345678",
    "categoryId":1,
    "description":"รายละเอียดเรื่องร้องเรียนสำหรับทดสอบ"
  }'
```

### 9.2 รายการเรื่องสำหรับ Sync/Reconciliation

```http
GET /api/v1/complaints?updatedSince=2026-10-27T00:00:00Z&status=InProgress&page=1&pageSize=20
Scope: complaints:read
```

- `page` เริ่มที่ 1
- `pageSize` 1–100
- `updatedSince` ไม่บังคับ
- `status` ไม่บังคับ
- คีย์ `Own` จะเห็นเฉพาะ Partner ของตน

```json
{
  "data": [
    {
      "referenceNumber": "SRT-COMPL-2569-0123",
      "externalReference": "EDOC-2026-00001234",
      "status": "InProgress",
      "priority": "Normal",
      "category": "ความตรงต่อเวลา",
      "subjectStation": "สถานีกรุงเทพอภิวัฒน์",
      "createdAt": "2026-10-27T02:00:00Z",
      "updatedAt": "2026-10-27T03:00:00Z"
    }
  ],
  "pagination": { "page": 1, "pageSize": 20, "total": 1, "totalPages": 1 }
}
```

### 9.3 อ่านรายละเอียดด้วยเลข SRT

```http
GET /api/v1/complaints/{referenceNumber}
Scope: complaints:read
```

### 9.4 อ่านรายละเอียดด้วยเลขอ้างอิง Partner

```http
GET /api/v1/complaints/by-external-reference/{externalReference}
Scope: complaints:read
```

### 9.5 อ่านสถานะอย่างย่อ

```http
GET /api/v1/complaints/{referenceNumber}/status
Scope: complaints:status
```

```json
{
  "referenceNumber": "SRT-COMPL-2569-0123",
  "externalReference": "EDOC-2026-00001234",
  "status": "InProgress",
  "statusTh": "กำลังดำเนินการ",
  "updatedAt": "2026-10-27T03:00:00Z",
  "closedAt": null,
  "slaBreached": false
}
```

### 9.6 อัปเดตสถานะ

```http
PUT /api/v1/complaints/{referenceNumber}/status
Scope: complaints:update
```

```json
{
  "newStatus": "Resolved",
  "note": "ดำเนินการแก้ไขแล้ว"
}
```

`note` ยาวไม่เกิน 2,000 ตัวอักษร ระบบตรวจ allowlist ของสถานะและ ownership ทุกครั้ง

### 9.7 eDOC payload

```http
GET /api/v1/complaints/{referenceNumber}/edoc-payload
Scope: complaints:edoc
```

Endpoint นี้มีข้อมูลผู้ร้อง จึงต้องจัดเก็บและส่งต่อตามนโยบายคุ้มครองข้อมูลส่วนบุคคลของทั้งสองหน่วยงาน

## 10. Statistics API

```http
GET /api/v1/stats/summary
Scope: stats:summary

GET /api/v1/stats/detailed
Scope: stats:detailed
```

Response มี `dataScope`:

- `partner` หมายถึงสถิติเฉพาะข้อมูลของ Partner
- `all` หมายถึงข้อมูลรวมทั้งหมด อนุญาตเฉพาะ Internal key

Endpoint `/api/v1/stats/corruption` ต้องใช้ `corruption:stats` และ Data access แบบ `All`

## 11. Webhook

### 11.1 ลงทะเบียน

```http
POST /api/v1/webhooks
Scope: webhooks:manage
```

```json
{
  "name": "EDOC production receiver",
  "targetUrl": "https://partner.example.go.th/hooks/srt-complaints",
  "events": [
    "complaint.created",
    "complaint.status_changed",
    "complaint.closed"
  ]
}
```

ข้อกำหนด URL:

- ต้องเป็น HTTPS
- ห้ามมี username/password ใน URL
- ห้ามชี้ไป loopback, private, link-local, cloud metadata หรือ reserved network
- ระบบไม่ติดตาม HTTP redirect
- DNS และปลายทางจะถูกตรวจซ้ำก่อนส่งทุกครั้ง

Response `201` จะแสดง `secret` เพียงครั้งเดียว

### 11.2 ดูรายการและปิด Webhook

```http
GET /api/v1/webhooks
DELETE /api/v1/webhooks/{id}
```

การ DELETE เป็นการปิดใช้งานเพื่อเก็บ Audit trail และตอบ `204 No Content`

### 11.3 Event envelope

```json
{
  "id": "b06762ce-8cf0-4e57-b2b7-44a93ab9ae85",
  "type": "complaint.status_changed",
  "occurredAt": "2026-10-27T03:30:00Z",
  "data": {
    "referenceNumber": "SRT-COMPL-2569-0123",
    "externalReference": "EDOC-2026-00001234",
    "oldStatus": "Pending",
    "newStatus": "InProgress",
    "updatedAt": "2026-10-27T03:30:00Z"
  }
}
```

Header ที่ SRT ส่ง:

```http
X-SRT-Event: complaint.status_changed
X-SRT-Event-Id: b06762ce-8cf0-4e57-b2b7-44a93ab9ae85
X-SRT-Delivery-Id: 7cc63781-2663-4124-b2d3-b98cce23646a
X-SRT-Timestamp: 1793071800
X-SRT-Signature: v1=<hex-hmac-sha256>
```

### 11.4 การตรวจลายเซ็น

Canonical message คือข้อความต่อไปนี้ โดยใช้ raw HTTP request body เดิมทุก byte

```text
{X-SRT-Timestamp}.{rawRequestBody}
```

คำนวณ:

```text
expected = lowercase_hex(HMAC-SHA256(webhook_secret, canonical_message))
```

ตรวจสอบด้วย constant-time comparison และปฏิเสธ timestamp ที่ต่างจากเวลาปัจจุบันเกิน 5 นาที เพื่อป้องกัน replay attack

ตัวอย่าง C#:

```csharp
using System.Security.Cryptography;
using System.Text;

static bool VerifyWebhook(string timestamp, string rawBody, string signatureHeader, string secret)
{
    if (!long.TryParse(timestamp, out var unixTime)) return false;
    var sentAt = DateTimeOffset.FromUnixTimeSeconds(unixTime);
    if ((DateTimeOffset.UtcNow - sentAt).Duration() > TimeSpan.FromMinutes(5)) return false;

    var canonical = Encoding.UTF8.GetBytes($"{timestamp}.{rawBody}");
    var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), canonical);
    var suppliedHex = signatureHeader.StartsWith("v1=") ? signatureHeader[3..] : "";

    byte[] supplied;
    try { supplied = Convert.FromHexString(suppliedHex); }
    catch (FormatException) { return false; }

    return supplied.Length == expected.Length &&
           CryptographicOperations.FixedTimeEquals(supplied, expected);
}
```

ตัวอย่าง Node.js:

```javascript
import crypto from "node:crypto";

export function verifySrtWebhook({ timestamp, rawBody, signature, secret }) {
  const sentAtMs = Number(timestamp) * 1000;
  if (!Number.isFinite(sentAtMs) || Math.abs(Date.now() - sentAtMs) > 5 * 60 * 1000) {
    return false;
  }

  const expected = crypto
    .createHmac("sha256", secret)
    .update(`${timestamp}.`)
    .update(rawBody)
    .digest("hex");

  const supplied = signature.startsWith("v1=") ? signature.slice(3) : "";
  if (supplied.length !== expected.length) return false;
  return crypto.timingSafeEqual(Buffer.from(supplied, "hex"), Buffer.from(expected, "hex"));
}
```

ข้อสำคัญ: Framework บางชนิด parse JSON ก่อนถึง handler ต้องเปิดการเก็บ raw body มิฉะนั้น JSON ที่ serialize ใหม่อาจมีช่องว่างหรือลำดับ property ต่างจากต้นฉบับและทำให้ signature ไม่ตรง

### 11.5 การตอบและ Retry

- ตอบ `2xx` เมื่อรับและบันทึก Event สำเร็จ
- ควรตอบภายใน 10 วินาที แล้วประมวลผลหนักแบบ background
- ใช้ `X-SRT-Event-Id` หรือ `X-SRT-Delivery-Id` ป้องกันประมวลผลซ้ำ
- SRT timeout ที่ 15 วินาที
- กรณีไม่ใช่ `2xx` จะ retry ประมาณ 1 นาที, 5 นาที, 30 นาที และ 2 ชั่วโมง
- ระบบส่งสูงสุด 5 ครั้งต่อ Delivery

Webhook เป็น at-least-once delivery จึงอาจได้รับ Event เดิมมากกว่าหนึ่งครั้ง

## 12. ตัวอย่าง Client

### 12.1 C# HttpClient

```csharp
using System.Net.Http.Json;

var client = new HttpClient { BaseAddress = new Uri("https://<hostname>/api/v1/") };
client.DefaultRequestHeaders.Add("X-API-Key", Environment.GetEnvironmentVariable("SRT_API_KEY"));

using var request = new HttpRequestMessage(HttpMethod.Post, "complaints");
request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
request.Content = JsonContent.Create(new
{
    externalReference = "EDOC-2026-00001234",
    reporterName = "สมชาย ใจดี",
    reporterPhone = "0812345678",
    categoryId = 1,
    description = "รายละเอียดเรื่องร้องเรียนสำหรับทดสอบ"
});

using var response = await client.SendAsync(request);
var body = await response.Content.ReadAsStringAsync();
if (!response.IsSuccessStatusCode)
    throw new InvalidOperationException($"SRT API {(int)response.StatusCode}: {body}");
```

### 12.2 Node.js fetch

```javascript
const response = await fetch("https://<hostname>/api/v1/complaints", {
  method: "POST",
  headers: {
    "content-type": "application/json",
    "x-api-key": process.env.SRT_API_KEY,
    "idempotency-key": crypto.randomUUID()
  },
  body: JSON.stringify({
    externalReference: "EDOC-2026-00001234",
    reporterName: "สมชาย ใจดี",
    reporterPhone: "0812345678",
    categoryId: 1,
    description: "รายละเอียดเรื่องร้องเรียนสำหรับทดสอบ"
  })
});

const body = await response.json();
if (!response.ok) throw new Error(JSON.stringify(body));
```

### 12.3 Postman environment

```json
{
  "name": "SRT Complaint Sandbox",
  "values": [
    { "key": "baseUrl", "value": "https://<hostname>/api/v1", "enabled": true },
    { "key": "apiKey", "value": "", "type": "secret", "enabled": true }
  ]
}
```

กำหนด Collection header:

```text
X-API-Key: {{apiKey}}
Accept: application/json
```

Pre-request script สำหรับคำขอสร้างเรื่อง:

```javascript
pm.request.headers.upsert({ key: "Idempotency-Key", value: pm.variables.replaceIn("{{$guid}}") });
```

## 13. HTTP status และแนวทางจัดการ

| Status | ความหมาย | การจัดการของ Client |
|---:|---|---|
| 200 | สำเร็จ | ประมวลผล response |
| 201 | สร้างสำเร็จ | บันทึก `referenceNumber` |
| 204 | ปิด Webhook สำเร็จ | ไม่ต้อง parse body |
| 400 | รูปแบบ request/header ผิด | แก้ request ไม่ควร retry อัตโนมัติ |
| 401 | API Key ผิด/หมดอายุ | หยุด retry และติดต่อผู้ดูแล |
| 403 | Scope, data access หรือ IP ไม่ผ่าน | หยุด retry และตรวจ config |
| 404 | ไม่พบหรือไม่มี ownership | ตรวจ reference และ Partner |
| 409 | ข้อมูลซ้ำหรือ idempotency conflict | ตรวจ `externalReference`/key |
| 422 | ค่า business validation ผิด | แก้ข้อมูลก่อนส่งใหม่ |
| 429 | เกิน Rate limit | รอตาม `Retry-After` พร้อม jitter |
| 500 | ระบบขัดข้อง | retry แบบ exponential backoff ด้วย Idempotency-Key เดิม |

## 14. การเก็บ Secret และ Rotation

- เก็บ API Key และ Webhook secret ใน Secret Manager
- จำกัดสิทธิ์อ่าน secret เฉพาะ workload ที่ต้องใช้
- ไม่เขียน secret ลง application log
- ไม่ฝังใน mobile app หรือ frontend JavaScript
- Rotate ทันทีหากสงสัยว่ารั่วไหล
- การ Rotate สร้างคีย์ใหม่และให้คีย์เดิมใช้ต่อได้ 7 วัน
- Partner ต้อง deploy คีย์ใหม่และยืนยัน traffic ก่อนครบ grace period
- เมื่อยกเลิกคีย์ ผลมีผลทันที

## 15. Go-live checklist

### Partner

- [ ] เก็บ API Key ใน Secret Manager
- [ ] ใช้ Production Base URL และ Production key ถูกคู่กัน
- [ ] ตั้งค่า egress IP ตรงกับ whitelist
- [ ] ดึง Category จาก Metadata API
- [ ] ใช้ UUID เป็น Idempotency-Key และเก็บคู่กับ transaction
- [ ] บันทึก `referenceNumber` และ `externalReference`
- [ ] ตรวจ Webhook signature จาก raw body
- [ ] ป้องกัน Event ซ้ำด้วย Event ID
- [ ] รองรับ Retry/429/5xx
- [ ] Mask PII และ Secret ใน log
- [ ] มี alert เมื่อ Webhook ล้มเหลวหรือ API 401/403 เพิ่มขึ้น

### ผู้ดูแล SRT

- [ ] Partner/contact/purpose ครบ
- [ ] แยก Sandbox และ Production key
- [ ] Scope เป็น least privilege
- [ ] External key ใช้ Data access = Own
- [ ] กำหนดวันหมดอายุ
- [ ] จำกัด IP สำหรับ Production
- [ ] Rate limit เหมาะสม
- [ ] Webhook ใช้ HTTPS public endpoint
- [ ] ทดสอบ create, retry, ownership และ webhook signature
- [ ] นัดหมายวัน Rotate และช่องทาง Incident

## 16. Security test ที่ Partner ควรทำ

1. ไม่ส่ง API Key ต้องได้ 401
2. ใช้คีย์ผิดต้องได้ 401
3. เรียก Endpoint ที่ไม่มี Scope ต้องได้ 403
4. ใช้ IP นอก whitelist ต้องได้ 403
5. อ่านเลขอ้างอิงของ Partner อื่นต้องได้ 404
6. ส่ง Idempotency-Key เดิมกับ payload เดิมต้องได้ response เดิม
7. ส่ง Idempotency-Key เดิมกับ payloadต่างกันต้องได้ 409
8. Webhook signature ผิดต้องถูกปฏิเสธ
9. Webhook timestamp เกิน 5 นาทีต้องถูกปฏิเสธ
10. Webhook Event ID ซ้ำต้องไม่ประมวลผล business ซ้ำ

## 17. การแจ้ง Incident

เมื่อแจ้งปัญหา ให้ระบุ:

- Partner code และ Environment
- เวลาเกิดเหตุเป็น UTC หรือระบุ timezone
- Method และ Endpoint
- HTTP status และ error code
- `traceId`, `X-SRT-Event-Id` หรือ `X-SRT-Delivery-Id`
- ผลกระทบและจำนวนรายการ

ห้ามแนบ API Key, Webhook secret, ข้อมูลบัตรประชาชน หรือข้อมูลผู้ร้องที่ไม่จำเป็น
