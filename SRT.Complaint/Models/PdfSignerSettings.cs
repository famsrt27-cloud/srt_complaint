namespace SRT.Complaint.Models;

public class PdfSignerSettings
{
    public int Id { get; set; }
    public string FullName { get; set; } = "นางสาวชัชชนก แสงมุข";
    public string PositionTitle { get; set; } = "หัวหน้ากองประชาสัมพันธ์";
    public string ActingTitle { get; set; } = "ปฏิบัติการแทน ผู้อำนวยการศูนย์ประชาสัมพันธ์";
    public string? SignatureStoredPath { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedById { get; set; }
    public StaffUser? UpdatedBy { get; set; }
}
