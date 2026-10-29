# eDOC API Security Review

ขอบเขต: outbound integration จาก SRT Complaint ไป RW Saraban/eDOC ได้แก่ service-account login, create, upload, inquiry, tracking, Basket selection, local durable state และหน้า Case Detail

สถานะโดยรวม: **พร้อมสำหรับ DEV/UAT หลังตั้งค่า secret และ migration; ยังไม่ควรเปิด Production จนกว่าจะหมุน credentials และยืนยัน Production endpoint/contract กับทีม eDOC**

## Findings

### EDOC-SEC-001 — Credentials ปรากฏในเอกสารอ้างอิง

- Severity: High
- OWASP API Top 10: API2:2023 Broken Authentication
- CWE: CWE-798 Use of Hard-coded Credentials
- Evidence/location: ไฟล์ Word/Postman ที่ผู้ให้บริการส่งมา (ไม่บันทึกค่าจริงในรายงานนี้)
- Risk: ผู้ที่เข้าถึงไฟล์สามารถใช้บัญชี API โดยไม่ผ่านสิทธิ์ของแอป และ credential อาจหลุดผ่าน Git/backup/chat
- Remediation: ขอทีม eDOC rotate password/token ที่ปรากฏในเอกสาร, ใช้ secret manager/environment variables, แยก DEV/Production credentials, จำกัด source IP และสิทธิ์เฉพาะ Basket ที่จำเป็น
- Implemented: ไม่คัดลอก secret ลง source/config; เพิ่มชื่อไฟล์อ้างอิงลง `.gitignore`; client ไม่ log credentials/token
- Residual status: **Open until provider confirms rotation**

### EDOC-SEC-002 — Create API ไม่มี idempotency contract ที่ชัดเจน

- Severity: High
- OWASP API Top 10: API6:2023 Unrestricted Access to Sensitive Business Flows
- CWE: CWE-837 Improper Enforcement of a Single, Unique Action
- Location: `EdocIntegrationService.ProcessLinkAsync`, unique indexes ของ `EdocDocumentLinks`
- Risk: timeout หลัง eDOC สร้างเอกสารสำเร็จแต่ก่อน SRT รับ response อาจทำให้ retry แล้วเกิดเอกสารซ้ำ
- Remediation/implemented: one complaint/one WID, durable Pending/Sending/Sent state, atomic claim, ห้าม auto-retry เมื่อ outcome ไม่ชัดเจน, เปลี่ยนเป็น `NeedsReview`, ใช้ Inquiry ตรวจ WID ก่อน manual retry และจำกัด retry ให้ SuperAdmin
- Residual status: **Mitigated**; ขอผู้ให้บริการยืนยัน uniqueness ของ WID หรือเพิ่ม idempotency key ฝั่ง eDOC

### EDOC-SEC-003 — Base URL อาจกลายเป็น SSRF/configuration abuse

- Severity: Medium
- OWASP API Top 10: API7:2023 Server Side Request Forgery, API8:2023 Security Misconfiguration
- CWE: CWE-918 Server-Side Request Forgery
- Location: `EdocOptionsValidator`, `EdocHttpMessageHandlerFactory`
- Risk: ผู้ที่แก้ configuration อาจชี้ service account request ไป host อื่นเพื่อขโมย credentials/token หรือเข้าถึง private network
- Remediation/implemented: HTTPS-only, explicit hostname allowlist, DNS resolve แล้ว block private/reserved addresses โดย default, connect ไป IP ที่ตรวจแล้ว, host pinning, ปิด redirects/proxy/cookies และจำกัด timeout
- Residual status: **Mitigated**; การเปลี่ยน configuration/secret store ต้องถูกจำกัดสิทธิ์และ audit

### EDOC-SEC-004 — Unbounded file/response resource consumption

