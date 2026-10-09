# Traffy Fondue Exchange API

## รายละเอียดการใช้งาน API เพื่อแลกเปลี่ยนข้อมูล Traffy Fondue กับหน่วยงานอื่น ๆ

หมายเหตุ :

- หากหน่วยงานของท่านต้องการสมัครใช้งาน Exchange API กรุณากรอกแบบฟอร์มที่นี่ [https://forms.gle/bvFEhjPHSmU1x7wP7](https://forms.gle/bvFEhjPHSmU1x7wP7)  
- จำกัดจำนวนครั้งการใช้งานต่อเดือน (credit\_balance) ตามที่ได้รับแจ้งทาง e-mail และเริ่มนับใหม่วันที่ 1 ของทุกเดือน  
- API เกือบทุกตัว ต้องใช้ token ด้วย ซึ่งสามารถขอ token ได้ที่ API get-auth โดยต้องใช้ user & pass ตามที่ได้แจ้งไปทาง e-mail

## Version history

**2023-07-13**

- Initial release

**2024-02-07**

- แก้ bug API update-issue ส่งตัวแปร photo แล้ว error  
- ปรับปรุง API update-issue รองรับ input parameter extra\_detail

**2024-02-08**

- แก้ bug API update-issue ให้อัปเดตทุกเรื่องแจ้งที่เป็น client\_ticket\_id เดียวกัน

**2024-03-22**

- ปรับปรุง API update-issue รองรับการส่งไฟล์ชนิดต่างๆ (เช่น pdf, doc, xls)

# **สารบัญ**

[การยืนยันตัวตน](#การยืนยันตัวตน)

- [API get-auth](#api-get-auth-\(ขอ-jwt\))

[การขอข้อมูลจาก Traffy Fondue](#การขอข้อมูลจาก-traffy-fondue)

- [API get-issues](#api-get-issues-\(ขอ-list-รายการแจ้ง-ที่หน่วยงานได้รับ\))  
- [API get-issue](#api-get-issue-\(ขอรายละเอียดการแจ้งของ-ticket_id-ที่ระบุ\))  
- [API download-issues](#api-download-issues-\(ดาวน์โหลดไฟล์-csv-ของ-list-รายการแจ้ง-ที่หน่วยงานได้รับ\))  
- [API search-org](#api-search-org-\(ค้นหาหน่วยงานจากชื่อ\))  
- [API get-org-list](#api-get-org-list-\(ขอ-list-รายชื่อหน่วยงานที่อยู่ใต้บังคับบัญชา\))  
- [API get-type-list](#api-get-type-list-\(ขอ-list-รายชื่อประเภทปัญหาที่หน่วยงานรับผิดชอบ\))  
- [API get-status-list](#api-get-status-list-\(ขอ-list-รายชื่อสถานะของการแจ้งที่สามารถใช้ได้\))

[การส่งข้อมูลให้ Traffy Fondue](#การส่งข้อมูลให้-traffy-fondue)

- [API new-issue](#api-new-issue-\(เพิ่มเรื่องแจ้ง\))  
- [API update-issue](#api-update-issue-\(อัปเดตสถานะ\))

[Traffy Fondue ส่งข้อมูลไปให้ระบบอื่น ๆ แบบ real-time](#traffy-fondue-ส่งข้อมูลไปให้ระบบอื่น-ๆ-แบบ-real-time)

- [เมื่อมีเรื่องแจ้งเข้ามาใหม่](#เมื่อมีเรื่องแจ้งเข้ามาใหม่)  
- [เมื่อมีเจ้าหน้าที่มาอัปเดตสถานะ](#เมื่อมีการอัปเดตสถานะ)

# 

# **การยืนยันตัวตน** {#การยืนยันตัวตน}

## API get-auth (ขอ JWT) {#api-get-auth-(ขอ-jwt)}

ใช้ขอ JWT (JSON Web Token) เพื่อนำไปใช้กับ API ต่าง ๆ  
หมายเหตุ :

- ห้ามเผยแพร่ user และ pass สู่สาธารณะเด็ดขาด เช่น ห้ามฝังไว้ในเว็บไซต์หรือ mobile app เพราะอาจถูก inspect หรือ sniffing ดูได้ ให้ทำการขอ token โดยใช้ server เท่านั้น  
- หาก credit\_balance ลดลงเร็วผิดปรกติ ให้รีบติดต่อทีมงาน Traffy ทันที ที่ LINE @fonduehelp [https://lin.ee/pRPBNq2](https://lin.ee/pRPBNq2) เพื่อตรวจสอบและเปลี่ยนรหัสใหม่  
- JWT Token ถูกตั้งเวลาให้หมดอายุ โปรดตรวจสอบจาก expire\_timestamp ที่ได้รับมาพร้อมกับ token (หรือจะถอดรหัส expire\_timestamp ออกมาจาก token ก็ได้ ด้วยเว็บ [https://jwt.io](https://jwt.io) )  
- การใช้งาน API เกือบทุกตัว ต้องใช้ JWT token ด้วย ซึ่งสามารถขอ token ได้ที่ API get-auth นี้ โดยต้องใช้ user & pass ตามที่ได้แจ้งไปใน e-mail  
- หลังจากได้รับ token แล้ว สามารถใช้งาน API ต่าง ๆ ได้โดยส่งมาที่ Header Authorization Bearer ตามตัวอย่างด้านล่างนี้  
  ตัวอย่างการเรียกใช้ API get-issues ด้วย Postman  
  ![][image1]  
  ตัวอย่างการเรียกใช้ API get-issues ด้วย cURL

| curl \--location 'https://publicapi.traffy.in.th/exchange-api/get-issues/v1' \\ \--header 'Authorization: Bearer abcdefghijklmnopqrstuvwxyz1234567890' |
| :---- |

  ตัวอย่างการเรียกใช้ API get-issues ด้วย PHP

| $url \= 'https://publicapi.traffy.in.th/exchange-api/get-issues/v1'; $pre\_context \= \[      'http' \=\> \[          'method' \=\> "GET",          'header' \=\> "Authorization: Bearer abcdefghijklmnopqrstuvwxyz1234567890\\r\\n".              "Content-Type: application/json; charset=UTF-8\\r\\n";     \] \]; $api\_result \= file\_get\_contents($url, false, stream\_context\_create($pre\_context)); |
| :---- |

**URL:** https://publicapi.traffy.in.th/exchange-api/get-auth/v1

**Input parameters**

| Method | POST JSON |  |  |
| :---- | :---- | :---- | :---- |
| **ชื่อ** | **คำอธิบาย** | **ตัวอย่าง** | **REQUIRED?** |
| user | ชื่อผู้ใช้งาน ของ account ที่ได้รับแจ้งในเอกสารการใช้งาน หมายเหตุ : ห้ามเผยแพร่สู่สาธารณะเด็ดขาด | traffy | REQUIRED |
| pass | รหัสผ่าน ของ account ที่ได้รับแจ้งในเอกสารการใช้งาน หมายเหตุ : ห้ามเผยแพร่สู่สาธารณะเด็ดขาด | 1234 | REQUIRED |

**Example input**

| \[POST JSON\] {     "user": "traffy",     "pass": "1234" } |
| :---- |

**JSON output parameters**

| ชื่อ | คำอธิบาย | ตัวอย่าง |
| :---- | :---- | :---- |
| status | ผลการทำงาน ได้แก่\- success ไม่เกิดข้อผิดพลาดใด ๆ\- fail เกิดข้อผิดพลาด ไม่สามารถทำงานต่อได้\- warning เกิดข้อผิดพลาด แต่ยังสามารถทำงานต่อได้ | success |
| message | ข้อความอธิบายหากเกิดความผิดพลาด (status เป็น fail หรือ warning) | เช่น \- fail : “Invalid user or pass | user หรือ pass ไม่ถูกต้อง” \- warning : “Almost running out of API usage quota | ใกล้หมดโควต้าการใช้งาน API แล้ว” \- fail : “Running out of API usage quota | หมดโควต้าการใช้งาน API แล้ว” |
| exec\_time | เวลาที่ใช้ในการประมวลผลข้อมูล (หน่วยเป็นวินาที) | 0.041s |
| credit\_balance | จำนวนครั้งการใช้งานที่เหลืออยู่ในเดือนนี้ (-1 คือ ไม่จำกัด) | 820 |
| quota\_limit | จำนวนโควต้าต่อเดือน | 1000 |
| permissions | สิทธิ์ของ account นี้ | \[“read”,”write”\] |
| results | Array ของข้อมูลผลลัพธ์ (\*) | (Array) |
| \*ข้อมูลที่อยู่ใน results |  |  |
| token | JWT สำหรับนำไปใช้กับ API ต่าง ๆ | abcdefghijklmnopqrstuvwxyz1234567890 |
| expire\_timestamp | วันเวลาที่ token หมดอายุ (UTC+7) | 2023-06-13 00:48:46 |

**Example output**

| {     "status": "success",     "message": "",     "exec\_time": "0.041s",     "credit\_balance": 820,     "quota\_limit": 1000,     "permissions": \[         "read",         "write"     \],     "results": \[         "token": "abcdefghijklmnopqrstuvwxyz1234567890",         "expire\_timestamp": "2023-06-13 00:48:46"     \] } |
| :---- |

# 

# **การขอข้อมูลจาก Traffy Fondue** {#การขอข้อมูลจาก-traffy-fondue}

\* การใช้งานในกลุ่มนี้ account ต้องมีสิทธิ์ (permission) read ด้วย

## API get-issues (ขอ list รายการแจ้ง ที่หน่วยงานได้รับ) {#api-get-issues-(ขอ-list-รายการแจ้ง-ที่หน่วยงานได้รับ)}

ใช้ขอรายการแจ้งที่หน่วยงานได้รับ ตามวันเวลาที่แจ้งเข้าหรือมีการอัปเดตในช่วงเวลาที่เลือก duration

**URL:** https://publicapi.traffy.in.th/exchange-api/get-issues/v1

**Input parameters**

| Method | GET |  |  |
| :---- | :---- | :---- | :---- |
| **Authorization** | Bearer [Token](#api-get-auth-\(ขอ-jwt\)) |  |  |
| **ชื่อ** | **คำอธิบาย** | **ตัวอย่าง** | **REQUIRED?** |
| org\_id | หมายเลขหน่วยงานรับผิดชอบ หากต้องการดูหลายหน่วยงาน ให้คั่นด้วยลูกน้ำ (ดูได้เฉพาะหน่วยงานที่กำหนด และหน่วยงานในผังบังคับบัญชาเท่านั้น ตรวจสอบหน่วยงานที่สามารถใช้ได้ที่ API [get-org-list](https://docs.google.com/document/d/1X24qN0c9ZLu5sDsiklE2OVRJdTNgBqP3xA7mRHKZjn0/edit#heading=h.6x6f0qhbpe8z) โดยไม่ต้องส่งค่า org\_id มา) | 151,1302 | OPTIONAL (Default \= ถูกกำหนดตาม account) |
| duration | ช่วงเวลาของข้อมูล ได้แก่ “today” ดึงข้อมูลเฉพาะที่มีการแจ้งเข้าหรือมีการอัปเดต ในวันปัจจุบัน “all” ทั้งหมด หมายเหตุ : ข้อมูล JSON จะใหญ่มาก จนบาง client อาจรับไม่ไหว | today | OPTIONAL (Default \= today) |

**Example input**

| \[GET URL\] https://publicapi.traffy.in.th/exchange-api/get-issues/v1?org\_id=151,1302\&duration=today |
| :---- |

**JSON output parameters**

| ชื่อ | คำอธิบาย | ตัวอย่าง |
| :---- | :---- | :---- |
| status | ผลการทำงาน ได้แก่\- success ไม่เกิดข้อผิดพลาดใด ๆ\- fail เกิดข้อผิดพลาด ไม่สามารถทำงานต่อได้\- warning เกิดข้อผิดพลาด แต่ยังสามารถทำงานต่อได้ | success |
| message | ข้อความอธิบายหากเกิดความผิดพลาด (status เป็น fail หรือ warning) | เช่น \- fail : “This token has expired | Token นี้หมดอายุแล้ว” \- fail : “ERR 1 \- Invalid token | Token ไม่ถูกต้อง” \- fail : “Running out of API usage quota | หมดโควต้าการใช้งาน API แล้ว” \- warning : “Almost running out of API usage quota | ใกล้หมดโควต้าการใช้งาน API แล้ว” \- fail : “Not allowed to get data from this org\_id | ไม่มีสิทธิ์ขอข้อมูลจาก org\_id ที่ระบุ” |
| exec\_time | เวลาที่ใช้ในการประมวลผลข้อมูล (หน่วยเป็นวินาที) | 1.02s |
| source | แหล่งของข้อมูล ได้แก่\- db คือ ข้อมูลสดที่ได้จากฐานข้อมูล\- cache คือ ข้อมูลที่ผู้อื่นเคยขอไว้ก่อนแล้ว | cache 2023-05-30 09:55:02 (exp 60m) |
| credit\_balance | จำนวนครั้งการใช้งานที่เหลืออยู่ในเดือนนี้ | 820 |
| org\_id | Array หมายเลขหน่วยงานที่ใช้ค้นหา | \[151\] |
| count | จำนวนผลลัพธ์ | 12 |
| results | Array ของข้อมูลผลลัพธ์ (\*) | (Array) โดยจะเรียงจากการแจ้งล่าสุดหรืออัปเดตล่าสุดก่อน |
| \*ข้อมูลที่อยู่ใน results |  |  |
| ticket\_id | หมายเลขเรื่องแจ้ง | 2023-ABCDEF |
| type | ประเภทปัญหา | ถนน |
| organization | หน่วยงานที่ร่วมรับผิดชอบ (คั่นด้วยลูกน้ำ) | จังหวัดปทุมธานี, อำเภอคลองหลวง, เทศบาลตำบลท่าโขลง |
| description | ข้อความที่ผู้แจ้งพิมพ์มา | ถนนมีสะพานข้ามคลองชำรุด |
| photo | URL ภาพที่ผู้แจ้งส่งมา | https://… |
| latitude | Latitude ของพิกัด GPS | 14.077676 |
| longitude | Longitude ของพิกัด GPS | 100.601305 |
| address | ที่อยู่ไปรษณีย์ | 112 อุทยานวิทยาศาสตร์ ถนนพหลโ ยธิน ตำบล คลองหนึ่ง อำเภอคลองหลวง ปทุมธานี 12120 |
| subdistrict | แขวง | คลองหนึ่ง |
| district | เขต | คลองหลวง |
| province | จังหวัด (เป็นชื่อเต็ม เช่น กรุงเทพมหานคร พระนครศรีอยุธยา นครราชสีมา) | ปทุมธานี |
| timestamp | วันเวลาที่แจ้ง (UTC+7) | 2023-01-01 01:02:03.000000 |
| photo\_after | URL ภาพล่าสุดที่ จนท. ส่งมา | https://… |
| timestamp\_inprogress | วันเวลาที่ดำเนินการครั้งแรก (UTC+7) | 2023-01-01 01:02:03.000000 |
| timestamp\_finished | วันเวลาที่เสร็จสิ้นครั้งล่าสุด (UTC+7) | 2023-01-01 01:02:03.000000 |
| org\_finished | หน่วยงานที่กดเสร็จสิ้นครั้งล่าสุด | Traffy @ ITS Lab2 |
| status | สถานะ (รอรับเรื่อง, ดำเนินการ, ส่งต่อ ฯลฯ) *\*แต่ละหน่วยงาน อาจมองเห็น status ไม่เหมือนกัน แม้จะเป็นการแจ้งเรื่องเดียวกัน* | เสร็จสิ้น |
| confirmed | รับรองการแก้ไขแล้ว (true, false) | false |
| star | ดาวความพึงพอใจ (1-5) | 5 |
| count\_reopen | จำนวนครั้งที่ถูกเปิดเรื่องใหม่ | 3 |
| last\_activity | วันเวลากิจกรรมล่าสุด (UTC+7) | 2023-01-01 01:02:03.000000 |

**Example output**

| {     "status": "success",     "message": "",     "exec\_time": "1.441s",     "source": "cache 2023-05-30 09:55:02 (exp 60m)",     "credit\_balance": 820,     "org\_id": \[         151     \],     "count": 13,     "results": \[         {             "ticket\_id": "2023-ABCDEF",             "type": "ถนน",             "organization": "Traffy @ ITS Lab2, จังหวัดปทุมธานี, อำเภอคลองหลวง, เทศบาลตำบลท่าโขลง",             "description": "ถนนมีสะพานข้ามคลองชำรุด",             "photo": "https://storage.googleapis.com/traffy\_public\_bucket/attachment/2023-05/cd7795d479bf9a819ece9e4fe89171c232469e31.jpg",             "latitude": 14.077676,             "longitude": 100.601305,             "address": "112 อุทยานวิทยาศาสตร์ ถนนพหลโ ยธิน ตำบล คลองหนึ่ง อำเภอคลองหลวง ปทุมธานี 12120",             "subdistrict": "คลองหนึ่ง",             "district": "คลองหลวง",             "province": "ปทุมธานี",             "timestamp": "2023-01-01 01:02:03.000000",             "photo\_after": "https://storage.googleapis.com/traffy\_public\_bucket/attachment/2023-05/1684299562950.jpeg",             "timestamp\_inprogress": "2023-01-01 09:02:03.000000",             "timestamp\_finished": "2023-01-01 18:02:03.000000",             "org\_finished": "Traffy @ ITS Lab2",             "status": "เสร็จสิ้น",             "confirmed": false,             "star": 5,             "count\_reopen": 0,             "last\_activity": "2023-01-01 18:02:03.000000"         },         {             ...         }     \] } |
| :---- |

## API get-issue (ขอรายละเอียดการแจ้งของ ticket\_id ที่ระบุ) {#api-get-issue-(ขอรายละเอียดการแจ้งของ-ticket_id-ที่ระบุ)}

ใช้ขอรายละเอียดของการแจ้ง ticket\_id ที่ระบุ  
หมายเหตุ : ห้ามเผยแพร่ข้อมูลส่วนของ timeline สู่สาธารณะ เนื่องจากอาจมีชื่อและเบอร์โทรศัพท์ของผู้แจ้ง เพื่อให้ จนท. ติดต่อกลับ

**URL:** https://publicapi.traffy.in.th/exchange-api/get-issue/v1

**Input parameters**

| Method | GET |  |  |
| :---- | :---- | :---- | :---- |
| **Authorization** | Bearer [Token](#api-get-auth-\(ขอ-jwt\)) |  |  |
| **ชื่อ** | **คำอธิบาย** | **ตัวอย่าง** | **REQUIRED?** |
| ticket\_id | หมายเลขของเรื่องแจ้ง (จะดูได้เฉพาะเรื่องแจ้งในหน่วยงานที่กำหนด และหน่วยงานในผังบังคับบัญชาเท่านั้น)(ไม่ต้องใส่เครื่องหมาย \#) | 2023-ABCDEF | REQUIRED |
| refresh | บังคับให้ไม่ใช้ข้อมูลจาก cache หมายเหตุ : ให้ใช้เมื่อต้องการดึงข้อมูลการแจ้งหลังมีการอัปเดตเท่านั้น เพื่อให้ได้ข้อมูลที่อัปเดตล่าสุด | true | OPTIONAL (Default \= false) |

**Example input**

| \[GET URL\] https://publicapi.traffy.in.th/exchange-api/get-issue/v1?ticket\_id=2023-ABCDEF |
| :---- |

**JSON output parameters**

| ชื่อ | คำอธิบาย | ตัวอย่าง |
| :---- | :---- | :---- |
| status | ผลการทำงาน ได้แก่\- success ไม่เกิดข้อผิดพลาดใด ๆ\- fail เกิดข้อผิดพลาด ไม่สามารถทำงานต่อได้\- warning เกิดข้อผิดพลาด แต่ยังสามารถทำงานต่อได้ | success |
| message | ข้อความอธิบายหากเกิดความผิดพลาด (status เป็น fail หรือ warning) | เช่น \- fail : “ticket\_id is not specified | ไม่ได้ระบุ ticket\_id” \- fail : “ticket\_id is not found | ไม่พบ ticket\_id นี้” \- fail : “This token has expired | Token นี้หมดอายุแล้ว” \- fail : “ERR 1 \- Invalid token | Token ไม่ถูกต้อง” \- fail : “Running out of API usage quota | หมดโควต้าการใช้งาน API แล้ว” \- warning : “Almost running out of API usage quota | ใกล้หมดโควต้าการใช้งาน API แล้ว” \- fail : “Not allowed to get data from this org\_id | ไม่มีสิทธิ์ขอข้อมูลจาก org\_id ที่ระบุ” |
| exec\_time | เวลาที่ใช้ในการประมวลผลข้อมูล (หน่วยเป็นวินาที) | 1.02s |
| source | แหล่งของข้อมูล ได้แก่\- db คือ ข้อมูลสดที่ได้จากฐานข้อมูล\- cache คือ ข้อมูลที่ผู้อื่นเคยขอไว้ก่อนแล้ว | cache 2023-05-30 09:55:02 (exp 60m) |
| credit\_balance | จำนวนครั้งการใช้งานที่เหลืออยู่ในเดือนนี้ | 820 |
| ticket\_id | รหัสการแจ้งที่ต้องการดูรายละเอียด | 2023-ABCDEF |
| results | ข้อมูลผลลัพธ์ (\*) | (Array) |
| \*ข้อมูลที่อยู่ใน results |  |  |
| ticket\_id | หมายเลขของเรื่องแจ้ง | 2023-ABCDEF |
| type | ประเภทปัญหา ที่ผู้แจ้งได้เลือกไว้ | ถนน |
| organization | หน่วยงานที่ร่วมรับผิดชอบ คั่นด้วยลูกน้ำ | จังหวัดปทุมธานี, อำเภอคลองหลวง, เทศบาลตำบลท่าโขลง |
| description | ข้อความที่ผู้แจ้งพิมพ์มา | ถนนมีสะพานข้ามคลองชำรุด |
| photo | ภาพที่ผู้แจ้งส่งมา | https://… |
| latitude | Latitude ของพิกัด GPS | 14.077676 |
| longitude | Longitude ของพิกัด GPS | 100.601305 |
| address | ที่อยู่ไปรษณีย์ | 112 อุทยานวิทยาศาสตร์ ถนนพหลโ ยธิน ตำบล คลองหนึ่ง อำเภอคลองหลวง ปทุมธานี 12120 |
| subdistrict | แขวง | คลองหนึ่ง |
| district | เขต | คลองหลวง |
| province | จังหวัด | ปทุมธานี |
| timestamp | วันเวลาที่แจ้ง (UTC+7) | 2023-01-01 01:02:03.000000 |
| status\_type | สถานะตามที่ผู้แจ้งเห็น ได้แก่ \- report \= รอรับเรื่อง \- inprogress \= กำลังดำเนินการ \- finish \= เสร็จสิ้น \- irrelevant \= ไม่เกี่ยวข้อง \- follow \= ติดตามเรื่อง (ผู้แจ้ง และหน่วยงาน อาจจะเห็นสถานะไม่เหมือนกัน) | inprogress |
| confirmed | รับรองการแก้ไขแล้ว (true, false) | false |
| star | ดาวความพึงพอใจ (1-5) | 5 |
| count\_reopen | จำนวนครั้งที่ถูกเปิดเรื่องใหม่ | 3 |
| last\_activity | วันเวลากิจกรรมล่าสุด (UTC+7) | 2023-01-01 01:02:03.000000 |
| orgs | Array ของสถานะและประเภทปัญหา ที่แต่ละหน่วยงานได้รับ (แต่ละหน่วยงานอาจมองเห็นได้ไม่เหมือนกัน) (\*\*) | (Array)โดยเรียงลำดับตามวันเวลาที่ข้อมูลเข้าแต่ละหน่วยงาน |
| timeline | Array ของกิจกรรมการอัปเดต (\*\*\*) หมายเหตุ : ห้ามเผยแพร่ข้อมูลส่วนของ timeline สู่สาธารณะ เนื่องจากอาจมีชื่อและเบอร์โทรศัพท์ของผู้แจ้ง เพื่อให้ จนท. ติดต่อกลับ | (Array) โดยเรียงจากข้อความแรกก่อน |
| \*\*ข้อมูลที่อยู่ใน orgs |  |  |
| timestamp | วันเวลาของการอัปเดต (UTC+7) | 2023-01-01 18:02:03.000000 |
| org\_id | หมายเลขของหน่วยงาน | 151 |
| org | ชื่อหน่วยงาน | Traffy @ ITS Lab2 |
| type\_id | หมายเลขประเภทปัญหา ที่หน่วยงานได้รับ (ดูที่ API [get-type-list](#api-get-type-list-\(ขอ-list-รายชื่อประเภทปัญหาที่หน่วยงานรับผิดชอบ\))) | 540640 |
| type | ประเภทปัญหา ที่หน่วยงานได้รับ (ภาษาไทย) | ถนน |
| type\_en | ประเภทปัญหา ที่หน่วยงานได้รับ (ภาษาอังกฤษ) | Road |
| status\_id | หมายเลขของสถานะ ที่แต่ละหน่วยงานมองเห็น (ดูที่ API [get-status-list](#api-get-status-list-\(ขอ-list-รายชื่อสถานะของการแจ้งที่สามารถใช้ได้\))) | 135256 |
| status | สถานะ ที่แต่ละหน่วยงานมองเห็น | ของบประมาณ |
| status\_type | ประเภทของสถานะ ที่แต่ละหน่วยงานมองเห็น ได้แก่ start \= ยังไม่มี จนท. มารับเรื่อง inprogress \= ยังไม่เสร็จ finish \= เสร็จแล้ว irrelevant \= ไม่เกี่ยวข้อง follow \= ติดตามเรื่อง | inprogress |
| \*\*\*ข้อมูลที่อยู่ใน timeline |  |  |
| timestamp | วันเวลาของการอัปเดต (UTC+7) | 2023-01-01 18:02:03.000000 |
| status\_id | หมายเลขของสถานะที่ จนท. กดอัปเดต | 67273 |
| status | สถานะที่ จนท. กดอัปเดต | ส่งต่อ |
| org\_id | หมายเลขหน่วยงานที่กดอัปเดต | 151 |
| org | หน่วยงานที่กดอัปเดต | Traffy @ ITS Lab2 |
| note | คำอธิบายประกอบการอัปเดต | ได้ทำการแก้ไขเสร็จสิ้นแล้วครับ |
| photo | URL รูปภาพประกอบการอัปเดต | https://storage.googleapis.com/traffy\_public\_bucket/attachment/2023-05/1684299562950.jpeg |

**Example output**

| {     "status": "success",     "message": "",     "exec\_time": "1.441s",     "source": "cache 2023-05-30 09:55:02 (exp 60m)",     "credit\_balance": 820,     "ticket\_id": "2023-ABCDEF",     "results": {         "ticket\_id": "2023-ABCDEF",         "type": "ถนน",         "organization": "Traffy @ ITS Lab2, จังหวัดปทุมธานี, อำเภอคลองหลวง, เทศบาลตำบลท่าโขลง",         "description": "ถนนมีสะพานข้ามคลองชำรุด",         "photo": "https://storage.googleapis.com/traffy\_public\_bucket/attachment/2023-05/cd7795d479bf9a819ece9e4fe89171c232469e31.jpg",         "latitude": 14.077676,         "longitude": 100.601305,         "address": "112 อุทยานวิทยาศาสตร์ ถนนพหลโ ยธิน ตำบล คลองหนึ่ง อำเภอคลองหลวง ปทุมธานี 12120",         "subdistrict": "คลองหนึ่ง",         "district": "คลองหลวง",         "province": "ปทุมธานี",         "timestamp": "2023-01-01 01:02:03.000000",         "status\_type": "finish",         "confirmed": false,         "star": 5,         "count\_reopen": 0,         "last\_activity": "2023-01-01 18:02:03.000000",         "orgs": \[             {                 "timestamp": "2023-01-01 01:02:03.000000",                 "org\_id": 1302,                 "org": "อบต.แอตแลนติส (ใช้ทดสอบ)",                 "type\_id": 545507,                 "type": "จราจร",                 "type\_en": "Traffic",                 "status\_id": 51557,                 "status": "รอรับเรื่อง",                 "status\_type": "start"             },             {                 "timestamp": "2023-01-01 01:02:05.000000",                 "org\_id": 151,                 "org": "Traffy @ ITS Lab2",                 "type\_id": 545507,                 "type": "จราจร",                 "type\_en": "Traffic",                 "status\_id": 51557,                 "status": "รอรับเรื่อง",                 "status\_type": "start"             },             {                 ...             }         \],         "timeline": \[             {                 "timestamp": "2023-01-01 01:02:03.000000",                 "status\_id": null,                 "status": "รอรับเรื่อง",                 "org\_id": null,                 "org": null,                 "note": "ถนนมีสะพานข้ามคลองชำรุด",                 "photo": "https://storage.googleapis.com/traffy\_public\_bucket/attachment/2023-05/cd7795d479bf9a819ece9e4fe89171c232469e31.jpg",             },             {                 "timestamp": "2023-01-01 18:02:03.000000",                 "status\_id": 57011,                 "status": "เสร็จสิ้น",                 "org\_id": 151,                 "org": "Traffy @ ITS Lab2",                 "note": "ได้ทำการแก้ไขเสร็จสิ้นแล้วครับ",                 "photo": "https://storage.googleapis.com/traffy\_public\_bucket/attachment/2023-05/1684299562950.jpeg",             },             {                 ...             }         \]     } } |
| :---- |

## API download-issues (ดาวน์โหลดไฟล์ CSV ของ list รายการแจ้ง ที่หน่วยงานได้รับ) {#api-download-issues-(ดาวน์โหลดไฟล์-csv-ของ-list-รายการแจ้ง-ที่หน่วยงานได้รับ)}

ใช้ขอรายการแจ้งที่หน่วยงานได้รับ เป็นไฟล์ CSV ตามวันเวลาที่แจ้งเข้าหรือมีการอัปเดตในช่วงเวลาที่เลือก duration โดยจะเรียงจากการแจ้งล่าสุดหรืออัปเดตล่าสุดก่อน

**URL:** https://publicapi.traffy.in.th/exchange-api/download-issues/v1

**Input parameters**

| Method | GET |  |  |
| :---- | :---- | :---- | :---- |
| **Authorization** | Bearer [Token](#api-get-auth-\(ขอ-jwt\)) |  |  |
| **ชื่อ** | **คำอธิบาย** | **ตัวอย่าง** | **REQUIRED?** |
| org\_id | หมายเลขหน่วยงานรับผิดชอบ หากต้องการดูหลายหน่วยงาน ให้คั่นด้วยลูกน้ำ (ดูได้เฉพาะหน่วยงานที่กำหนด และหน่วยงานในผังบังคับบัญชาเท่านั้น ตรวจสอบหน่วยงานที่สามารถใช้ได้ที่ API [get-org-list](https://docs.google.com/document/d/1X24qN0c9ZLu5sDsiklE2OVRJdTNgBqP3xA7mRHKZjn0/edit#heading=h.6x6f0qhbpe8z) โดยไม่ต้องส่งค่า org\_id มา) | 151,1302 | OPTIONAL (Default \= ถูกกำหนดตาม account) |
| duration | ช่วงเวลาของข้อมูล ได้แก่ “today” ดึงข้อมูลเฉพาะที่มีการแจ้งเข้าหรือมีการอัปเดต ในวันปัจจุบัน “all” ทั้งหมด หมายเหตุ : ขนาดไฟล์จะใหญ่มาก ประมาณ 2-300 MB จนบาง client อาจรับไม่ไหว | today | OPTIONAL (Default \= today) |

**Example input**

| \[GET URL\] https://publicapi.traffy.in.th/exchange-api/download-issues/v1?org\_id=151 |
| :---- |

**CSV output parameters**

| ชื่อ | คำอธิบาย | ตัวอย่าง |
| :---- | :---- | :---- |
| ticket\_id | หมายเลขเรื่องแจ้ง | 2023-ABCDEF |
| type | ประเภทปัญหา | ถนน |
| organization | หน่วยงานที่ร่วมรับผิดชอบ (คั่นด้วยลูกน้ำ) | จังหวัดปทุมธานี, อำเภอคลองหลวง, เทศบาลตำบลท่าโขลง |
| description | ข้อความที่ผู้แจ้งพิมพ์มา | ถนนมีสะพานข้ามคลองชำรุด |
| photo | URL ภาพที่ผู้แจ้งส่งมา | https://… |
| latitude | Latitude ของพิกัด GPS | 14.077676 |
| longitude | Longitude ของพิกัด GPS | 100.601305 |
| address | ที่อยู่ไปรษณีย์ | 112 อุทยานวิทยาศาสตร์ ถนนพหลโ ยธิน ตำบล คลองหนึ่ง อำเภอคลองหลวง ปทุมธานี 12120 |
| subdistrict | แขวง | คลองหนึ่ง |
| district | เขต | คลองหลวง |
| province | จังหวัด (เป็นชื่อเต็ม เช่น กรุงเทพมหานคร พระนครศรีอยุธยา นครราชสีมา) | ปทุมธานี |
| timestamp | วันเวลาที่แจ้ง (UTC+7) | 2023-01-01 01:02:03.000000 |
| photo\_after | URL ภาพล่าสุดที่ จนท. ส่งมา | https://… |
| timestamp\_inprogress | วันเวลาที่ดำเนินการครั้งแรก (UTC+7) | 2023-01-01 01:02:03.000000 |
| timestamp\_finished | วันเวลาที่เสร็จสิ้นครั้งล่าสุด (UTC+7) | 2023-01-01 01:02:03.000000 |
| org\_finished | หน่วยงานที่กดเสร็จสิ้นครั้งล่าสุด | Traffy @ ITS Lab2 |
| status | สถานะ (รอรับเรื่อง, ดำเนินการ, ส่งต่อ ฯลฯ) *\*แต่ละหน่วยงาน อาจมองเห็น status ไม่เหมือนกัน แม้จะเป็นการแจ้งเรื่องเดียวกัน* | เสร็จสิ้น |
| confirmed | รับรองการแก้ไขแล้ว (true, false) | false |
| star | ดาวความพึงพอใจ (1-5) | 5 |
| count\_reopen | จำนวนครั้งที่ถูกเปิดเรื่องใหม่ | 3 |
| last\_activity | วันเวลากิจกรรมล่าสุด (UTC+7) | 2023-01-01 01:02:03.000000 |

## API search-org (ค้นหาหน่วยงานจากชื่อ) {#api-search-org-(ค้นหาหน่วยงานจากชื่อ)}

ใช้คนหาหน่วยงานจากชื่อ

**URL:** https://publicapi.traffy.in.th/exchange-api/search-org/v1

**Input parameters**

| Method | GET |  |  |
| :---- | :---- | :---- | :---- |
| **Authorization** | Bearer [Token](#api-get-auth-\(ขอ-jwt\)) |  |  |
| **ชื่อ** | **คำอธิบาย** | **ตัวอย่าง** | **REQUIRED?** |
| text | ชื่อของหน่วยงาน มีความยาวอย่างน้อย 3 ตัวอักษร \- ใช้สัญลักษณ์ \* แทนที่บางส่วนของชื่อ เช่น “เขตค\*” \- ใช้สัญลักษณ์ ? แทนที่ตัวอักษร 1 ตัว เช่น “เขตจตุจัก?” | เขตค\* | REQUIRED |

**Example input**

| \[GET URL\] https://publicapi.traffy.in.th/exchange-api/search-org/v1?text=เขตค\* |
| :---- |

**JSON output parameters**

| ชื่อ | คำอธิบาย | ตัวอย่าง |
| :---- | :---- | :---- |
| status | ผลการทำงาน ได้แก่\- success ไม่เกิดข้อผิดพลาดใด ๆ\- fail เกิดข้อผิดพลาด ไม่สามารถทำงานต่อได้\- warning เกิดข้อผิดพลาด แต่ยังสามารถทำงานต่อได้ | success |
| message | ข้อความอธิบายหากเกิดความผิดพลาด (status เป็น fail หรือ warning) | เช่น \- fail : “text is not specified | ไม่ได้ระบุ text” \- fail : “text is too short | text สั้นเกินไป” \- fail : “This token has expired | Token นี้หมดอายุแล้ว” \- fail : “ERR 1 \- Invalid token | Token ไม่ถูกต้อง” \- fail : “Running out of API usage quota | หมดโควต้าการใช้งาน API แล้ว” \- warning : “Almost running out of API usage quota | ใกล้หมดโควต้าการใช้งาน API แล้ว” |
| exec\_time | เวลาที่ใช้ในการประมวลผลข้อมูล (หน่วยเป็นวินาที) | 1.02s |
| source | แหล่งของข้อมูล ได้แก่\- db คือ ข้อมูลสดที่ได้จากฐานข้อมูล\- cache คือ ข้อมูลที่ผู้อื่นเคยขอไว้ก่อนแล้ว | cache 2023-05-30 09:55:02 (exp 60m) |
| credit\_balance | จำนวนครั้งการใช้งานที่เหลืออยู่ในเดือนนี้ | 820 |
| text | ชื่อของหน่วยงาน ที่ใช้ค้นหา | เขตค\* |
| count | จำนวนผลลัพธ์ | 4 |
| results | Array ของข้อมูลผลลัพธ์ (\*) | (Array) |
| \*ข้อมูลที่อยู่ใน results |  |  |
| org\_id | หมายเลขหน่วยงาน | 12345 |
| name | ชื่อหน่วยงาน | เขตคลองเตย |
| photo | รูปภาพประจำหน่วยงาน | https://storage.googleapis.com/traffy\_public\_bucket/attachment/2022-06/3c78cf9e067cfc439af33084e6c85d7b329297d1.jpg |
| latitude | Latitude ของพิกัด GPS | 13.708142 |
| longitude | Longitude ของพิกัด GPS | 100.583716 |
| address | ที่อยู่ไปรษณีย์ | เขตคลองเตย กรุงเทพมหานคร |
| province | จังหวัดที่ตั้งของสำนักงาน | กรุงเทพมหานคร |
| is\_official | เป็นหน่วยงานบัญชีทางการหรือไม่ | true |

**Example output**

| {     "status": "success",     "message": "",     "exec\_time": "1.441s",     "source": "cache 2023-05-30 09:55:02 (exp 60m)",     "credit\_balance": 820,     "text": "เขตค\*",     "count": 4,     "results": \[         {             "org\_id": 12345,             "name": "เขตคลองเตย",             "photo": "https://storage.googleapis.com/traffy\_public\_bucket/attachment/2022-06/3c78cf9e067cfc439af33084e6c85d7b329297d1.jpg",             "latitude": 13.70814,             "longitude": 100.58371,             "address": "เขตคลองเตย กรุงเทพมหานคร",             "province": "กรุงเทพมหานคร",             "is\_official": true         },         {             "org\_id": 12346,             "name": "เขตคลองสาน",             "photo": "https://storage.googleapis.com/traffy\_public\_bucket/attachment/2022-06/5beca6c6fdfe270705134b1486c3b8e4474b2600.jpg",             "latitude": 13.73054,             "longitude": 100.50925,             "address": "เขตคลองสาน กรุงเทพมหานคร",             "province": "กรุงเทพมหานคร",             "is\_official": true         },         {             ...         }     \] } |
| :---- |

## API get-org-list (ขอ list รายชื่อหน่วยงานที่อยู่ใต้บังคับบัญชา) {#api-get-org-list-(ขอ-list-รายชื่อหน่วยงานที่อยู่ใต้บังคับบัญชา)}

ใช้ขอรายการหน่วยงานที่อยู่ใต้บังคับบัญชา ของหน่วยงานหลักที่ระบุ

**URL:** https://publicapi.traffy.in.th/exchange-api/get-org-list/v1

**Input parameters**

| Method | GET |  |  |
| :---- | :---- | :---- | :---- |
| **Authorization** | Bearer [Token](#api-get-auth-\(ขอ-jwt\)) |  |  |
| **ชื่อ** | **คำอธิบาย** | **ตัวอย่าง** | **REQUIRED?** |
| org\_id | หมายเลขหน่วยงานหลัก | 151 | OPTIONAL (Default \= ถูกกำหนดตาม account) |

**Example input**

| \[GET URL\] https://publicapi.traffy.in.th/exchange-api/get-org-list/v1?org\_id=151 |
| :---- |

**JSON output parameters**

| ชื่อ | คำอธิบาย | ตัวอย่าง |
| :---- | :---- | :---- |
| status | ผลการทำงาน ได้แก่\- success ไม่เกิดข้อผิดพลาดใด ๆ\- fail เกิดข้อผิดพลาด ไม่สามารถทำงานต่อได้\- warning เกิดข้อผิดพลาด แต่ยังสามารถทำงานต่อได้ | success |
| message | ข้อความอธิบายหากเกิดความผิดพลาด (status เป็น fail หรือ warning) | เช่น \- fail : “This token has expired | Token นี้หมดอายุแล้ว” \- fail : “ERR 1 \- Invalid token | Token ไม่ถูกต้อง” \- fail : “Running out of API usage quota | หมดโควต้าการใช้งาน API แล้ว” \- warning : “Almost running out of API usage quota | ใกล้หมดโควต้าการใช้งาน API แล้ว” |
| exec\_time | เวลาที่ใช้ในการประมวลผลข้อมูล (หน่วยเป็นวินาที) | 1.02s |
| source | แหล่งของข้อมูล ได้แก่\- db คือ ข้อมูลสดที่ได้จากฐานข้อมูล\- cache คือ ข้อมูลที่ผู้อื่นเคยขอไว้ก่อนแล้ว | cache 2023-05-30 09:55:02 (exp 60m) |
| credit\_balance | จำนวนครั้งการใช้งานที่เหลืออยู่ในเดือนนี้ | 820 |
| org\_id | หมายเลขหน่วยงานหลัก | 151 |
| count | จำนวนผลลัพธ์ หากไม่มีหน่วยงานใต้บังคับบัญชา count จะเป็น 0 | 4 |
| results | Array ของข้อมูลผลลัพธ์ (\*) | (Array) โดยผลลัพธ์แรกจะเป็นหน่วยงานหลัก ต่อด้วยหน่วยงานใต้บังคับบัญชา หากไม่มีหน่วยงานใต้บังคับบัญชา ก็จะเป็น array ว่าง “results”: \[ \] |
| \*ข้อมูลที่อยู่ใน results |  |  |
| org\_id | หมายเลขหน่วยงาน | 151 |
| name | ชื่อหน่วยงาน | Traffy @ ITS Lab2 |
| photo | รูปภาพประจำหน่วยงาน | https://storage.googleapis.com/traffy\_public\_bucket/attachment/2023-02/f9a8feea6cc86563b8617c1b65a010491e2a987c.jpeg |
| latitude | Latitude ของพิกัด GPS | 13.77199 |
| longitude | Longitude ของพิกัด GPS | 100.62367 |
| address | ที่อยู่ไปรษณีย์ | ศูนย์เทคโนโลยีอิเล็กทรอนิกส์แห่งชาติ 112 อุทยานวิทยาศาสตร์ประเทศไทย ถนนพหลโยธิน ตำบลคลองหนึ่ง อำเภอคลองหลวง จังหวัดปทุมธานี 12120 |
| province | จังหวัดที่ตั้งของสำนักงาน | ปทุมธานี |

**Example output**

| {     "status": "success",     "message": "",     "exec\_time": "1.441s",     "source": "cache 2023-05-30 09:55:02 (exp 60m)",     "credit\_balance": 820,     "org\_id": 151,     "count": 619,     "results": \[         {             "org\_id": 151,             "name": "Traffy @ ITS Lab2",             "photo": "https://storage.googleapis.com/traffy\_public\_bucket/attachment/2023-02/f9a8feea6cc86563b8617c1b65a010491e2a987c.jpeg",             "latitude": 13.77199,             "longitude": 100.62367,             "address": "ศูนย์เทคโนโลยีอิเล็กทรอนิกส์แห่งชาติ 112 อุทยานวิทยาศาสตร์ประเทศไทย ถนนพหลโยธิน ตำบลคลองหนึ่ง อำเภอคลองหลวง จังหวัดปทุมธานี 12120",             "province": "กรุงเทพมหานคร",             "is\_official": true         },         {             "org\_id": 1302,             "name": "อบต.แอตแลนติส (ใช้ทดสอบ)",             "photo": "https://storage.googleapis.com/traffy\_public\_bucket/attachment/2022-06/3c78cf9e067cfc439af33084e6c85d7b329297d1.jpg",             "latitude": 13.70814,             "longitude": 100.58371,             "address": "ศูนย์เทคโนโลยีอิเล็กทรอนิกส์แห่งชาติ 112 อุทยานวิทยาศาสตร์ประเทศไทย ถนนพหลโยธิน ตำบลคลองหนึ่ง อำเภอคลองหลวง จังหวัดปทุมธานี 12120",             "province": "ปทุมธานี",             "is\_official": true         },         {             ...         }     \] } |
| :---- |

## API get-type-list (ขอ list รายชื่อประเภทปัญหาที่หน่วยงานรับผิดชอบ) {#api-get-type-list-(ขอ-list-รายชื่อประเภทปัญหาที่หน่วยงานรับผิดชอบ)}

ใช้ขอรายการประเภทปัญหา ในหน่วยงานที่ต้องการ (เมื่อจะทำการเพิ่มเรื่องแจ้งด้วย API [new-issue](#api-new-issue-\(เพิ่มเรื่องแจ้ง\)))

**URL:** https://publicapi.traffy.in.th/exchange-api/get-type-list/v1

**Input parameters**

| Method | GET |  |  |
| :---- | :---- | :---- | :---- |
| **Authorization** | Bearer [Token](#api-get-auth-\(ขอ-jwt\)) |  |  |
| **ชื่อ** | **คำอธิบาย** | **ตัวอย่าง** | **REQUIRED?** |
| org\_id | หมายเลขหน่วยงานที่จะดูว่ามีประเภทปัญหาอะไรให้ใช้บ้าง หากต้องการดูหลายหน่วยงาน ให้คั่นด้วยลูกน้ำ (ดูได้เฉพาะหน่วยงานที่กำหนด และหน่วยงานในผังบังคับบัญชาเท่านั้น ตรวจสอบหน่วยงานที่สามารถใช้ได้ที่ API [get-org-list](https://docs.google.com/document/d/1X24qN0c9ZLu5sDsiklE2OVRJdTNgBqP3xA7mRHKZjn0/edit#heading=h.6x6f0qhbpe8z) โดยไม่ต้องส่งค่า org\_id มา) | 151,1302 | OPTIONAL (Default \= ถูกกำหนดตาม account) |

**Example input**

| \[GET URL\] https://publicapi.traffy.in.th/exchange-api/get-type-list/v1?org\_id=151 |
| :---- |

**JSON output parameters**

| ชื่อ | คำอธิบาย | ตัวอย่าง |
| :---- | :---- | :---- |
| status | ผลการทำงาน ได้แก่\- success ไม่เกิดข้อผิดพลาดใด ๆ\- fail เกิดข้อผิดพลาด ไม่สามารถทำงานต่อได้\- warning เกิดข้อผิดพลาด แต่ยังสามารถทำงานต่อได้ | success |
| message | ข้อความอธิบายหากเกิดความผิดพลาด (status เป็น fail หรือ warning) | เช่น \- fail : “This token has expired | Token นี้หมดอายุแล้ว” \- fail : “ERR 1 \- Invalid token | Token ไม่ถูกต้อง” \- fail : “Running out of API usage quota | หมดโควต้าการใช้งาน API แล้ว” \- warning : “Almost running out of API usage quota | ใกล้หมดโควต้าการใช้งาน API แล้ว” \- fail : “Not allowed to get data from this org\_id | ไม่มีสิทธิ์ขอข้อมูลจาก org\_id ที่ระบุ” |
| exec\_time | เวลาที่ใช้ในการประมวลผลข้อมูล (หน่วยเป็นวินาที) | 1.02s |
| source | แหล่งของข้อมูล ได้แก่\- db คือ ข้อมูลสดที่ได้จากฐานข้อมูล\- cache คือ ข้อมูลที่ผู้อื่นเคยขอไว้ก่อนแล้ว | cache 2023-05-30 09:55:02 (exp 60m) |
| credit\_balance | จำนวนครั้งการใช้งานที่เหลืออยู่ในเดือนนี้ | 820 |
| org\_id | หมายเลขหน่วยงาน | 151 |
| count | จำนวนผลลัพธ์ | 16 |
| results | Array ของข้อมูลผลลัพธ์ (\*) | (Array) |
| \*ข้อมูลที่อยู่ใน results |  |  |
| topic\_id | หมายเลขของประเภทปัญหา (เป็น id กลางของระบบ Fondue) | 18 |
| type\_id | หมายเลขของประเภทปัญหา (เป็น id เฉพาะของแต่ละหน่วยงานนั้นๆ) | 24181 |
| type | ชื่อประเภทปัญหา (ไทย) | ถนน |
| type\_en | ชื่อประเภทปัญหา (อังกฤษ) | Road |
| photo | รูปภาพไอคอน | https://storage.googleapis.com/traffy\_public\_bucket/attachment/2018-03/s-1520325564.92.png |
| index | ลำดับการเรียงลำดับ | 3 |

**Example output**

| {     "status": "success",     "message": "",     "exec\_time": "1.441s",     "source": "cache 2023-05-30 09:55:02 (exp 60m)",     "credit\_balance": 820,     "org\_id": 151,     "count": 16,     "results": \[         {             "topic\_id": 18,             "type\_id": 24181,             "type": "ถนน",             "type\_en": "Road",             "photo": "https://storage.googleapis.com/traffy\_public\_bucket/attachment/2018-03/s-1520325564.92.png",             "index": 1         },         {             "topic\_id": 26,             "type\_id": 24182,             "type": "จราจร",             "type\_en": "Traffic",             "photo": "https://storage.googleapis.com/traffy\_public\_bucket/attachment/2022-09/s-1662976683.880073.png",             "index": 2         },         {             ...         }     \] } |
| :---- |

## API get-status-list (ขอ list รายชื่อสถานะของการแจ้งที่สามารถใช้ได้) {#api-get-status-list-(ขอ-list-รายชื่อสถานะของการแจ้งที่สามารถใช้ได้)}

ใช้ขอรายการสถานะการแจ้งที่สามารถใช้ได้ ในหน่วยงานที่ต้องการ (เมื่อจะทำการอัปเดตสถานะด้วย API [update-issue](#api-update-issue-\(อัปเดตสถานะ\)))

**URL:** https://publicapi.traffy.in.th/exchange-api/get-status-list/v1

**Input parameters**

| Method | GET |  |  |
| :---- | :---- | :---- | :---- |
| **Authorization** | Bearer [Token](#api-get-auth-\(ขอ-jwt\)) |  |  |
| **ชื่อ** | **คำอธิบาย** | **ตัวอย่าง** | **REQUIRED?** |
| org\_id | หมายเลขหน่วยงานที่จะดูว่ามีสถานะอะไรให้ใช้บ้าง หากต้องการดูหลายหน่วยงาน ให้คั่นด้วยลูกน้ำ (ดูได้เฉพาะหน่วยงานที่กำหนด และหน่วยงานในผังบังคับบัญชาเท่านั้น ตรวจสอบหน่วยงานที่สามารถใช้ได้ที่ API [get-org-list](https://docs.google.com/document/d/1X24qN0c9ZLu5sDsiklE2OVRJdTNgBqP3xA7mRHKZjn0/edit#heading=h.6x6f0qhbpe8z) โดยไม่ต้องส่งค่า org\_id มา) | 151 | OPTIONAL (Default \= ถูกกำหนดตาม account) |

**Example input**

| \[GET URL\] https://publicapi.traffy.in.th/exchange-api/get-status-list/v1?org\_id=151 |
| :---- |

**JSON output parameters**

| ชื่อ | คำอธิบาย | ตัวอย่าง |
| :---- | :---- | :---- |
| status | ผลการทำงาน ได้แก่\- success ไม่เกิดข้อผิดพลาดใด ๆ\- fail เกิดข้อผิดพลาด ไม่สามารถทำงานต่อได้\- warning เกิดข้อผิดพลาด แต่ยังสามารถทำงานต่อได้ | success |
| message | ข้อความอธิบายหากเกิดความผิดพลาด (status เป็น fail หรือ warning) | เช่น \- fail : “This token has expired | Token นี้หมดอายุแล้ว” \- fail : “ERR 1 \- Invalid token | Token ไม่ถูกต้อง” \- fail : “Running out of API usage quota | หมดโควต้าการใช้งาน API แล้ว” \- warning : “Almost running out of API usage quota | ใกล้หมดโควต้าการใช้งาน API แล้ว” \- fail : “Not allowed to get data from this org\_id | ไม่มีสิทธิ์ขอข้อมูลจาก org\_id ที่ระบุ” |
| exec\_time | เวลาที่ใช้ในการประมวลผลข้อมูล (หน่วยเป็นวินาที) | 1.02s |
| source | แหล่งของข้อมูล ได้แก่\- db คือ ข้อมูลสดที่ได้จากฐานข้อมูล\- cache คือ ข้อมูลที่ผู้อื่นเคยขอไว้ก่อนแล้ว | cache 2023-05-30 09:55:02 (exp 60m) |
| credit\_balance | จำนวนครั้งการใช้งานที่เหลืออยู่ในเดือนนี้ | 820 |
| org\_id | หมายเลขหน่วยงาน | 151 |
| count | จำนวนผลลัพธ์ | 10 |
| results | Array ของข้อมูลผลลัพธ์ (\*) | (Array) |
| \*ข้อมูลที่อยู่ใน results |  |  |
| status\_id | หมายเลขของสถานะ | 143562 |
| status | ชื่อสถานะ | เสร็จสิ้น |
| status\_type | ประเภทของสถานะ ได้แก่ start \= ยังไม่มี จนท. มารับเรื่อง inprogress \= ยังไม่เสร็จ finish \= เสร็จแล้ว irrelevant \= ไม่เกี่ยวข้อง follow \= ติดตามเรื่อง | follow |
| index | ลำดับการเรียงลำดับ | 3 |

**Example output**

| {     "status": "success",     "message": "",     "exec\_time": "1.441s",     "source": "cache 2023-05-30 09:55:02 (exp 60m)",     "credit\_balance": 820,     "count": 12,     "results": \[         {             "status\_id": 81,             "status": "รอรับเรื่อง",             "status\_type": "start",             "index": 1         },         {             "status\_id": 192003,             "status": "รับเรื่อง",             "status\_type": "inprogress",             "index": 2         },         {             ...         }     \] } |
| :---- |

# 

# **การส่งข้อมูลให้ Traffy Fondue** {#การส่งข้อมูลให้-traffy-fondue}

\* การใช้งานในกลุ่มนี้ account ต้องมีสิทธิ์ (permission) write ด้วย

## API new-issue (เพิ่มเรื่องแจ้ง) {#api-new-issue-(เพิ่มเรื่องแจ้ง)}

ใช้ส่งรายละเอียดการแจ้ง มาเข้าระบบ Traffy Fondue  
หมายเหตุ : มีการตรวจการแจ้งซ้ำจากเงื่อนไข 2 แบบคือ

1. ถ้าไม่ได้ระบุ client\_ticket\_id มา จะตรวจจาก description และ user\_id (จาก token) หรือ client\_user\_id หรือ reporter\_name  
2. ถ้าระบุ client\_ticket\_id มา แล้วเคยมี client\_ticket\_id นี้ในระบบ ก็จะถือว่าแจ้งซ้ำ

**URL:** https://publicapi.traffy.in.th/exchange-api/new-issue/v1

**Input parameters**

| Method | POST JSON |  |  |
| :---- | :---- | :---- | :---- |
| **Authorization** | Bearer [Token](#api-get-auth-\(ขอ-jwt\)) |  |  |
| **ชื่อ** | **คำอธิบาย** | **ตัวอย่าง** | **REQUIRED?** |
| description | ข้อความอธิบาย | ทดสอบการแจ้ง | REQUIRED |
| topic\_id | หมายเลขประเภทปัญหา (ดูที่ API [get-type-list](#api-get-type-list-\(ขอ-list-รายชื่อประเภทปัญหาที่หน่วยงานรับผิดชอบ\)) ผลลัพธ์ results.topic\_id) | 18 | REQUIRED (ให้เลือกส่ง type\_id หรือ topic\_id หรือ type\_name อย่างใดอย่างหนึ่งเท่านั้น) |
| type\_id | หมายเลขประเภทปัญหา (ดูที่ API [get-type-list](#api-get-type-list-\(ขอ-list-รายชื่อประเภทปัญหาที่หน่วยงานรับผิดชอบ\)) ผลลัพธ์ results.type\_id) | 24181 | REQUIRED (ให้เลือกส่ง type\_id หรือ topic\_id หรือ type\_name อย่างใดอย่างหนึ่งเท่านั้น) |
| type\_name | ชื่อประเภทปัญหา (ดูที่ API [get-type-list](#api-get-type-list-\(ขอ-list-รายชื่อประเภทปัญหาที่หน่วยงานรับผิดชอบ\)) ผลลัพธ์ results.type) | ถนน | REQUIRED (ให้เลือกส่ง type\_id หรือ topic\_id หรือ type\_name อย่างใดอย่างหนึ่งเท่านั้น) |
| photo\_base64 | Array ของ base64 ของไฟล์ (ไม่เกิน 3 ไฟล์) **หมายเหตุ:** \- ต้องมี MIME ด้วย เช่น data:image/jpeg หรือ data:application/pdf \- ขนาดไฟล์ไม่เกิน 90 MB | \["data:image/jpeg;base64,/9j/4AAQSk….","...","..."\] | REQUIRED (ถ้าไม่ได้ส่ง photo มา) |
| photo | Array ของลิงค์ URL ของไฟล์ (ไม่เกิน 3 ไฟล์) **หมายเหตุ:** \- ใน URL ต้องมีนามสกุลของไฟล์ เช่น .pdf \- ขนาดไฟล์ไม่เกิน 90 MB | \["https://storage.googleapis.com/traffy-fondue/line\_img/13565592441693.jpeg","...","..."\] | REQUIRED(ถ้าไม่ได้ส่ง photo\_base64 มา) |
| latitude | Latitude ของพิกัด GPS | 13.754158 | REQUIRED |
| longitude | Longitude ของพิกัด GPS | 100.5014985 | REQUIRED |
| address | ที่อยู่ไปรษณีย์ | 112 ถ.พหลโยธิน ต.คลองหนึ่ง อ.คลองหลวง จ.ปทุมธานี | OPTIONAL |
| org\_id | หมายเลขหน่วยงานที่จะแจ้งเข้า (ถ้าต้องการแจ้งเข้าหลายหน่วยงาน ให้คั่นด้วยลูกน้ำ เช่น 151,1302) | 151 | OPTIONAL (Default \= ถูกกำหนดตาม account) |
| reporter\_name | ชื่อนามสกุลผู้แจ้ง | ทดสอบ Traffy | OPTIONAL |
| reporter\_phone | เบอร์โทรติดต่อของผู้แจ้ง (สามารถใส่ตัวอักษรอื่นๆได้) | 0812345678-9 ต่อ 123 เฉพาะนอกเวลาราชการ | OPTIONAL |
| reporter\_sms | เบอร์มือถือของผู้แจ้ง สำหรับใช้ส่ง SMS รายงานความคืบหน้าเท่านั้น (ไม่แสดงให้ จนท. เห็น) | 0812345678 | OPTIONAL (ถ้ามี ก็จะส่ง SMS แจ้งความคืบหน้าไปให้) |
| reporter\_email | อีเมลของผู้แจ้ง สำหรับใช้รายงานความคืบหน้าเท่านั้น (ไม่แสดงให้ จนท. เห็น) | abc@def.com | OPTIONAL (ขณะนี้ยังไม่มีฟีเจอร์ส่งอีเมลแจ้งความคืบหน้า) |
| client\_user\_id | หมายเลขผู้แจ้ง ตามที่เก็บไว้ในระบบต้นทาง (ถ้ามี) | abc123456 | OPTIONAL |
| client\_ticket\_id | หมายเลขเรื่องแจ้ง ตามที่เก็บไว้ในระบบต้นทาง (ถ้ามี) | p123456789 | OPTIONAL |
| client\_timestamp | วันเวลาที่แจ้ง (หากไม่ระบุ time zone จะถือว่าเป็น UTC+7) หมายเหตุ : ใช้เก็บเพื่ออ้างอิงเท่านั้น ซึ่งในระบบ Fondue จะใช้ timestamp ตามวันเวลาที่ได้รับข้อมูล | 2023-01-01 01:02:03+0700 | OPTIONAL |

**Example input**

| \[POST JSON\] {     "description": "ทดสอบการแจ้ง",     "type\_id": 24181,     "photo\_base64": \[         "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAJYAAAC0CAMAAABBuyXtAAAAGXRFWHRTb2Z0d2FyZQBBZG9iZSBJbWFnZVJlYWR5ccllPAAAAyFpVFh0WE1MOmNvbS5hZG9iZS54bXAAAAAAADw/eHBhY2tldCBiZWdpbj0i77u/IiBpZD0iVzVNME1wQ2VoaUh6cmVTek5UY3prYzlkIj8+IDx4OnhtcG1ldGEgeG1sbnM6eD0iYWRvYmU6bnM6bWV0YS8iIHg6eG1wdGs9IkFkb2JlIFhNUCBDb3JlIDUuNS1jMDIxIDc5LjE1NDkxMSwgMjAxMy8xMC8yOS0xMTo0NzoxNiAgICAgICAgIj4gPHJkZjpSREYgeG1sbnM6cmRmPSJodHRwOi8vd3d3LnczLm9yZy8xOTk5LzAyLzIyLXJkZi1zeW50YXgtbnMjIj4gPHJkZjpEZXNjcmlwdGlvbiByZGY6YWJvdXQ9IiIgeG1sbnM6eG1wTU09Imh0dHA6Ly9ucy5hZG9iZS5jb20veGFwLzEuMC9tbS8iIHhtbG5zOnN0UmVmPSJodHRwOi8vbnMuYWRvYmUuY29tL3hhcC8xLjAvc1R5cGUvUmVzb3VyY2VSZWYjIiB4bWxuczp4bXA9Imh0dHA6Ly9ucy5hZG9iZS5jb20veGFwLzEuMC8iIHhtcE1NOkRvY3VtZW50SUQ9InhtcC5kaWQ6MTlGQUFDMjEwQzJEMTFFRTk2OTdFMjY4NkM5ODI5M0QiIHhtcE1NOkluc3RhbmNlSUQ9InhtcC5paWQ6MTlGQUFDMjAwQzJEMTFFRTk2OTdFMjY4NkM5ODI5M0QiIHhtcDpDcmVhdG9yVG9vbD0iQWRvYmUgUGhvdG9zaG9wIENDIChXaW5kb3dzKSI+IDx4bXBNTTpEZXJpdmVkRnJvbSBzdFJlZjppbnN0YW5jZUlEPSJ4bXAuaWlkOkIxMTMwQzIwMEMyQzExRUVCNEQ1QUExM0M1RjExMEI2IiBzdFJlZjpkb2N1bWVudElEPSJ4bXAuZGlkOkIxMTMwQzIxMEMyQzExRUVCNEQ1QUExM0M1RjExMEI2Ii8+IDwvcmRmOkRlc2NyaXB0aW9uPiA8L3JkZjpSREY+IDwveDp4bXBtZXRhPiA8P3hwYWNrZXQgZW5kPSJyIj8+ShfL/QAAAAxQTFRFcUcn/vz4/6gAnH9owsExJwAABNNJREFUeNrsnIGSsyoMhRPO+7/z/S2KIYQKVSA7V3Z2xlKVz0MSAlqJRQFoWQEkCQkoWlxgYC2HyhQjR1RCsB2LvBRILCJnXOSMauciP3YlucgdFVHE8ka1yUX+xNq4yJ9YbrH+QcEjFsb5ONxhwdMwmwUed4PHiQV3Du4Z65WrM8l8jf6vyrWJ5E8ubDD+5Iow3uTah2lvMeLoO1+9mLIHV3Kdpu7K6MVKi6c1l3MpyJFcyFbL0mIe4IVKhtTV64ycrTMSMTtYa8yplFzLbExRSaNfqFdBVcoFD1SlXPO7ka0CXY/1Uq23rhpVIRc8QJVGDxdU60LqV6hCLriAKuRyAqXkghssnooFcC2G1mMEZsiE7l6ctrzQJxeG9l41QbiSC3OMvFGu0RN/XOVTC+SCmeZ1yjUtn1orF+5gDZzJVrsHS2MEP9OLPAVreYyAT7ngVC5mlzHiMbmmqGXLhbMMlQvfuocUUTFsbXAjjB5tXvZl1Q8jjL4F63q973m5Lqy5cQlydi/23kmYIRc6zzLFFfsaeXwpfCO4CzVkymN1Jdrtfepa5CeML8fCA/Y1jgsubgQoLpgz2dVcVn+6eq7AJRa5hJJcvrDgnQsv11/m8uSOMnH3YvaII+JUd0zTzqKWjto9n5lqXvYTo0dihXO45qlmH6fnWq04KYYAkVhzHomK7W/y7Izx+bVPw3Gyus3dod1x/B2dNFEWv1CKIsZlUAIXrjkhQuLI9qLTnZ34kWn7X/KgRcRKViNs61CMlmJFBxRY0a4WYx19Jjxx3SiTYSHvxDTDWIRFCWL7uPcfJZvzVtjnb7vasR5/UBGy/NoUHo78eGh9YSwW3zmNP6wxauH4gye1Hpp7TMCC5YZGJT6JWotaQK9nX04HceTRqvVzx/SFOB/0drukFla5ZlR5oNSILlI2cW50epWBZTylaUc3cCtW92Ii7BMzp5uD2QwJ0Kz5XUQbS+2J7rhF2S0RKNTzADJ2rGOpS/kZC3Jqn7GcjUH7RBVrr/v8NQYkmA+i6ChZbYyasfpG3oI+r3gEi5dhkU+sPrXoJla/bdGlrj9G+aonWm6Pb1jW0bewzLhVj0bytjQbsS4fX/ETVjXK27Gb8yifIrq0o3PPrnDaNiZejHTHDlZl94ShI4P4nhfUso0fx+rv+RYMeDOLMhILEMxsoy0XRFMqWd0+doSoTnXyFOhKT9/ylre85Yg4eLE6sJy+usnr+7ccYvl8txviXTl/Yjl+QZ8zLpwvf3T3WBg5fK+LuxeLAsXbYeFGqvyltavBYL9LN/8Rxeyi5pLssrxYL9YfwgphLFbYStoqvgnG3v8qs2/0juJjeYY2rLOF/ORBFFEnvyp2LE55HtOJFXRrrK9Sbply6LogDwo/Y+1HZj2jdUuXUHY86/1CptFdtQJfYOn+Vaj8pFrJEIKFFfLa8/qt1gTqbdtKHRIMgwpKw9P8TDWVF9xSK11+A9YRGgpUNlBvqnU01obF2jksy3zAtjLX1lghWFgcyngZhqhlYylHUMazTC3DEczRxXSDR9SyQ3soYlApginMU2pVsLhUy3QOnqlWMKlDoRZPVcuqZTvwDlLL2jJrTbWG2RZ/jwBBBfRyQAr1bO1htcrv68oEM0G9oHpnPi/W/w3rPwEGAJgFLVXEhHMJAAAAAElFTkSuQmCC"     \],     "latitude": 13.754158,     "longitude": 100.5014985 } |
| :---- |

**JSON output parameters**

| ชื่อ | คำอธิบาย | ตัวอย่าง |
| :---- | :---- | :---- |
| status | ผลการทำงาน ได้แก่\- success ไม่เกิดข้อผิดพลาดใด ๆ\- fail เกิดข้อผิดพลาด ไม่สามารถทำงานต่อได้\- warning เกิดข้อผิดพลาด แต่ยังสามารถทำงานต่อได้ | success |
| message | ข้อความอธิบายหากเกิดความผิดพลาด (status เป็น fail หรือ warning) | เช่น \- fail : “Invalid input JSON format | ข้อมูลรูปแบบ JSON ไม่ถูกต้อง” \- fail : “latitude or longitude is not specified | ไม่ได้ระบุ latitude หรือ longitude” \- fail : “Invalid latitude or longitude value | ค่าพิกัด latitude (-90 ถึง 90\) หรือ longitude (-180 ถึง 180\) ไม่ถูกต้อง” \- fail : “\[ชื่อ parameter\] is not specified | ไม่ได้ระบุ \[ชื่อ parameter\]” \- fail : “Duplicate of post \#\[ticket\_id\] | แจ้งซ้ำกับหมายเลข \#\[ticket\_id\]” \- fail : “Only support photo file type gif/png/jpg/jpeg | รองรับเฉพาะไฟล์รูปชนิด gif/png/jpg/jpeg เท่านั้น” \- fail : “This token has expired | Token นี้หมดอายุแล้ว” \- fail : “ERR 1 \- Invalid token | Token ไม่ถูกต้อง” \- fail : “Running out of API usage quota | หมดโควต้าการใช้งาน API แล้ว” \- warning : “Almost running out of API usage quota | ใกล้หมดโควต้าการใช้งาน API แล้ว” \- fail : “Not allowed to get data from this org\_id | ไม่มีสิทธิ์ขอข้อมูลจาก org\_id ที่ระบุ” |
| exec\_time | เวลาที่ใช้ในการประมวลผลข้อมูล (หน่วยเป็นวินาที) | 1.02s |
| credit\_balance | จำนวนครั้งการใช้งานที่เหลืออยู่ในเดือนนี้ | 820 |
| org\_id | Array หมายเลขหน่วยงานที่แจ้งเข้า | \[151\] |
| results | ข้อมูลผลลัพธ์ (\*) |  |
| \*ข้อมูลที่อยู่ใน results |  |  |
| ticket\_id | หมายเลขของเรื่องแจ้ง | ABCDEF |
| password | รหัสผ่านสำหรับใช้ติดตามความคืบหน้า โดยสามารถติดตามได้ที่เว็บไซต์ https://c.traffy.in.th/ ตามด้วย ticket\_id เช่น https://c.traffy.in.th/ABCDEF | 1234 |

**Example output**

| {     "status": "success",     "message": "",     "exec\_time": "1.441s",     "credit\_balance": 820,     "org\_id": \[         151     \],     "results": {         "ticket\_id": "ABCDEF",         "password": "1234",         "smsStatus": "Y"     } } |
| :---- |

## API update-issue (อัปเดตสถานะ) {#api-update-issue-(อัปเดตสถานะ)}

ใช้อัปเดตสถานะของเรื่องแจ้ง ในระบบ Traffy Fondue  
หมายเหตุ : ไม่สามารถใช้สำหรับการ เชิญร่วม, ส่งต่อ, เปิดเรื่องใหม่, รับรองการแก้ไข, เปลี่ยนประเภทปัญหา

**URL:** https://publicapi.traffy.in.th/exchange-api/update-issue/v1

**Input parameters**

| Method | PATCH JSON |  |  |
| :---- | :---- | :---- | :---- |
| **Authorization** | Bearer [Token](#api-get-auth-\(ขอ-jwt\)) |  |  |
| **ชื่อ** | **คำอธิบาย** | **ตัวอย่าง** | **REQUIRED?** |
| ticket\_id | หมายเลขของเรื่องแจ้งที่ต้องการอัปเดตสถานะ (ไม่ต้องใส่เครื่องหมาย \#) | 2023-ABCDEF | REQUIRED (ถ้าไม่ได้ส่ง client\_ticket\_id มา) |
| client\_ticket\_id | หมายเลขเรื่องแจ้ง ตามที่เก็บไว้ในระบบต้นทาง | p123456789 | REQUIRED (ถ้าไม่ได้ส่ง ticket\_id มา) |
| org\_id | หมายเลขหน่วยงานที่เป็นผู้ดำเนินการ (ได้เฉพาะหน่วยงานที่กำหนด และหน่วยงานในผังบังคับบัญชาเท่านั้น) (ดูที่ API [get-org-list](#api-get-org-list-\(ขอ-list-รายชื่อหน่วยงานที่อยู่ใต้บังคับบัญชา\))) | 151 | OPTIONAL (Default \= ถูกกำหนดตาม account) |
| status\_id | หมายเลขของสถานะที่ต้องการใช้ (ดูที่ API [get-status-list](#api-get-status-list-\(ขอ-list-รายชื่อสถานะของการแจ้งที่สามารถใช้ได้\))) | 143562 | REQUIRED |
| note | คำอธิบายประกอบการอัปเดต | ได้ทำการแก้ไขเสร็จสิ้นแล้วครับ | REQUIRED |
| extra\_detail | ข้อมูลเพิ่มเติม | { "CreateDate": "2023-12-03T19:25:05.3766667", "UpdateDate": "2024-01-31T11:48:54.2733333", "PersonalFirstName": "ทดสอบ", … } | OPTIONAL |
| photo\_base64 | Array ของ base64 ของไฟล์ (ไม่เกิน 3 ไฟล์) **หมายเหตุ:** \- ต้องมี MIME ด้วย เช่น data:image/jpeg หรือ data:application/pdf \- ขนาดไฟล์ไม่เกิน 90 MB | \["data:image/jpeg;base64,/9j/4AAQSk….","...","..."\] | REQUIRED (ถ้าไม่ได้ส่ง photo มา) |
| photo | Array ของลิงค์ URL ของไฟล์ (ไม่เกิน 3 ไฟล์) **หมายเหตุ:** \- ใน URL ต้องมีนามสกุลของไฟล์ เช่น .pdf \- ขนาดไฟล์ไม่เกิน 90 MB | \["https://storage.googleapis.com/traffy-fondue/line\_img/13565592441693.jpeg","...","..."\] | REQUIRED(ถ้าไม่ได้ส่ง photo\_base64 มา) |

**Example input**

| \[PATCH JSON\] {     "ticket\_id": "2023-ABCDEF",     "org\_id": 151,     "status\_id": 143562,     "note": "ได้ทำการแก้ไขเสร็จสิ้นแล้วครับ",     "extra\_detail": {         "CreateDate": "2023-12-03T19:25:05.3766667",         "UpdateDate": "2024-01-31T11:48:54.2733333",         "PersonalFirstName": "ทดสอบ",         …     },     "photo\_base64": \[         "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAJYAAAC0CAMAAABBuyXtAAAAGXRFWHRTb2Z0d2FyZQBBZG9iZSBJbWFnZVJlYWR5ccllPAAAAyFpVFh0WE1MOmNvbS5hZG9iZS54bXAAAAAAADw/eHBhY2tldCBiZWdpbj0i77u/IiBpZD0iVzVNME1wQ2VoaUh6cmVTek5UY3prYzlkIj8+IDx4OnhtcG1ldGEgeG1sbnM6eD0iYWRvYmU6bnM6bWV0YS8iIHg6eG1wdGs9IkFkb2JlIFhNUCBDb3JlIDUuNS1jMDIxIDc5LjE1NDkxMSwgMjAxMy8xMC8yOS0xMTo0NzoxNiAgICAgICAgIj4gPHJkZjpSREYgeG1sbnM6cmRmPSJodHRwOi8vd3d3LnczLm9yZy8xOTk5LzAyLzIyLXJkZi1zeW50YXgtbnMjIj4gPHJkZjpEZXNjcmlwdGlvbiByZGY6YWJvdXQ9IiIgeG1sbnM6eG1wTU09Imh0dHA6Ly9ucy5hZG9iZS5jb20veGFwLzEuMC9tbS8iIHhtbG5zOnN0UmVmPSJodHRwOi8vbnMuYWRvYmUuY29tL3hhcC8xLjAvc1R5cGUvUmVzb3VyY2VSZWYjIiB4bWxuczp4bXA9Imh0dHA6Ly9ucy5hZG9iZS5jb20veGFwLzEuMC8iIHhtcE1NOkRvY3VtZW50SUQ9InhtcC5kaWQ6MTlGQUFDMjEwQzJEMTFFRTk2OTdFMjY4NkM5ODI5M0QiIHhtcE1NOkluc3RhbmNlSUQ9InhtcC5paWQ6MTlGQUFDMjAwQzJEMTFFRTk2OTdFMjY4NkM5ODI5M0QiIHhtcDpDcmVhdG9yVG9vbD0iQWRvYmUgUGhvdG9zaG9wIENDIChXaW5kb3dzKSI+IDx4bXBNTTpEZXJpdmVkRnJvbSBzdFJlZjppbnN0YW5jZUlEPSJ4bXAuaWlkOkIxMTMwQzIwMEMyQzExRUVCNEQ1QUExM0M1RjExMEI2IiBzdFJlZjpkb2N1bWVudElEPSJ4bXAuZGlkOkIxMTMwQzIxMEMyQzExRUVCNEQ1QUExM0M1RjExMEI2Ii8+IDwvcmRmOkRlc2NyaXB0aW9uPiA8L3JkZjpSREY+IDwveDp4bXBtZXRhPiA8P3hwYWNrZXQgZW5kPSJyIj8+ShfL/QAAAAxQTFRFcUcn/vz4/6gAnH9owsExJwAABNNJREFUeNrsnIGSsyoMhRPO+7/z/S2KIYQKVSA7V3Z2xlKVz0MSAlqJRQFoWQEkCQkoWlxgYC2HyhQjR1RCsB2LvBRILCJnXOSMauciP3YlucgdFVHE8ka1yUX+xNq4yJ9YbrH+QcEjFsb5ONxhwdMwmwUed4PHiQV3Du4Z65WrM8l8jf6vyrWJ5E8ubDD+5Iow3uTah2lvMeLoO1+9mLIHV3Kdpu7K6MVKi6c1l3MpyJFcyFbL0mIe4IVKhtTV64ycrTMSMTtYa8yplFzLbExRSaNfqFdBVcoFD1SlXPO7ka0CXY/1Uq23rhpVIRc8QJVGDxdU60LqV6hCLriAKuRyAqXkghssnooFcC2G1mMEZsiE7l6ctrzQJxeG9l41QbiSC3OMvFGu0RN/XOVTC+SCmeZ1yjUtn1orF+5gDZzJVrsHS2MEP9OLPAVreYyAT7ngVC5mlzHiMbmmqGXLhbMMlQvfuocUUTFsbXAjjB5tXvZl1Q8jjL4F63q973m5Lqy5cQlydi/23kmYIRc6zzLFFfsaeXwpfCO4CzVkymN1Jdrtfepa5CeML8fCA/Y1jgsubgQoLpgz2dVcVn+6eq7AJRa5hJJcvrDgnQsv11/m8uSOMnH3YvaII+JUd0zTzqKWjto9n5lqXvYTo0dihXO45qlmH6fnWq04KYYAkVhzHomK7W/y7Izx+bVPw3Gyus3dod1x/B2dNFEWv1CKIsZlUAIXrjkhQuLI9qLTnZ34kWn7X/KgRcRKViNs61CMlmJFBxRY0a4WYx19Jjxx3SiTYSHvxDTDWIRFCWL7uPcfJZvzVtjnb7vasR5/UBGy/NoUHo78eGh9YSwW3zmNP6wxauH4gye1Hpp7TMCC5YZGJT6JWotaQK9nX04HceTRqvVzx/SFOB/0drukFla5ZlR5oNSILlI2cW50epWBZTylaUc3cCtW92Ii7BMzp5uD2QwJ0Kz5XUQbS+2J7rhF2S0RKNTzADJ2rGOpS/kZC3Jqn7GcjUH7RBVrr/v8NQYkmA+i6ChZbYyasfpG3oI+r3gEi5dhkU+sPrXoJla/bdGlrj9G+aonWm6Pb1jW0bewzLhVj0bytjQbsS4fX/ETVjXK27Gb8yifIrq0o3PPrnDaNiZejHTHDlZl94ShI4P4nhfUso0fx+rv+RYMeDOLMhILEMxsoy0XRFMqWd0+doSoTnXyFOhKT9/ylre85Yg4eLE6sJy+usnr+7ccYvl8txviXTl/Yjl+QZ8zLpwvf3T3WBg5fK+LuxeLAsXbYeFGqvyltavBYL9LN/8Rxeyi5pLssrxYL9YfwgphLFbYStoqvgnG3v8qs2/0juJjeYY2rLOF/ORBFFEnvyp2LE55HtOJFXRrrK9Sbply6LogDwo/Y+1HZj2jdUuXUHY86/1CptFdtQJfYOn+Vaj8pFrJEIKFFfLa8/qt1gTqbdtKHRIMgwpKw9P8TDWVF9xSK11+A9YRGgpUNlBvqnU01obF2jksy3zAtjLX1lghWFgcyngZhqhlYylHUMazTC3DEczRxXSDR9SyQ3soYlApginMU2pVsLhUy3QOnqlWMKlDoRZPVcuqZTvwDlLL2jJrTbWG2RZ/jwBBBfRyQAr1bO1htcrv68oEM0G9oHpnPi/W/w3rPwEGAJgFLVXEhHMJAAAAAElFTkSuQmCC"     \] } |
| :---- |

**JSON output parameters**

| ชื่อ | คำอธิบาย | ตัวอย่าง |
| :---- | :---- | :---- |
| status | ผลการทำงาน ได้แก่\- success ไม่เกิดข้อผิดพลาดใด ๆ\- fail เกิดข้อผิดพลาด ไม่สามารถทำงานต่อได้\- warning เกิดข้อผิดพลาด แต่ยังสามารถทำงานต่อได้ | success |
| message | ข้อความอธิบายหากเกิดความผิดพลาด (status เป็น fail หรือ warning) |  |
| exec\_time | เวลาที่ใช้ในการประมวลผลข้อมูล (หน่วยเป็นวินาที) | 1.02s |
| credit\_balance | จำนวนครั้งการใช้งานที่เหลืออยู่ในเดือนนี้ | 820 |
| org\_id | หมายเลขหน่วยงานที่ทำการอัปเดต | 151 |
| status\_id | หมายเลขของสถานะ ที่หน่วยงาน org\_id จะมองเห็น (ดูที่ API [get-status-list](#api-get-status-list-\(ขอ-list-รายชื่อสถานะของการแจ้งที่สามารถใช้ได้\))) | 135256 |
| status | สถานะ ที่หน่วยงาน org\_id จะมองเห็น | เสร็จสิ้น |
| status\_type | สถานะตามที่ผู้แจ้งเห็น ได้แก่ \- report \= รอรับเรื่อง \- inprogress \= กำลังดำเนินการ \- finish \= เสร็จสิ้น \- irrelevant \= ไม่เกี่ยวข้อง \- follow \= ติดตามเรื่อง (ผู้แจ้ง และหน่วยงาน อาจจะเห็นสถานะไม่เหมือนกัน) | finish |

**Example output**

| {     "status": "success",     "message": "",     "exec\_time": "1.441s",     "credit\_balance": 820,     "org\_id": 151,     "status\_id": 135256,     "status": "เสร็จสิ้น",     "status\_type": "finish" } |
| :---- |

## API star (ให้คะแนนเรื่องแจ้ง)

ใช้ส่งคะแนนของเรื่องแจ้ง มาเข้าระบบ Traffy Fondue

**URL:** https://publicapi.traffy.in.th/exchange-api/star/v1

**Input parameters**

| Method | POST JSON |  |  |
| :---- | :---- | :---- | :---- |
| **Authorization** | Bearer [Token](#api-get-auth-\(ขอ-jwt\)) |  |  |
| **ชื่อ** | **คำอธิบาย** | **ตัวอย่าง** | **REQUIRED?** |
| star | คะแนนเรื่องแจ้ง | 5 (ต้องเป็นตัวเลข 1-5) | REQUIRED |
| ticket\_id | หมายเลขของเรื่องแจ้งที่ต้องการให้คะแนน (ไม่ต้องใส่เครื่องหมาย \#) | 2023-ABCDEF | REQUIRED (ถ้าไม่ได้ส่ง client\_ticket\_id มา) |
| client\_ticket\_id | หมายเลขเรื่องแจ้ง ตามที่เก็บไว้ในระบบต้นทาง | p123456789 | REQUIRED (ถ้าไม่ได้ส่ง ticket\_id มา) |

**Example input**

| \[POST JSON\] {    "star": 4,    "ticket\_id": "VXA2RT" } |
| :---- |

**JSON output parameters**

| ชื่อ | คำอธิบาย | ตัวอย่าง |
| :---- | :---- | :---- |
| status | ผลการทำงาน ได้แก่\- success ไม่เกิดข้อผิดพลาดใด ๆ\- fail เกิดข้อผิดพลาด ไม่สามารถทำงานต่อได้\- warning เกิดข้อผิดพลาด แต่ยังสามารถทำงานต่อได้ | success |
| message | ข้อความอธิบายหากเกิดความผิดพลาด (status เป็น fail หรือ warning) | เช่น \- fail : “Invalid input JSON format | ข้อมูลรูปแบบ JSON ไม่ถูกต้อง” \- fail : “\[ชื่อ parameter\] is not specified | ไม่ได้ระบุ \[ชื่อ parameter\]” \- fail : “This token has expired | Token นี้หมดอายุแล้ว” \- fail : “ERR 1 \- Invalid token | Token ไม่ถูกต้อง” \- fail : “Running out of API usage quota | หมดโควต้าการใช้งาน API แล้ว” \- warning : “Almost running out of API usage quota | ใกล้หมดโควต้าการใช้งาน API แล้ว” |
| exec\_time | เวลาที่ใช้ในการประมวลผลข้อมูล (หน่วยเป็นวินาที) | 1.02s |
| credit\_balance | จำนวนครั้งการใช้งานที่เหลืออยู่ในเดือนนี้ | 820 |

**Example output**

| {    "status": "success",    "message": "",    "exec\_time": "5.176s",    "credit\_balance": null } |
| :---- |

## API comment (ให้ความเห็นเรื่องแจ้งหลังให้ดาว)

ใช้ส่งความเห็นเรื่องแจ้งหลังให้ดาว มาเข้าระบบ Traffy Fondue

**URL:** https://publicapi.traffy.in.th/exchange-api/comment/v1

**Input parameters**

| Method | POST JSON |  |  |
| :---- | :---- | :---- | :---- |
| **Authorization** | Bearer [Token](#api-get-auth-\(ขอ-jwt\)) |  |  |
| **ชื่อ** | **คำอธิบาย** | **ตัวอย่าง** | **REQUIRED?** |
| comment | ความเห็นหลังให้ดาว | ขอบคุณเจ้าหน้าที่ครับ | REQUIRED |
| ticket\_id | หมายเลขของเรื่องแจ้งที่ต้องการให้คะแนน (ไม่ต้องใส่เครื่องหมาย \#) | 2023-ABCDEF | REQUIRED (ถ้าไม่ได้ส่ง client\_ticket\_id มา) |
| client\_ticket\_id | หมายเลขเรื่องแจ้ง ตามที่เก็บไว้ในระบบต้นทาง | p123456789 | REQUIRED (ถ้าไม่ได้ส่ง ticket\_id มา) |

**Example input**

| \[POST JSON\] {    "comment": "ทดสอบให้ความเห็นหลังจากให้ดาวโดยทีมงาน Traffy Fondue",    "ticket\_id": "M6NGC2" } |
| :---- |

**JSON output parameters**

| ชื่อ | คำอธิบาย | ตัวอย่าง |
| :---- | :---- | :---- |
| status | ผลการทำงาน ได้แก่\- success ไม่เกิดข้อผิดพลาดใด ๆ\- fail เกิดข้อผิดพลาด ไม่สามารถทำงานต่อได้\- warning เกิดข้อผิดพลาด แต่ยังสามารถทำงานต่อได้ | success |
| message | ข้อความอธิบายหากเกิดความผิดพลาด (status เป็น fail หรือ warning) | เช่น \- fail : “Invalid input JSON format | ข้อมูลรูปแบบ JSON ไม่ถูกต้อง” \- fail : “\[ชื่อ parameter\] is not specified | ไม่ได้ระบุ \[ชื่อ parameter\]” \- fail : “This token has expired | Token นี้หมดอายุแล้ว” \- fail : “ERR 1 \- Invalid token | Token ไม่ถูกต้อง” \- fail : “Running out of API usage quota | หมดโควต้าการใช้งาน API แล้ว” \- warning : “Almost running out of API usage quota | ใกล้หมดโควต้าการใช้งาน API แล้ว” |
| exec\_time | เวลาที่ใช้ในการประมวลผลข้อมูล (หน่วยเป็นวินาที) | 1.02s |
| credit\_balance | จำนวนครั้งการใช้งานที่เหลืออยู่ในเดือนนี้ | 820 |

**Example output**

| {    "status": "success",    "message": "",    "exec\_time": "5.176s",    "credit\_balance": null } |
| :---- |

# **Traffy Fondue ส่งข้อมูลไปให้ระบบอื่น ๆ แบบ real-time** {#traffy-fondue-ส่งข้อมูลไปให้ระบบอื่น-ๆ-แบบ-real-time}

Traffy Fondue สามารถส่งข้อมูลออกไปให้ระบบภายนอกได้แบบ real-time เมื่อมีเรื่องแจ้งเข้ามาใหม่ หรือมีการอัปเดตสถานะ โดยจะไม่นับรวมกับจำนวนการใช้งาน (non credit\_balance)  
สามารถติดต่อสอบถาม การขอให้ส่งข้อมูลไปยังระบบของท่าน ได้ทางไลน์ @fonduehelp [https://lin.ee/pRPBNq2](https://lin.ee/pRPBNq2)

## เมื่อมีเรื่องแจ้งเข้ามาใหม่ {#เมื่อมีเรื่องแจ้งเข้ามาใหม่}

เมื่อมีเรื่องแจ้งใหม่ เข้ามาในหน่วยงานตามที่กำหนดใน account ของท่าน ทาง Traffy Fondue จะส่งข้อมูลการแจ้ง แบบ POST Method ไปทาง URL ตามที่ต้องการ ด้วยข้อมูลในรูปแบบ JSON ดังนี้

**URL:** (ติดต่อ Traffy เพื่อแจ้งตำแหน่ง URL รับข้อมูลเรื่องแจ้งใหม่ ในระบบของท่าน)

**JSON POST parameters**

| ชื่อ | คำอธิบาย | ตัวอย่าง |
| :---- | :---- | :---- |
| timestamp | วันเวลาที่แจ้ง (UTC+7) | 2023-01-01 01:02:03.000000 |
| ticket\_id | หมายเลขเรื่องแจ้งในระบบ Traffy Fondue | 2023-ABCDEF |
| description | ข้อความอธิบาย | ทดสอบการแจ้ง |
| type\_id | หมายเลขประเภทปัญหา (ดูที่ API [get-type-list](#api-get-type-list-\(ขอ-list-รายชื่อประเภทปัญหาที่หน่วยงานรับผิดชอบ\))) | 12 |
| type | ประเภทปัญหา | ถนน |
| photo | Array ของลิงค์ URL ของไฟล์รูปภาพ | \["https://storage.googleapis.com/traffy-fondue/line\_img/13565592441693.jpeg","...","..."\] |
| latitude | Latitude ของพิกัด GPS | 13.754158 |
| longitude | Longitude ของพิกัด GPS | 100.5014985 |
| address | ที่อยู่ไปรษณีย์ | 112 ถ.พหลโยธิน ต.คลองหนึ่ง อ.คลองหลวง จ.ปทุมธานี |
| orgs | Array หน่วยงานที่รับผิดชอบ | (Array) |
| \*\*ข้อมูลที่อยู่ใน orgs |  |  |
| org\_id | หมายเลขของหน่วยงาน | 151 |
| org | ชื่อหน่วยงาน | Traffy @ ITS Lab2 |

**Example POST parameters**

| \[POST JSON\] {     "timestamp": "2023-01-01 01:02:03.000000",     "ticket\_id": "2023-ABCDEF",     "description": "ทดสอบการแจ้ง",     "type\_id": 12,     "type": "ถนน",     "photo": \[         "https://storage.googleapis.com/traffy-fondue/line\_img/13565592441693.jpeg"     \],     "latitude": 13.754158,     "longitude": 100.5014985,     "address": "112 ถ.พหลโยธิน ต.คลองหนึ่ง อ.คลองหลวง จ.ปทุมธานี",     "orgs": \[         {             "org\_id": 151,             "org": "Traffy @ ITS Lab2"         },         {             "org\_id": 1302,             "org": "อบต.แอตแลนติส (ใช้ทดสอบ)"         },         {             ...         }     \] } |
| :---- |

## เมื่อมีการอัปเดตสถานะ {#เมื่อมีการอัปเดตสถานะ}

เมื่อมีการอัปเดตสถานะ ของเรื่องแจ้งในหน่วยงานตามที่กำหนดใน account ของท่าน ทาง Traffy Fondue จะส่งข้อมูลการอัปเดต แบบ PATCH Method ไปให้ URL ตามที่ต้องการ ด้วยข้อมูลในรูปแบบ JSON ดังนี้

**URL:** (ติดต่อ Traffy เพื่อแจ้งตำแหน่ง URL รับข้อมูลการอัปเดตสถานะ ในระบบของท่าน)

**JSON PATCH parameters**

| ชื่อ | คำอธิบาย | ตัวอย่าง |
| :---- | :---- | :---- |
| timestamp | วันเวลาที่อัปเดต (UTC+7) | 2023-01-01 18:02:03.000000 |
| ticket\_id | หมายเลขเรื่องแจ้งในระบบ Traffy Fondue | 2023-ABCDEF |
| org | ชื่อหน่วยงานที่เป็นผู้ดำเนินการ | Traffy @ ITS Lab2 |
| status | สถานะที่อัปเดต | เสร็จสิ้น |
| note | คำอธิบายประกอบการอัปเดต | ได้ทำการแก้ไขเสร็จสิ้นแล้วครับ |
| photo | Array ของลิงค์ URL ของไฟล์รูปภาพ | \["https://storage.googleapis.com/traffy-fondue/line\_img/13565592441693.jpeg","...","..."\] |

**Example PATCH parameters**

| \[PATCH JSON\] {     "timestamp": "2023-01-01 18:02:03.000000",     "ticket\_id": "2023-ABCDEF",     "org": "Traffy @ ITS Lab2",     "status": "เสร็จสิ้น",     "note": "ได้ทำการแก้ไขเสร็จสิ้นแล้วครับ",     "photo": \[         "https://storage.googleapis.com/traffy-fondue/line\_img/13565592441693.jpeg"     \] } |
| :---- |

[image1]: <data:image/png;base64,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>