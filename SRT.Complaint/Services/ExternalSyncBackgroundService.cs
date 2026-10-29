namespace SRT.Complaint.Services;

/// <summary>
/// Runs a daily pull sync for every configured external system so issues are still imported
/// when staff do not click "ดึงคำร้อง" manually.
/// </summary>
public class ExternalSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExternalSyncBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await RunSyncAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Scheduled external sync failed");
            }
        }
    }

    private async Task RunSyncAsync(IServiceProvider services, CancellationToken ct)
    {
        var externalSyncService = services.GetRequiredService<IExternalSyncService>();

        foreach (var system in externalSyncService.GetAvailableSystems())
        {
            if (!system.IsConfigured) continue;

            try
            {
                var log = await externalSyncService.SyncAsync(system.SystemKey, triggeredById: null, ct);
                logger.LogInformation(
                    "Scheduled sync for {System}: {Status}, fetched={Fetched} new={New} dup={Dup} err={Err}",
                    system.SystemKey, log.SyncStatus, log.FetchedCount, log.NewCount, log.DuplicateCount, log.ErrorCount);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled sync failed for {System}", system.SystemKey);
            }
        }
    }
}
