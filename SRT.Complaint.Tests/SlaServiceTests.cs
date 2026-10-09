using SRT.Complaint.Models;
using SRT.Complaint.Services;
using Xunit;

namespace SRT.Complaint.Tests;

public class SlaServiceTests
{
    [Fact]
    public async Task CalculateDeadlineAsync_UsesConfiguredHours_WhenPriorityConfigExists()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.SlaConfigs.Add(new SlaConfig { Priority = "Urgent", ResolutionHours = 24 });
        await db.SaveChangesAsync();
        var service = new SlaService(db);
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var deadline = await service.CalculateDeadlineAsync("Urgent", from);

        Assert.Equal(from.AddHours(24), deadline);
    }

    [Fact]
    public async Task CalculateDeadlineAsync_FallsBackTo168Hours_WhenNoConfigForPriority()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        var service = new SlaService(db);
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var deadline = await service.CalculateDeadlineAsync("Unknown", from);

        Assert.Equal(from.AddHours(168), deadline);
    }
}
