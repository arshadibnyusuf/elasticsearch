using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElasticSearchApi.Application.Interfaces;
using ElasticSearchApi.Application.Models;
using ElasticSearchApi.Domain.Entities;
using ElasticSearchApi.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ElasticSearchApi.Application.Services;

public class ProductSearchService : IProductSearchService
{
    private readonly IProductRepository _productRepository;
    private readonly ICsvParserService _csvParserService;
    private readonly ILogger<ProductSearchService> _logger;
    private const string SettingsPath = "elasticsearch/settings.json";
    private const string MappingsPath = "elasticsearch/mappings.json";

    public ProductSearchService(
        IProductRepository productRepository,
        ICsvParserService csvParserService,
        ILogger<ProductSearchService> logger)
    {
        _productRepository = productRepository;
        _csvParserService = csvParserService;
        _logger = logger;
    }

    public async Task<IndexResult> IndexProductsFromCsvAsync(string csvFilePath, string indexName, CancellationToken cancellationToken = default)
    {
        var result = new IndexResult();
        
        try
        {
            // Check if CSV file exists
            if (!File.Exists(csvFilePath))
            {
                var error = $"CSV file not found at {csvFilePath}";
                _logger.LogError(error);
                result.Success = false;
                result.ErrorMessage = error;
                return result;
            }

            // Check if index exists, if not create it with settings and mappings
            var indexExists = await _productRepository.IndexExistsAsync(indexName, cancellationToken);
            if (!indexExists)
            {
                _logger.LogInformation("Index {IndexName} does not exist. Creating with custom settings and mappings", indexName);
                
                // Check if settings and mappings files exist
                if (!File.Exists(SettingsPath) || !File.Exists(MappingsPath))
                {
                    var error = $"Settings or mappings file not found. Expected at {SettingsPath} and {MappingsPath}";
                    _logger.LogError(error);
                    result.Success = false;
                    result.ErrorMessage = error;
                    return result;
                }

                using var settingsStream = File.OpenRead(SettingsPath);
                using var mappingsStream = File.OpenRead(MappingsPath);
                
                var created = await _productRepository.CreateIndexAsync(
                    indexName, settingsStream, mappingsStream, cancellationToken);
                
                if (!created)
                {
                    var error = $"Failed to create index {indexName}";
                    _logger.LogError(error);
                    result.Success = false;
                    result.ErrorMessage = error;
                    return result;
                }
            }

            // Parse CSV file
            _logger.LogInformation("Starting CSV parsing from {FilePath}", csvFilePath);
            var products = await _csvParserService.ParseProductsAsync(csvFilePath);
            var productsList = products.ToList();
            result.TotalProductsParsed = productsList.Count;
            _logger.LogInformation("CSV parsing complete. Parsed {Count} products", productsList.Count);

            // Index products
            _logger.LogInformation("Starting bulk indexing of {Count} products to {IndexName}", productsList.Count, indexName);
            var indexed = await _productRepository.IndexProductsAsync(indexName, productsList, cancellationToken);

            if (!indexed)
            {
                var error = "Failed to index products to Elasticsearch";
                _logger.LogError(error);
                result.Success = false;
                result.ErrorMessage = error;
                return result;
            }

            _logger.LogInformation("Successfully indexed {Count} products from CSV to {IndexName}", productsList.Count, indexName);
            result.Success = true;
            result.TotalProductsIndexed = productsList.Count;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing products from CSV");
            result.Success = false;
            result.ErrorMessage = $"An error occurred while indexing products: {ex.Message}";
            result.Errors.Add(ex.ToString());
            return result;
        }
    }

    public async Task<List<Product>> SearchProductsAsync(string indexName, string searchTerm, int pageSize, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                _logger.LogWarning("Search term is empty");
                return new List<Product>();
            }

            if (pageSize <= 0 || pageSize > 100)
            {
                pageSize = 20; // Default page size
            }

            _logger.LogInformation("Searching for '{SearchTerm}' in index {IndexName} with page size {PageSize}", 
                searchTerm, indexName, pageSize);

            var results = await _productRepository.LayeredSearchAsync(indexName, searchTerm, pageSize, cancellationToken);

            _logger.LogInformation("Search completed. Found {Count} products", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching products");
            throw;
        }
    }
}