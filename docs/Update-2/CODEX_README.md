# วิธีใช้งานชุดไฟล์นี้กับ Codex CLI

แตก ZIP แล้วเก็บ `document.md` พร้อมโฟลเดอร์ `pages/` และ `images/` ไว้ด้วยกัน เพราะ Markdown ใช้ relative path สำหรับรูปภาพ

ตัวอย่าง prompt:

```text
อ่าน document.md และตรวจสอบภาพทั้งหมดในโฟลเดอร์ pages/ และ images/
สรุป requirement ของระบบใหม่ โดยตรวจสอบภาพ pages/page-1.png ถึง pages/page-สุดท้ายด้วย
ห้ามเดารายละเอียดที่อ่านจากภาพไม่ชัด
```

`pages/page-*.png` เหมาะสำหรับอ่านภาพรวมและ layout ของแต่ละหน้า ส่วน `images/image-*.jpg/png` เหมาะสำหรับวิเคราะห์รูปที่ฝังอยู่แบบรายรูป
