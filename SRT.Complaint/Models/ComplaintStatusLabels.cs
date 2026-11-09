namespace SRT.Complaint.Models;

public static class ComplaintStatusLabels
{
    public static string Thai(string? status) => status switch
    {
        "Pending" => "รอรับเรื่อง",
        "InProgress" => "กำลังดำเนินการ",
        "WaitingInfo" => "รอข้อมูลเพิ่มเติม",
        "Forwarded" => "ส่งต่อหน่วยงาน",
        "UnderReview" => "อยู่ระหว่างพิจารณา",
        "Resolved" => "พร้อมสรุปผล",
        "Closed" => "ปิดเรื่องแล้ว",
        "Rejected" => "ยุติเรื่อง",
        _ => "ไม่ทราบสถานะ"
    };
}
