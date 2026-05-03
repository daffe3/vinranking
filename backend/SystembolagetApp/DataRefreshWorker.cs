using SystembolagetApp.Services;

namespace SystembolagetApp;

public class DataRefreshWorker : BackgroundService
{
    private readonly ILogger<DataRefreshWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public DataRefreshWorker(ILogger<DataRefreshWorker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataRefreshWorker startad");
        await RunCycleAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            await RunAiCycleAsync(stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var fetcher = scope.ServiceProvider.GetRequiredService<SystembolagetFetcherService>();
            await fetcher.FetchAndSaveAsync(ct);

            var analyzer = scope.ServiceProvider.GetRequiredService<AiAnalyzerService>();
            await analyzer.AnalyzeUnanalyzedProductsAsync(batchSize: 100, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel i DataRefreshWorker");
        }
    }

    private async Task RunAiCycleAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var analyzer = scope.ServiceProvider.GetRequiredService<AiAnalyzerService>();
            await analyzer.AnalyzeUnanalyzedProductsAsync(batchSize: 100, ct);

            var vivino = scope.ServiceProvider.GetRequiredService<VivinoService>();
            await vivino.EnrichWithVivinoAsync(batchSize: 50, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel i AI-analyscykel");
        }
    }
}
