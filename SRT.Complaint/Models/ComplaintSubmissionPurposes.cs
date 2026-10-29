namespace SRT.Complaint.Models;

public static class ComplaintSubmissionPurposes
{
    public const string ServiceImprovement = "ServiceImprovement";
    public const string InvestigationAndContact = "InvestigationAndContact";
    public const string ValidationPattern = "^(ServiceImprovement|InvestigationAndContact)$";

    public static bool IsValid(string? value)
        => value is ServiceImprovement or InvestigationAndContact;

    public static string Label(string? value) => value switch
    {
        ServiceImprovement => "แจ้งข้อมูลเพื่อปรับปรุงและพัฒนาบริการ",
        InvestigationAndContact => "ขอให้ตรวจสอบข้อเท็จจริงและติดต่อกลับ",
        _ => "ไม่ระบุ"
    };
}
