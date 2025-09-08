using ElasticSearchApi.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ElasticSearchApi.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(
    IProductSearchService productSearchService,
    ILogger<ProductsController> logger) : ControllerBase
{
    private const string DefaultIndexName = "products";
    private const string CsvFilePath = "data.csv";

    /// <summary>
    /// Indexes products from the data.csv file into Elasticsearch
    /// </summary>
    [HttpPost("index")]
    public async Task<IActionResult> IndexProducts(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting product indexing from CSV");
            
            var result = await productSearchService.IndexProductsFromCsvAsync(
                CsvFilePath, DefaultIndexName, cancellationToken);

            if (result.Success)
            {
                return Ok(new 
                { 
                    message = "Products indexed successfully", 
                    index = DefaultIndexName,
                    totalProductsParsed = result.TotalProductsParsed,
                    totalProductsIndexed = result.TotalProductsIndexed
                });
            }

            return StatusCode(500, new 
            { 
                message = "Failed to index products",
                error = result.ErrorMessage,
                details = result.Errors
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error indexing products");
            return StatusCode(500, new 
            { 
                message = "An unexpected error occurred while indexing products", 
                error = ex.Message,
                details = new[] { ex.ToString() }
            });
        }
    }

    /// <summary>
    /// Searches for products using layered query approach
    /// </summary>
    /// <param name="q">Search term</param>
    /// <param name="size">Page size (default: 20, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("search")]
    public async Task<IActionResult> SearchProducts(
        [FromQuery] string q, 
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest(new { message = "Search term 'q' is required" });
            }

            if (size <= 0 || size > 100)
            {
                size = 20;
            }

            logger.LogInformation("Searching for products with term: {SearchTerm}, size: {Size}", q, size);
            
            var results = await productSearchService.SearchProductsAsync(
                DefaultIndexName, q, size, cancellationToken);

            return Ok(new
            {
                query = q,
                count = results.Count,
                results = results
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching products");
            return StatusCode(500, new { message = "An error occurred while searching products", error = ex.Message });
        }
    }
}