- Severity: Medium
- OWASP API Top 10: API4:2023 Unrestricted Resource Consumption
- CWE: CWE-400 Uncontrolled Resource Consumption
- Location: `EdocApiClient.ReadBodyAsync`, `EdocIntegrationService.UploadPendingAttachmentsAsync`
- Risk: response หรือไฟล์ขนาดใหญ่อาจใช้ memory มากและทำให้ worker/application ล่ม
- Remediation/implemented: response cap 1 MB (configurable สูงสุด 5 MB), attachment cap 10 MB (configurableสูงสุด 20 MB), JSON max depth 32, จำนวนงานต่อ background batch ถูกจำกัด
- Residual status: **Mitigated**; ยืนยัน provider file limit ใน UAT

### EDOC-SEC-005 — Token และข้อมูลเอกสารรั่วผ่าน log/UI

- Severity: Medium
- OWASP API Top 10: API2:2023 Broken Authentication, API3:2023 Broken Object Property Level Authorization
- CWE: CWE-532 Insertion of Sensitive Information into Log File
- Location: `EdocApiClient`, `EdocIntegrationService`, Case Detail handlers
- Risk: access token, password, Base64 attachment หรือข้อมูลผู้ร้องเรียนอาจถูกบันทึกหรือแสดงให้ผู้ไม่มีสิทธิ์
- Remediation/implemented: ไม่ log request/response body หรือ token, error ที่เก็บมีเฉพาะ generic message/HTTP/remote code, page อยู่หลัง StaffOnly และ mutation ตรวจ owner/SuperAdmin, service-account credentials ไม่ถูกส่งไป browser
- Residual status: **Mitigated**; ควรตรวจ centralized logging/redaction ระหว่าง UAT

### EDOC-SEC-006 — Shared service account มี blast radius กว้าง

- Severity: Medium
- OWASP API Top 10: API5:2023 Broken Function Level Authorization
- CWE: CWE-250 Execution with Unnecessary Privileges
- Location: eDOC service-account provisioning (provider-side)
- Risk: หาก credential หลุด ผู้โจมตีอาจสร้างเอกสารในทุก Basket ที่บัญชีเข้าถึงได้
- Remediation: ขอ least-privilege account, จำกัดเฉพาะ create/upload/inquiry/tracking และ Basket ที่อนุญาต, source-IP allowlist, rotate ตามรอบ, alert เมื่อ volume ผิดปกติ
- Implemented in SRT: Basket allowlist, category mapping, authorization ที่ปุ่ม, audit trail
- Residual status: **Open/provider action required**

## Positive controls verified

- ใช้ authorization ของระบบ SRT แทนการส่ง credential eDOC ไปยังผู้ใช้
- login token ถูก cache ใน memory และ refresh เมื่อ 401 โดยไม่เก็บลงฐานข้อมูล
- create success ถูก persist ก่อน upload attachments ลดโอกาสสร้าง document ซ้ำเมื่อ upload ล้มเหลว
- stale `Sending` ถูกเปลี่ยนเป็น `NeedsReview` ไม่ถูก retry โดยอัตโนมัติ
- uniqueness constraints ที่ ComplaintId และ WID
- Basket ที่ client ส่งต้องอยู่ใน server-side allowlist
- outbound redirect/proxy ถูกปิด และ endpoint ต้องเป็น HTTPS allowlisted host
- migration ไม่แก้หรือลบข้อมูลเดิม มีเฉพาะตาราง eDOC ใหม่

## Production gates

1. eDOC ยืนยันว่า credentials ในเอกสารตัวอย่างถูก rotate แล้ว
2. ยืนยัน Production Base URL, TLS certificate chain และ IP allowlist
3. ยืนยัน WID uniqueness/idempotency behavior และ response schema จริง
4. ผ่าน UAT แบบสอง Basket อย่างน้อย และทดสอบ timeout/reconciliation
5. ตั้ง monitoring สำหรับ error rate, `NeedsReview`, `Failed`, authentication failures และปริมาณ create ผิดปกติ
6. ทดสอบ restore/rollback ฐานข้อมูลและ secret rotation
7. ตรวจ log จาก production-like environment ว่าไม่มี token/password/body/Base64

