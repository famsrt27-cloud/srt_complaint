using Moq;
using SRT.Complaint.Models;
using SRT.Complaint.Services;
using Xunit;

namespace SRT.Complaint.Tests;

public class StaffUserServiceTests
{
    private static StaffUserService CreateService(
        Data.AppDbContext db, out Mock<IAuditService> audit, out Mock<IMaskingService> masking)
    {
        audit = new Mock<IAuditService>();
        masking = new Mock<IMaskingService>();
        masking.Setup(m => m.Encrypt(It.IsAny<string>()))
            .Returns((string s) => System.Text.Encoding.UTF8.GetBytes(s));
        masking.Setup(m => m.Decrypt(It.IsAny<byte[]>()))
            .Returns((byte[] b) => System.Text.Encoding.UTF8.GetString(b));
        return new StaffUserService(db, audit.Object, masking.Object);
    }

    [Fact]
    public async Task CreateAsync_RejectsNonSevenDigitEmployeeCode()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        var service = CreateService(db, out _, out _);

        var result = await service.CreateAsync("123", "Test User", null, "GeneralOfficer", 1, "0000001", "127.0.0.1");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateEmployeeCode()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.StaffUsers.Add(new StaffUser { EmployeeCode = "1234567", FullName = "Existing", Role = "GeneralOfficer" });
        await db.SaveChangesAsync();
        var service = CreateService(db, out _, out _);

        var result = await service.CreateAsync("1234567", "New User", null, "GeneralOfficer", 1, "0000001", "127.0.0.1");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateAsync_Succeeds_HashesPasswordAndEncryptsTempPassword()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        var service = CreateService(db, out var audit, out var masking);

        var result = await service.CreateAsync("1234567", "New User", "a@b.com", "GeneralOfficer", 1, "0000001", "127.0.0.1");

        Assert.True(result.Success);
        Assert.NotNull(result.TempPassword);
        Assert.NotEqual(result.TempPassword, result.User!.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(result.TempPassword, result.User.PasswordHash));
        masking.Verify(m => m.Encrypt(result.TempPassword!), Times.Once);
        audit.Verify(a => a.LogAsync("CreateStaffUser", 1, "0000001", "StaffUser", It.IsAny<string>(),
            It.IsAny<object>(), "127.0.0.1", default), Times.Once);
    }

    [Fact]
    public async Task ViewTempPasswordAsync_ReturnsError_WhenExpired()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.StaffUsers.Add(new StaffUser
        {
            EmployeeCode = "1234567",
            FullName = "Expired User",
            Role = "GeneralOfficer",
            MustChangePassword = true,
            TempPasswordEncrypted = System.Text.Encoding.UTF8.GetBytes("secret"),
            TempPasswordExpiresAt = DateTime.UtcNow.AddHours(-1)
        });
        await db.SaveChangesAsync();
        var user = db.StaffUsers.Single();
        var service = CreateService(db, out _, out _);

        var result = await service.ViewTempPasswordAsync(user.Id);

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData("edit")]
    [InlineData("reset")]
    [InlineData("view")]
    [InlineData("toggle")]
    public async Task AllMutations_ReturnFriendlyError_ForMissingUser(string op)
    {
        await using var db = TestHelpers.CreateAppDbContext();
        var service = CreateService(db, out _, out _);
        const int missingId = 999;

        var result = op switch
        {
            "edit" => await service.EditAsync(missingId, "name", null, "GeneralOfficer", true, 1, "0000001", "ip"),
            "reset" => await service.ResetPasswordAsync(missingId, 1, "0000001", "ip"),
            "view" => await service.ViewTempPasswordAsync(missingId),
            "toggle" => await service.ToggleActiveAsync(missingId, 1, "0000001", "ip"),
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };

        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }
}
