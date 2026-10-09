#nullable enable
using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public class StaffUserOperationResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public StaffUser? User { get; init; }
    public string? TempPassword { get; init; }
    public DateTime? TempPasswordExpiresAt { get; init; }
}

public interface IStaffUserService
{
    Task<StaffUserOperationResult> CreateAsync(string employeeCode, string fullName, string? email, string role,
        int actorId, string actorCode, string ip);

    Task<StaffUserOperationResult> EditAsync(int userId, string fullName, string? email, string role, bool isActive,
        int actorId, string actorCode, string ip);

    Task<StaffUserOperationResult> ResetPasswordAsync(int userId, int actorId, string actorCode, string ip);

    Task<StaffUserOperationResult> ViewTempPasswordAsync(int userId);

    Task<StaffUserOperationResult> ToggleActiveAsync(int userId, int actorId, string actorCode, string ip);
}
