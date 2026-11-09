using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using SRT.Complaint.Data;
using SRT.Complaint.Models;
using SRT.Complaint.Pages.Admin;
using SRT.Complaint.Services;

namespace SRT.Complaint.Tests;

public class DepartmentsPageTests
{
    [Fact]
    public async Task Edit_RenamesReferencesInCategoriesAndComplaints()
    {
        await using var db = CreateDb();
        var department = new ComplaintDepartment { Name = "ฝ่ายเดิม", SortOrder = 1 };
        db.ComplaintDepartments.Add(department);
        db.ComplaintCategories.Add(new ComplaintCategory { Name = "หมวดทดสอบ", DepartmentName = "ฝ่ายเดิม" });
        db.Complaints.Add(new SRT.Complaint.Models.Complaint
        {
            ReferenceNumber = "TEST-001",
            ReporterName = "ผู้ทดสอบ",
            ReporterPhone = "0812345678",
            Description = "รายละเอียดทดสอบ",
            ResponsibleDepartment = "ฝ่ายเดิม"
        });
        await db.SaveChangesAsync();

        var model = CreateModel(db);
        model.EditDepartmentId = department.Id;
        model.EditName = "ฝ่ายใหม่";
        model.EditDescription = "ปรับชื่อฝ่าย";
        model.EditSortOrder = 2;

        await model.OnPostEditAsync();

        Assert.Equal("ฝ่ายใหม่", (await db.ComplaintCategories.SingleAsync()).DepartmentName);
        Assert.Equal("ฝ่ายใหม่", (await db.Complaints.SingleAsync()).ResponsibleDepartment);
        Assert.Equal("ฝ่ายใหม่", (await db.ComplaintDepartments.SingleAsync()).Name);
    }

    [Fact]
    public async Task Delete_WhenReferenced_KeepsDepartmentAndShowsError()
    {
        await using var db = CreateDb();
        var department = new ComplaintDepartment { Name = "ฝ่ายที่ถูกใช้", SortOrder = 1 };
        db.ComplaintDepartments.Add(department);
        db.ComplaintCategories.Add(new ComplaintCategory { Name = "หมวดทดสอบ", DepartmentName = department.Name });
        await db.SaveChangesAsync();

        var model = CreateModel(db);

        await model.OnPostDeleteAsync(department.Id);

        Assert.True(await db.ComplaintDepartments.AnyAsync(x => x.Id == department.Id));
        Assert.Contains("ปิดใช้งานแทน", model.TempData["Error"]?.ToString());
    }

    [Fact]
    public async Task ToggleActive_ChangesAvailabilityWithoutDeletingDepartment()
    {
        await using var db = CreateDb();
        var department = new ComplaintDepartment { Name = "ฝ่ายทดสอบ", IsActive = true, SortOrder = 1 };
        db.ComplaintDepartments.Add(department);
        await db.SaveChangesAsync();

        var model = CreateModel(db);

        await model.OnPostToggleActiveAsync(department.Id);

        Assert.False((await db.ComplaintDepartments.SingleAsync()).IsActive);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static DepartmentsModel CreateModel(AppDbContext db)
    {
        var audit = new Mock<IAuditService>();
        audit.Setup(x => x.LogAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim("EmployeeCode", "0000001"),
                new Claim(ClaimTypes.Role, "SuperAdmin")
            ], "Test"))
        };
        var tempDataProvider = new Mock<ITempDataProvider>();
        tempDataProvider.Setup(x => x.LoadTempData(httpContext)).Returns(new Dictionary<string, object>());

        return new DepartmentsModel(db, audit.Object)
        {
            PageContext = new PageContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, tempDataProvider.Object)
        };
    }
}
