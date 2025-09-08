using ElasticSearchApi.Application.Interfaces;

namespace ElasticSearchApi.Api.Services;

public class StartupIndexingService(
    IServiceProvider serviceProvider,
    ILogger<StartupIndexingService> logger) : BackgroundService
{
    private const string CsvFilePath = "data.csv";
    private const string IndexName = "products";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Starting automatic product indexing...");

            // Wait a bit for all services to be ready
            await Task.Delay(5000, stoppingToken);

            using var scope = serviceProvider.CreateScope();
            var productSearchService = scope.ServiceProvider.GetRequiredService<IProductSearchService>();

            logger.LogInformation("Checking if products need to be indexed...");
            
            // Index products
            var result = await productSearchService.IndexProductsFromCsvAsync(
                CsvFilePath, IndexName, stoppingToken);

            if (result.Success)
            {
                logger.LogInformation("Automatic indexing completed successfully. Indexed {Count} products.", 
                    result.TotalProductsIndexed);
            }
            else
            {
                logger.LogError("Automatic indexing failed: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during automatic indexing");
        }
    }
}