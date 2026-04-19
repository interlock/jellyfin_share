using Jellyfin.Plugin.MediaShare.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaShare.Services;

public class SyncScheduler(
    FederationService fedService,
    ILogger<SyncScheduler> logger) : IAsyncBackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await fedService.TriggerSyncAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sync scheduler failed");
            }
            await Task.Delay(_interval, stoppingToken);
        }
    }
}

public interface IAsyncBackgroundService
{
    Task ExecuteAsync(CancellationToken stoppingToken);
}