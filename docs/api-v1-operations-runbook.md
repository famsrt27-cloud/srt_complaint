# API v1 Operations Runbook

เอกสารสำหรับผู้ดูแล SRT ไม่ควรส่งให้ Partner ภายนอกทั้งฉบับ

## Configuration

### Reverse proxy และ IP จริงของ Partner

ถ้าวางระบบหลัง reverse proxy/load balancer ให้ตั้ง `ReverseProxy:KnownProxies` เป็น IP ของ proxy ที่เชื่อถือได้ เช่น environment variable `ReverseProxy__KnownProxies__0=10.10.0.5` ระบบจะอ่าน `X-Forwarded-For` และ `X-Forwarded-Proto` จาก proxy ที่ระบุเท่านั้น ห้ามตั้งเป็น proxy ที่ผู้ใช้อินเทอร์เน็ตเข้าถึงและปลอม header ได้ เพราะ IP allowlist และ rate limit ใช้ client IP หลังผ่านขั้นตอนนี้

ตั้งค่าผ่าน environment variable หรือ Secret Store ห้าม commit secret ลง repository

กำหนด `ApiManagement__Environment=Sandbox` บน Sandbox และ `ApiManagement__Environment=Production` บน Production ระบบนอก Development จะไม่เริ่มทำงานหากไม่มีค่านี้ และจะรับ/สร้างเฉพาะ API key ที่ environment ตรงกับ instance

```text
ConnectionStrings__DefaultConnection=<sql-server-connection>
Encryption__Key=<base64-32-byte-key>
BootstrapAdmin__Password=<one-time-initial-password>
ApiManagement__AllowedWebhookHosts__0=<explicit-private-host-if-approved>
DistributedRuntime__Mode=MultiInstance
DistributedRuntime__RateLimitProvider=Redis
DistributedRuntime__RedisConnection=<redis-connection-secret>
DistributedRuntime__RedisInstanceName=SRT:Production:
DistributedRuntime__EmergencyReadLimitPerMin=10
DataProtection__ApplicationName=SRT.Complaint.Production
DataProtection__CertificateThumbprint=<certificate-thumbprint>
DataProtection__CertificateStoreLocation=LocalMachine
```

`BootstrapAdmin:Password` ใช้เฉพาะตอนฐานข้อมูลยังไม่มี SuperAdmin หลังสร้างสำเร็จให้ลบค่าออกจาก Environment/Secret Store

Webhook secret ใหม่ใช้ ASP.NET Core Data Protection โดย key ring เก็บในตาราง `DataProtectionKeys` ของฐานข้อมูลร่วม ทุก instance ต้องใช้ `DataProtection__ApplicationName` เดียวกัน Production บังคับ certificate จาก `LocalMachine\My` หรือ `CurrentUser\My` เพื่อเข้ารหัส key at rest และ identity ของ application pool/container ต้องมีสิทธิ์อ่าน private key

Certificate ต้องอยู่ในช่วงอายุใช้งานและมี private key ระบบจะ fail startup หากหา certificate ไม่พบ/หมดอายุ เมื่อ rotate certificate ต้องคง certificate เก่าพร้อม private key ไว้จนกว่าจะยืนยันว่า key ring เก่าทั้งหมดไม่ถูกใช้งาน มิฉะนั้น cookie หรือ secret ที่เข้ารหัสด้วย key เก่าอาจถอดไม่ได้

หากใช้ API gateway เป็นตัวคุม quota ให้ตั้ง `DistributedRuntime__RateLimitProvider=Gateway` แต่ยังต้องกำหนด Redis connection ในโหมด MultiInstance เพื่อแชร์ Session ระบบจะเก็บ local application limiter เป็น defense-in-depth เท่านั้น ต้องตั้ง gateway ให้ใช้ API key identity เดียวกันและห้ามนับแยกตาม backend instance

Fail policy ของ Redis limiter: คำสั่งเขียนตอบ `503 rate_limit_unavailable` พร้อม `Retry-After: 5`; safe read ใช้ local emergency limit ตาม `EmergencyReadLimitPerMin` ชั่วคราว จึงต้อง alert เมื่อพบ error code นี้และแก้ Redis ก่อนเพิ่ม traffic

## Database deployment

Migration:

```text
20261027031230_AddPartnerApiV1Foundation
20261103075305_AddProductionHardening
```

Migration จะ:

1. สร้าง Partner `LEGACY`
2. ย้าย API Key เดิมไปยัง Partner นี้
3. กำหนดคีย์เดิมเป็น Production
4. กำหนด Internal key เป็น `All` และ External key เป็น `Own`
5. ผูก Complaint เดิมที่ Channel เป็น `API` กับ Partner `LEGACY`
6. สร้าง Idempotency, Webhook Outbox และ Delivery identity
7. เพิ่ม SQL Server `rowversion` ที่ `Complaints.RowVersion` สำหรับ `ETag`/`If-Match`
8. สร้างตาราง `DataProtectionKeys` สำหรับ shared key ring

ก่อน deploy:

```powershell
dotnet ef migrations has-pending-model-changes --project SRT.Complaint/SRT.Complaint.csproj --startup-project SRT.Complaint/SRT.Complaint.csproj --context AppDbContext
dotnet ef migrations script --idempotent --project SRT.Complaint/SRT.Complaint.csproj --startup-project SRT.Complaint/SRT.Complaint.csproj --context AppDbContext --output db/production-migrations.sql
```

สำรองฐานข้อมูลและตรวจ API Key เดิมก่อน deploy Production

