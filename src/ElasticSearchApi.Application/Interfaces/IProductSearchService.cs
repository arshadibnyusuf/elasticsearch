using ElasticSearchApi.Application.Models;
using ElasticSearchApi.Domain.Entities;

namespace ElasticSearchApi.Application.Interfaces;

public interface IProductSearchService
{
    Task<IndexResult> IndexProductsFromCsvAsync(string csvFilePath, string indexName, CancellationToken cancellationToken = default);
    Task<List<Product>> SearchProductsAsync(string indexName, string searchTerm, int pageSize, CancellationToken cancellationToken = default);
}