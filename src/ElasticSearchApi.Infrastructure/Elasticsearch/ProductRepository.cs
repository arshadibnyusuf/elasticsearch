using Elasticsearch.Net;
using ElasticSearchApi.Domain.Entities;
using ElasticSearchApi.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ElasticSearchApi.Infrastructure.Elasticsearch;

public class ProductRepository(
    IElasticClient client,
    ILayeredQueryBuilder queryBuilder,
    ILogger<ProductRepository> logger) : IProductRepository
{

    public async Task<bool> CreateIndexAsync(string indexName, Stream settingsJson, Stream mappingsJson, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if index already exists
            var existsResponse = await client.Indices.ExistsAsync(indexName, ct: cancellationToken);
            if (existsResponse.Exists)
            {
                logger.LogInformation("Index {IndexName} already exists", indexName);
                return true;
            }

            // Read settings and mappings
            using var settingsReader = new StreamReader(settingsJson);
            using var mappingsReader = new StreamReader(mappingsJson);
            
            var settingsJsonString = await settingsReader.ReadToEndAsync();
            var mappingsJsonString = await mappingsReader.ReadToEndAsync();

            var settings = JObject.Parse(settingsJsonString);
            var mappings = JObject.Parse(mappingsJsonString);

            // Create index with settings and mappings
            var indexBody = JsonConvert.SerializeObject(new
            {
                settings = settings,
                mappings = mappings
            });
            
            var createIndexResponse = await client.LowLevel.Indices.CreateAsync<StringResponse>(
                indexName,
                PostData.String(indexBody),
                ctx: cancellationToken);

            if (!createIndexResponse.Success)
            {
                var errorDetails = string.Empty;
                if (!string.IsNullOrEmpty(createIndexResponse.Body))
                {
                    errorDetails = createIndexResponse.Body;
                }
                
                logger.LogError("Failed to create index {IndexName}: {Error} - Response: {Response}", 
                    indexName, 
                    createIndexResponse.OriginalException?.Message ?? "Unknown error",
                    errorDetails);
                return false;
            }

            logger.LogInformation("Successfully created index {IndexName} with custom settings and mappings", indexName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating index {IndexName}", indexName);
            return false;
        }
    }

    public async Task<bool> IndexProductsAsync(string indexName, IEnumerable<Product> products, CancellationToken cancellationToken = default)
    {
        try
        {
            var productList = products.ToList();
            if (!productList.Any())
            {
                logger.LogWarning("No products to index");
                return true;
            }
            
            // Verify index exists
            var indexExists = await client.Indices.ExistsAsync(indexName, ct: cancellationToken);
            if (!indexExists.Exists)
            {
                logger.LogError("Index {IndexName} does not exist. Cannot index products.", indexName);
                return false;
            }
            
            // Check if index already has data
            var countResponse = await client.CountAsync<Product>(c => c.Index(indexName), cancellationToken);
            if (countResponse.IsValid && countResponse.Count > 0)
            {
                logger.LogInformation("Index {IndexName} already contains {Count} documents. Skipping indexing.", indexName, countResponse.Count);
                return true;
            }

            // Bulk index in batches of 500
            const int batchSize = 500;
            var totalIndexed = 0;
            var totalBatches = (int)Math.Ceiling((double)productList.Count / batchSize);
            
            for (int i = 0; i < productList.Count; i += batchSize)
            {
                var currentBatch = (i / batchSize) + 1;
                logger.LogInformation("Processing batch {CurrentBatch}/{TotalBatches} (items {StartIndex}-{EndIndex} of {Total})", 
                    currentBatch, totalBatches, i + 1, Math.Min(i + batchSize, productList.Count), productList.Count);
                
                var batchList = productList.Skip(i).Take(batchSize).ToList();
                logger.LogDebug("Batch {CurrentBatch} contains {Count} items. First ID: {FirstId}, Last ID: {LastId}", 
                    currentBatch, batchList.Count, 
                    batchList.FirstOrDefault()?.Id ?? "N/A", 
                    batchList.LastOrDefault()?.Id ?? "N/A");
                
                var bulkResponse = await client.BulkAsync(b => b
                    .Index(indexName)
                    .IndexMany(batchList, (descriptor, product) => descriptor
                        .Id(product.Id)
                        .Document(product)), cancellationToken);

                // Check if there are actual errors (not 201 Created responses)
                var hasRealErrors = false;
                if (bulkResponse.Items != null)
                {
                    foreach (var item in bulkResponse.Items)
                    {
                        // Status codes 200-299 are success, 400+ are errors
                        if (item.Status >= 400)
                        {
                            hasRealErrors = true;
                            logger.LogError("Failed to index document {Id} - Status: {Status}, Error: {Error}",
                                item.Id, item.Status, item.Error?.ToString() ?? "No error details");
                        }
                    }
                }
                
                if (hasRealErrors)
                {
                    logger.LogError("Some documents failed to index in batch {CurrentBatch}", currentBatch);
                    return false;
                }
                
                // If we get here, all documents were indexed successfully (even if status was 201)

                totalIndexed += batchList.Count;
                logger.LogInformation("Batch {CurrentBatch}/{TotalBatches} completed successfully. Total indexed so far: {TotalIndexed}/{Total} products", 
                    currentBatch, totalBatches, totalIndexed, productList.Count);
                
                // Add a small delay between batches to avoid overwhelming ES
                if (currentBatch < totalBatches)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }

            logger.LogInformation("Successfully indexed all {Count} products", productList.Count);
            
            // Verify the count in the index
            await Task.Delay(500, cancellationToken); // Give ES time to refresh
            var finalCountResponse = await client.CountAsync<Product>(c => c.Index(indexName), cancellationToken);
            if (finalCountResponse.IsValid)
            {
                logger.LogInformation("Verification: Index {IndexName} now contains {Count} documents", indexName, finalCountResponse.Count);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error bulk indexing products");
            return false;
        }
    }

    public async Task<List<Product>> LayeredSearchAsync(string indexName, string searchTerm, int pageSize, CancellationToken cancellationToken = default)
    {
        try
        {
            // Build multi-search query
            var multiSearchQuery = queryBuilder.BuildMultiSearchQuery(indexName, searchTerm, pageSize);
            
            // Execute multi-search
            var response = await client.LowLevel.MultiSearchAsync<StringResponse>(
                PostData.String(multiSearchQuery),
                ctx: cancellationToken);

            if (!response.Success)
            {
                logger.LogError("Multi-search failed: {Error}", response.OriginalException?.Message);
                throw new InvalidOperationException($"Search failed: {response.OriginalException?.Message}");
            }

            // Parse response
            var msearchResponse = JsonConvert.DeserializeObject<MultiSearchResponse>(response.Body);
            if (msearchResponse == null)
            {
                throw new InvalidOperationException("Failed to parse multi-search response");
            }
            
            List<Product> allProducts = [];
            HashSet<string> addedIds = [];

            foreach (var searchResponse in msearchResponse.Responses)
            {
                if (searchResponse.Error != null)
                {
                    logger.LogWarning("Layer search error: {Error}", searchResponse.Error.Reason);
                    continue;
                }

                foreach (var hit in searchResponse.Hits.Hits)
                {
                    var product = JsonConvert.DeserializeObject<Product>(hit.Source.ToString());
                    if (product != null && !string.IsNullOrEmpty(product.Id) && addedIds.Add(product.Id))
                    {
                        allProducts.Add(product);
                    }
                }
            }

            logger.LogInformation("Layered search completed. Found {Count} unique products", allProducts.Count);
            return allProducts;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing layered search");
            throw;
        }
    }

    public async Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.Indices.ExistsAsync(indexName, ct: cancellationToken);
            return response.Exists;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if index {IndexName} exists", indexName);
            return false;
        }
    }

    // Helper classes for response parsing
    private class MultiSearchResponse
    {
        [JsonProperty("responses")]
        public List<SearchResponse> Responses { get; set; } = [];
    }

    private class SearchResponse
    {
        [JsonProperty("error")]
        public ErrorDetails? Error { get; set; }

        [JsonProperty("hits")]
        public HitsContainer Hits { get; set; } = new();
    }

    private class ErrorDetails
    {
        [JsonProperty("reason")]
        public string Reason { get; set; } = string.Empty;
    }

    private class HitsContainer
    {
        [JsonProperty("hits")]
        public List<Hit> Hits { get; set; } = [];
    }

    private class Hit
    {
        [JsonProperty("_source")]
        public JObject Source { get; set; } = new();
    }
}