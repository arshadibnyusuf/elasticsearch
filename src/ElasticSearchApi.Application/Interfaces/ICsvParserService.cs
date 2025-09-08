using System.Collections.Generic;
using System.Threading.Tasks;
using ElasticSearchApi.Domain.Entities;

namespace ElasticSearchApi.Application.Interfaces;

public interface ICsvParserService
{
    Task<IEnumerable<Product>> ParseProductsAsync(string filePath);
}