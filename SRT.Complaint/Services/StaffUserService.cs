#nullable enable
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public class StaffUserService(AppDbContext db, IAuditService auditService, IMaskingService masking) : IStaffUserService
{
    private const string TempPasswordChars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
    private static readonly string[] AllowedRoles = ["GeneralOfficer", "CorruptionOfficer", "SuperAdmin"];

    public async Task<StaffUserOperationResult> CreateAsync(string employeeCode, string fullName, string? email,
        string role, int actorId, string actorCode, string ip)
    {
        var code = employeeCode.Trim();
        if (code.Length != 7 || !code.All(char.IsDigit))
            return new StaffUserOperationResult { Success = false, Error = "รหัสพนักงานต้องเป็นตัวเลข 7 หลัก" };

        if (string.IsNullOrWhiteSpace(fullName) || !AllowedRoles.Contains(role))
            return new StaffUserOperationResult { Success = false, Error = "กรุณากรอกข้อมูลให้ครบถ้วน" };

        if (await db.StaffUsers.AnyAsync(u => u.EmployeeCode == code))
            return new StaffUserOperationResult { Success = false, Error = $"รหัสพนักงาน {code} มีในระบบแล้ว" };

        var tempPassword = GenerateTempPassword();
        var expiresAt = DateTime.UtcNow.AddHours(8);
        var user = new StaffUser
        {
            EmployeeCode          = code,
            FullName              = fullName.Trim(),
            Email                 = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Role                  = role,
            PasswordHash          = BCrypt.Net.BCrypt.HashPassword(tempPassword, workFactor: 12),
            IsActive              = true,
            MustChangePassword    = true,
            TempPasswordExpiresAt = expiresAt,
            TempPasswordEncrypted = masking.Encrypt(tempPassword),
            CreatedAt             = DateTime.UtcNow
        };
        db.StaffUsers.Add(user);
        await db.SaveChangesAsync();
        await auditService.LogAsync("CreateStaffUser", actorId, actorCode,
            "StaffUser", user.Id.ToString(), new { user.EmployeeCode, user.FullName, user.Role }, ip);

        return new StaffUserOperationResult
        {
            Success = true, User = user, TempPassword = tempPassword, TempPasswordExpiresAt = expiresAt
        };
    }

    public async Task<StaffUserOperationResult> EditAsync(int userId, string fullName, string? email, string role,
        bool isActive, int actorId, string actorCode, string ip)
    {
        if (string.IsNullOrWhiteSpace(fullName) || !AllowedRoles.Contains(role))
            return new StaffUserOperationResult { Success = false, Error = "กรุณากรอกชื่อและบทบาท" };

        var user = await db.StaffUsers.FindAsync(userId);
        if (user == null)
            return new StaffUserOperationResult { Success = false, Error = "ไม่พบผู้ใช้งาน" };

        if (user.Id == actorId && (!isActive || role != user.Role))
            return new StaffUserOperationResult { Success = false, Error = "ไม่สามารถปิดบัญชีหรือเปลี่ยนบทบาทของบัญชีที่กำลังใช้งานอยู่" };

        if (user.Role == "SuperAdmin" && (role != "SuperAdmin" || !isActive) &&
            await db.StaffUsers.CountAsync(x => x.Role == "SuperAdmin" && x.IsActive) <= 1)
            return new StaffUserOperationResult { Success = false, Error = "ต้องมีผู้ดูแลระบบที่เปิดใช้งานอย่างน้อย 1 บัญชี" };

        var securityChanged = user.Role != role || user.IsActive != isActive;
        user.FullName = fullName.Trim();
        user.Email    = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        user.Role     = role;
        user.IsActive = isActive;
        if (securityChanged) user.AuthVersion++;
        await db.SaveChangesAsync();
        await auditService.LogAsync("EditStaffUser", actorId, actorCode,
            "StaffUser", user.Id.ToString(), new { user.FullName, user.Role, user.IsActive }, ip);

        return new StaffUserOperationResult { Success = true, User = user };
    }

    public async Task<StaffUserOperationResult> ResetPasswordAsync(int userId, int actorId, string actorCode, string ip)
    {
        var user = await db.StaffUsers.FindAsync(userId);
        if (user == null)
            return new StaffUserOperationResult { Success = false, Error = "ไม่พบผู้ใช้งาน" };

        var tempPassword = GenerateTempPassword();
        var expiresAt = DateTime.UtcNow.AddHours(8);
        user.PasswordHash          = BCrypt.Net.BCrypt.HashPassword(tempPassword, workFactor: 12);
        user.MustChangePassword    = true;
        user.TempPasswordExpiresAt = expiresAt;
        user.TempPasswordEncrypted = masking.Encrypt(tempPassword);
        user.AuthVersion++;
        await db.SaveChangesAsync();
        await auditService.LogAsync("ResetPassword", actorId, actorCode,
            "StaffUser", user.Id.ToString(), null, ip);

        return new StaffUserOperationResult
        {
            Success = true, User = user, TempPassword = tempPassword, TempPasswordExpiresAt = expiresAt
        };
    }

    public async Task<StaffUserOperationResult> ViewTempPasswordAsync(int userId)
    {
        var user = await db.StaffUsers.FindAsync(userId);
        if (user == null)
            return new StaffUserOperationResult { Success = false, Error = "ไม่พบผู้ใช้งาน" };

        if (!user.MustChangePassword || user.TempPasswordEncrypted == null)
            return new StaffUserOperationResult
            {
                Success = false,
                Error = "ไม่พบรหัสผ่านชั่วคราว (พนักงานอาจเปลี่ยนรหัสผ่านแล้ว หรือยังไม่เคยรีเซ็ต)"
            };

        if (user.TempPasswordExpiresAt.HasValue && user.TempPasswordExpiresAt.Value < DateTime.UtcNow)
            return new StaffUserOperationResult { Success = false, Error = "รหัสผ่านชั่วคราวหมดอายุแล้ว กรุณารีเซ็ตรหัสผ่านใหม่" };

        return new StaffUserOperationResult
        {
            Success = true,
            User = user,
            TempPassword = masking.Decrypt(user.TempPasswordEncrypted),
            TempPasswordExpiresAt = user.TempPasswordExpiresAt
        };
    }

    public async Task<StaffUserOperationResult> ToggleActiveAsync(int userId, int actorId, string actorCode, string ip)
    {
        var user = await db.StaffUsers.FindAsync(userId);
        if (user == null)
            return new StaffUserOperationResult { Success = false, Error = "ไม่พบผู้ใช้งาน" };

        if (user.Id == actorId)
            return new StaffUserOperationResult { Success = false, Error = "ไม่สามารถปิดบัญชีที่กำลังใช้งานอยู่" };

        if (user.Role == "SuperAdmin" && user.IsActive &&
            await db.StaffUsers.CountAsync(x => x.Role == "SuperAdmin" && x.IsActive) <= 1)
            return new StaffUserOperationResult { Success = false, Error = "ไม่สามารถปิด SuperAdmin คนสุดท้ายได้" };

        user.IsActive = !user.IsActive;
        user.AuthVersion++;
        await db.SaveChangesAsync();
        await auditService.LogAsync(user.IsActive ? "ActivateStaffUser" : "DeactivateStaffUser",
            actorId, actorCode, "StaffUser", userId.ToString(), null, ip);

        return new StaffUserOperationResult { Success = true, User = user };
    }

    private static string GenerateTempPassword() => new(Enumerable.Range(0, 8)
        .Select(_ => TempPasswordChars[RandomNumberGenerator.GetInt32(TempPasswordChars.Length)])
        .ToArray());
}
