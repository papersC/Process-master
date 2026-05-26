namespace ESEMS.Web.Services.AI;

/// <summary>
/// Background service that periodically refreshes the vector store index.
/// Runs every 30 minutes to keep the search index up-to-date.
/// </summary>
public class VectorSyncBackgroundService : BackgroundService
{
    private readonly VectorStoreService _vectorStore;
    private readonly ILogger<VectorSyncBackgroundService> _logger;

    public VectorSyncBackgroundService(VectorStoreService vectorStore, ILogger<VectorSyncBackgroundService> logger)
    {
        _vectorStore = vectorStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial index on startup (with delay to let DB migrations complete)
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_vectorStore.IsStale)
                {
                    _logger.LogInformation("Vector store is stale, re-indexing...");
                    await _vectorStore.IndexAllDataAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in vector sync background service");
            }

            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }
}
