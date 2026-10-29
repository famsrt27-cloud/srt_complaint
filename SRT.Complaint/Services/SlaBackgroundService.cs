using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;

namespace SRT.Complaint.Services;

public class SlaBackgroundService(IServiceScopeFactory scopeFactory, ILogger<SlaBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await RunChecksAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "SLA background check failed");
            }
        }
    }

    private static async Task RunChecksAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var notificationService = services.GetRequiredService<INotificationService>();
        var now = DateTime.UtcNow;

        // Mark SLA breached
        var breached = await db.Complaints
            .Include(c => c.AssignedTo)
            .Where(c => !c.SlaBreached && c.SlaDeadline.HasValue && c.SlaDeadline < now
                        && c.Status != "Closed" && c.Status != "Rejected")
            .ToListAsync(ct);
        foreach (var c in breached)
            c.SlaBreached = true;

        if (breached.Count > 0)
            await db.SaveChangesAsync(ct);

        // Notify the assigned officer that SLA has been breached (idempotent — SlaBreached flag prevents repeat sends)
        foreach (var c in breached)
        {
            if (string.IsNullOrEmpty(c.AssignedTo?.Email)) continue;
            await notificationService.SendAsync("SlaBreached", null, c.AssignedTo.Email, new()
            {
                ["ReferenceNumber"] = c.ReferenceNumber
            }, ct);
        }
    }
}
