namespace SRT.Complaint.Services;

public sealed class EdocBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<EdocBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverStaleAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IEdocIntegrationService>();
                if (!service.IsEnabled) continue;
                await service.ProcessPendingAsync(5, stoppingToken);
                await service.TrackDueAsync(20, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "eDOC background cycle failed");
            }
        }
    }

    private async Task RecoverStaleAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IEdocIntegrationService>().RecoverStaleAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Unable to recover stale eDOC jobs at startup");
        }
    }
}
