using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ElasticSearchApi.Domain.Entities;

namespace ElasticSearchApi.Domain.Interfaces;

public interface IProductRepository
{
    Task<bool> CreateIndexAsync(string indexName, Stream settingsJson, Stream mappingsJson, CancellationToken cancellationToken = default);
    Task<bool> IndexProductsAsync(string indexName, IEnumerable<Product> products, CancellationToken cancellationToken = default);
    Task<List<Product>> LayeredSearchAsync(string indexName, string searchTerm, int pageSize, CancellationToken cancellationToken = default);
    Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken = default);
}