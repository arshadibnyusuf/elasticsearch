using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ElasticSearchApi.Domain.Entities;
using ElasticSearchApi.Application.Models;

namespace ElasticSearchApi.Application.Interfaces;

public interface IProductSearchService
{
    Task<IndexResult> IndexProductsFromCsvAsync(string csvFilePath, string indexName, CancellationToken cancellationToken = default);
    Task<List<Product>> SearchProductsAsync(string indexName, string searchTerm, int pageSize, CancellationToken cancellationToken = default);
}