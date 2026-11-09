namespace SRT.Complaint.Models;

public static class ComplaintWorkflowSteps
{
    public const string Received = "Received";
    public const string Screening = "Screening";
    public const string WaitingInfo = "WaitingInfo";
    public const string Classification = "Classification";
    public const string Investigation = "Investigation";
    public const string Summary = "Summary";
    public const string Notification = "Notification";
    public const string NotificationRetry = "NotificationRetry";
    public const string Completed = "Completed";
    public const string Rejected = "Rejected";

    public static int Number(string? step) => step switch
    {
        Received => 1,
        Screening or WaitingInfo => 2,
        Classification => 3,
        Investigation => 4,
        Summary => 5,
        Notification or NotificationRetry => 6,
        Completed or Rejected => 7,
        _ => 1
    };

    public static string ThaiLabel(string? step) => step switch
    {
        Received => "รับเรื่องแล้ว",
        Screening => "รอตรวจสอบข้อมูล",
        WaitingInfo => "รอข้อมูลเพิ่มเติม",
        Classification => "กำลังจำแนกเรื่อง",
        Investigation => "กำลังดำเนินการ",
        Summary => "พร้อมสรุปผล",
        Notification => "รอแจ้งผล",
        NotificationRetry => "รอแจ้งผลอีกครั้ง",
        Completed => "ปิดเรื่องแล้ว",
        Rejected => "ยุติเรื่อง",
        _ => "รับเรื่องแล้ว"
    };

    public static string Title(int number) => number switch
    {
        1 => "รับเรื่องร้องเรียน",
        2 => "ตรวจสอบความครบถ้วน",
        3 => "จำแนกและวางแผนดำเนินการ",
        4 => "ตรวจสอบข้อเท็จจริงและดำเนินการ",
        5 => "สรุปผลและตรวจความครบถ้วน",
        6 => "แจ้งผลและปิดเรื่อง",
        _ => "ดำเนินการเสร็จสิ้น"
    };
}
