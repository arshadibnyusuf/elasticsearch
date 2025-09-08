using ElasticSearchApi.Infrastructure.Elasticsearch;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace ElasticSearchApi.Tests;

public class LayeredQueryBuilderTests(ITestOutputHelper output)
{
    private readonly LayeredQueryBuilder _queryBuilder = new();

    [Fact]
    public void BuildMultiSearchQuery_ShouldCreateValidQuery_AndPrintQuery()
    {
        var indexName = "products";
        var searchTerm = "laptop";
        var pageSize = 100;

        var query = _queryBuilder.BuildMultiSearchQuery(indexName, searchTerm, pageSize);

        output.WriteLine("=== ELASTICSEARCH MULTI-SEARCH QUERY ===");
        output.WriteLine(query);
        output.WriteLine("=== END QUERY ===");

        // Verify the query structure
        Assert.NotNull(query);
        Assert.NotEmpty(query);
        
        // Verify it contains 4 layers (8 lines - 4 index + 4 query)
        var lines = query.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(8, lines.Length);

        // Verify each layer has index header and query
        for (int i = 0; i < lines.Length; i += 2)
        {
            var indexLine = JObject.Parse(lines[i]);
            Assert.NotNull(indexLine["index"]);
            Assert.Equal(indexName, indexLine["index"]!.ToString());

            var queryLine = JObject.Parse(lines[i + 1]);
            Assert.NotNull(queryLine["query"]);
            Assert.NotNull(queryLine["size"]);
            Assert.Equal(pageSize, queryLine["size"]!.Value<int>());
        }
    }

    [Theory]
    [InlineData("laptop", 10)]
    [InlineData("phone case", 20)]
    [InlineData("USB-C cable", 5)]
    public void BuildMultiSearchQuery_WithDifferentInputs_ShouldWork(string searchTerm, int pageSize)
    {
        var query = _queryBuilder.BuildMultiSearchQuery("products", searchTerm, pageSize);

        output.WriteLine($"\n=== Query for '{searchTerm}' with size {pageSize} ===");
        output.WriteLine(query);

        Assert.Contains(searchTerm, query);
        Assert.Contains($"\"size\":{pageSize}", query);
    }

    [Fact]
    public void BuildMultiSearchQuery_ShouldHaveCorrectLayerStructure()
    {
        var query = _queryBuilder.BuildMultiSearchQuery("test-index", "test", 5);
        var lines = query.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        output.WriteLine("\n=== ANALYZING LAYER STRUCTURE ===");

        // Check each layer
        for (int layer = 0; layer < 4; layer++)
        {
            var queryLine = JObject.Parse(lines[layer * 2 + 1]);
            var multiMatch = queryLine["query"]!["bool"]!["must"]!["multi_match"]!;
            
            output.WriteLine($"\nLayer {layer + 1}:");
            output.WriteLine($"  Fields: {multiMatch["fields"]}");
            output.WriteLine($"  Type: {multiMatch["type"] ?? "default"}");
            output.WriteLine($"  Fuzziness: {multiMatch["fuzziness"] ?? "auto"}");

            switch (layer)
            {
                case 0: // Layer 1: phrase query on specific fields
                    Assert.Contains("title", multiMatch["fields"]!.ToString());
                    Assert.Contains("brand", multiMatch["fields"]!.ToString());
                    Assert.Equal("phrase", multiMatch["type"]!.ToString());
                    break;
                case 1: // Layer 2: multi_match with fuzziness 0
                    Assert.Equal(0, multiMatch["fuzziness"]!.Value<int>());
                    break;
                case 2: // Layer 3: multi_match with fuzziness 1
                    Assert.Equal(1, multiMatch["fuzziness"]!.Value<int>());
                    break;
                case 3: // Layer 4: multi_match with fuzziness 2
                    Assert.Equal(2, multiMatch["fuzziness"]!.Value<int>());
                    break;
            }

            // Check must_not excludes previous layers
            if (layer > 0)
            {
                var mustNot = queryLine["query"]!["bool"]!["must_not"] as JArray;
                Assert.NotNull(mustNot);
                Assert.Equal(layer, mustNot.Count);
                output.WriteLine($"  Excludes {layer} previous layer(s)");
            }
        }
    }
}