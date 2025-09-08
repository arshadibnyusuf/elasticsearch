using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ElasticSearchApi.Application.Interfaces;

namespace ElasticSearchApi.Api.Services;

public class StartupIndexingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StartupIndexingService> _logger;
    private const string CsvFilePath = "data.csv";
    private const string IndexName = "products";

    public StartupIndexingService(
        IServiceProvider serviceProvider,
        ILogger<StartupIndexingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting automatic product indexing...");

            // Wait a bit for all services to be ready
            await Task.Delay(5000, stoppingToken);

            using var scope = _serviceProvider.CreateScope();
            var productSearchService = scope.ServiceProvider.GetRequiredService<IProductSearchService>();

            _logger.LogInformation("Checking if products need to be indexed...");
            
            // Index products
            var result = await productSearchService.IndexProductsFromCsvAsync(
                CsvFilePath, IndexName, stoppingToken);

            if (result.Success)
            {
                _logger.LogInformation("Automatic indexing completed successfully. Indexed {Count} products.", 
                    result.TotalProductsIndexed);
            }
            else
            {
                _logger.LogError("Automatic indexing failed: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during automatic indexing");
        }
    }
}