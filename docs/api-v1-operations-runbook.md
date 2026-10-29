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
```

`BootstrapAdmin:Password` ใช้เฉพาะตอนฐานข้อมูลยังไม่มี SuperAdmin หลังสร้างสำเร็จให้ลบค่าออกจาก Environment/Secret Store

Webhook secret ใหม่ใช้ ASP.NET Core Data Protection จึงต้องใช้ shared key ring และป้องกัน key ring ด้วยสิทธิ์ระบบปฏิบัติการหรือ external key management เมื่อรันหลาย instance

## Database deployment

Migration:

```text
20261027031230_AddPartnerApiV1Foundation
```

Migration จะ:

1. สร้าง Partner `LEGACY`
2. ย้าย API Key เดิมไปยัง Partner นี้
3. กำหนดคีย์เดิมเป็น Production
4. กำหนด Internal key เป็น `All` และ External key เป็น `Own`
5. ผูก Complaint เดิมที่ Channel เป็น `API` กับ Partner `LEGACY`
6. สร้าง Idempotency, Webhook Outbox และ Delivery identity

ก่อน deploy:

```powershell
dotnet ef migrations has-pending-model-changes --project SRT.Complaint/SRT.Complaint.csproj --startup-project SRT.Complaint/SRT.Complaint.csproj --context AppDbContext
dotnet ef migrations script --idempotent --project SRT.Complaint/SRT.Complaint.csproj --startup-project SRT.Complaint/SRT.Complaint.csproj --context AppDbContext --output db/production-migrations.sql
```

สำรองฐานข้อมูลและตรวจ API Key เดิมก่อน deploy Production

## Creating a Partner and key

1. เปิด `Admin > API Keys`
2. เพิ่ม Partner โดยใช้ Code ที่ถาวร
3. สร้าง Sandbox key ก่อน
4. เลือก preset แล้วลด Scope ที่ไม่จำเป็น
5. External key ต้องเป็น `Own`
6. กำหนดวันหมดอายุและ IP whitelist
7. คัดลอก Raw Key จากหน้าผลลัพธ์เพียงครั้งเดียว
8. ส่งผ่านช่องทาง Secret Exchange ที่องค์กรอนุมัติ
9. บันทึกผู้รับ วันที่ และ Ticket อ้างอิงในระบบงานภายนอก

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
- Production ควรวาง distributed rate limiter ที่ API gateway หากรันมากกว่าหนึ่ง instance เพราะ application limiter ปัจจุบันนับต่อ instance
- จำกัด trusted proxy ก่อนเปิดใช้ Forwarded Headers มิฉะนั้น IP whitelist อาจใช้ IP ของ proxy หรือถูก spoof ได้

## Rollback

Rollback application ก่อน rollback migration หาก API v1 มี traffic แล้ว ควรรักษาตาราง Outbox และ Idempotency ไว้จนตรวจว่าไม่มีรายการค้าง การ rollback migration จะลบตารางใหม่และข้อมูล audit ที่เกี่ยวข้อง จึงต้องมี backup และ change approval ก่อนเสมอ
