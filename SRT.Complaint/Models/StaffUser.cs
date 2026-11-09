#nullable enable
namespace SRT.Complaint.Models;

public class StaffUser
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool MustChangePassword { get; set; } = false;
    public DateTime? TempPasswordExpiresAt { get; set; }
    public byte[]? TempPasswordEncrypted { get; set; }
    public int AuthVersion { get; set; } = 1;

    public ICollection<Complaint> AssignedComplaints { get; set; } = new List<Complaint>();
}