ต้อง apply migration ก่อนเริ่ม application รุ่นนี้ มิฉะนั้น Data Protection provider จะอ่าน/สร้าง key ไม่ได้ ห้าม rollback migration ขณะที่ application รุ่นใหม่ยังทำงานอยู่ เพราะจะลบ key ring และคอลัมน์ concurrency token

หากระบบเดิมมี Data Protection key ring อยู่ใน filesystem ให้เก็บ key เดิมไว้และวางแผน import/rotation ก่อนเปลี่ยน provider การเริ่มด้วยตารางว่างจะทำให้ ciphertext เดิม (เช่น webhook secret ที่เข้ารหัสแล้ว) ถอดไม่ได้ ให้ทดสอบ decrypt webhook secret เดิมอย่างน้อยหนึ่งรายการจากทุก instance ก่อนตัด traffic

## Creating a Partner and key

1. เปิด `Admin > API Keys`
2. เพิ่ม Partner โดยใช้ Code ที่ถาวร
3. สร้าง Sandbox key ก่อน
4. เลือก preset แล้วลด Scope ที่ไม่จำเป็น
5. External key ต้องเป็น `Own`
6. ห้ามให้ `complaints:update` แก่ External key; endpoint เปลี่ยนสถานะสงวนให้ Internal integration ที่ผ่านการอนุมัติ
7. กำหนดวันหมดอายุและ IP whitelist
8. คัดลอก Raw Key จากหน้าผลลัพธ์เพียงครั้งเดียว
9. ส่งผ่านช่องทาง Secret Exchange ที่องค์กรอนุมัติ
10. บันทึกผู้รับ วันที่ และ Ticket อ้างอิงในระบบงานภายนอก

## Production verification สำหรับ 3 controls

ก่อนเปิด traffic จริงให้ทดสอบผ่าน load balancer อย่างน้อยสอง application instances:

1. ส่ง request ด้วย API key เดียวสลับ instance จนครบ quota และยืนยันว่าคำขอถัดไปได้ `429` จาก quota รวม ไม่ใช่ quota ต่อ instance
2. ปิด Redis ชั่วคราวใน controlled test แล้วยืนยันว่า write ได้ `503 rate_limit_unavailable`, read ถูก emergency limit และมี alert
3. Login ที่ instance A แล้วใช้งาน session ที่ instance B ได้โดยไม่หลุด
4. สร้าง webhook ที่ instance A แล้วให้ worker instance B ถอด secret และลงลายเซ็นได้
5. ตรวจ certificate thumbprint, private-key ACL และ `ApplicationName` ให้ตรงกันทุก instance
6. อ่าน complaint เพื่อรับ `ETag`, อัปเดตด้วย `If-Match` แล้วตรวจว่า version ใหม่ถูกส่งกลับ
7. ส่ง `If-Match` เดิมซ้ำแล้วต้องได้ `412`; ไม่ส่ง header ต้องได้ `428`
8. ทดลองข้าม workflow (เช่น `Received` ไป `Closed`) ต้องได้ `409 invalid_status_transition`
9. ทดลอง External key เรียก status update ต้องได้ `403 external_status_update_disabled`

## Rotation

1. กด Rotate Key
2. ส่งคีย์ใหม่ให้ Partner ผ่าน Secret Exchange
3. คีย์เดิมหมดอายุภายใน 7 วัน
4. ตรวจหน้า Usage ให้เห็น traffic จากคีย์ใหม่
5. หาก Partner เปลี่ยนเสร็จก่อนกำหนด ให้ Revoke คีย์เดิม
6. หากเกิดการรั่วไหล ให้ Revoke ทันทีโดยไม่รอ grace period

## Webhook monitoring

ตรวจสอบ:

- HTTP status ล่าสุด
- Delivery ที่ retry ครบ 5 ครั้ง
- Outbox ที่ `ProcessedAt` เป็น null นานผิดปกติ
- Response time และ DNS/SSRF validation failure
- Webhook ของ key ที่หมดอายุหรือถูก revoke

Retry worker ทำงานทุก 30 วินาที และใช้ durable Outbox ในฐานข้อมูล

## Security notes

- OpenAPI JSON เปิดที่ `/openapi/v1.json` และไม่มี secret
- Raw API Key ไม่ถูกเก็บในฐานข้อมูล
- Raw Key ชั่วคราวในหน้า Admin เก็บฝั่ง Session และถูกลบหลังอ่าน
- Request log ไม่เก็บ Query string เพื่อลดความเสี่ยง PII
- Webhook redirect ถูกปิดและตรวจ DNS/ปลายทางทุกครั้ง
- Production หลาย instance ต้องใช้ Redis limiter หรือ API gateway quota และ shared Redis session; startup validation จะปฏิเสธ config แบบ Local
- Shared Data Protection key ring ต้องอยู่ในฐานข้อมูลร่วมและเข้ารหัสด้วย certificate; สำรองตาราง `DataProtectionKeys` พร้อมฐานข้อมูล
- จำกัด trusted proxy ก่อนเปิดใช้ Forwarded Headers มิฉะนั้น IP whitelist อาจใช้ IP ของ proxy หรือถูก spoof ได้

## Rollback

Rollback application ก่อน rollback migration หาก API v1 มี traffic แล้ว ควรรักษาตาราง Outbox และ Idempotency ไว้จนตรวจว่าไม่มีรายการค้าง การ rollback migration จะลบตารางใหม่และข้อมูล audit ที่เกี่ยวข้อง จึงต้องมี backup และ change approval ก่อนเสมอ
