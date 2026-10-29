namespace SRT.Complaint.Services;

public class WebhookRetryService(
    IServiceScopeFactory scopeFactory,
    ILogger<WebhookRetryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
            try
            {
                using var scope = scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IWebhookService>();
                await svc.DispatchPendingOutboxAsync(stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "Webhook retry batch failed");
            }
        }
    }
}
