namespace ElasticSearchApi.Infrastructure.Elasticsearch;

public interface ILayeredQueryBuilder
{
    string BuildMultiSearchQuery(string indexName, string searchTerm, int pageSize);
}