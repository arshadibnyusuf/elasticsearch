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
        
        // Verify it contains 5 layers (10 lines - 5 index + 5 query)
        var lines = query.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(10, lines.Length);

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
        for (int layer = 0; layer < 5; layer++)
        {
            var queryLine = JObject.Parse(lines[layer * 2 + 1]);
            var multiMatch = queryLine["query"]!["bool"]!["must"]!["multi_match"]!;
            
            output.WriteLine($"\nLayer {layer + 1}:");
            output.WriteLine($"  Fields: {multiMatch["fields"]}");
            output.WriteLine($"  Type: {multiMatch["type"] ?? "default"}");
            output.WriteLine($"  Fuzziness: {multiMatch["fuzziness"] ?? "auto"}");
            output.WriteLine($"  Operator: {multiMatch["operator"] ?? "default"}");

            switch (layer)
            {
                case 0: // Layer 1: phrase query on specific fields with AND
                    Assert.Contains("title", multiMatch["fields"]!.ToString());
                    Assert.Contains("brand", multiMatch["fields"]!.ToString());
                    Assert.Contains("categories", multiMatch["fields"]!.ToString());
                    Assert.Equal("phrase", multiMatch["type"]!.ToString());
                    Assert.Equal("and", multiMatch["operator"]!.ToString());
                    break;
                case 1: // Layer 2: multi_match with fuzziness 0 and AND
                    Assert.Equal(0, multiMatch["fuzziness"]!.Value<int>());
                    Assert.Equal("and", multiMatch["operator"]!.ToString());
                    Assert.Contains("title", multiMatch["fields"]!.ToString());
                    Assert.Contains("brand", multiMatch["fields"]!.ToString());
                    Assert.Contains("categories", multiMatch["fields"]!.ToString());
                    break;
                case 2: // Layer 3: multi_match with fuzziness 1 and AND
                    Assert.Equal(1, multiMatch["fuzziness"]!.Value<int>());
                    Assert.Equal("and", multiMatch["operator"]!.ToString());
                    Assert.Contains("title", multiMatch["fields"]!.ToString());
                    Assert.Contains("brand", multiMatch["fields"]!.ToString());
                    Assert.Contains("categories", multiMatch["fields"]!.ToString());
                    break;
                case 3: // Layer 4: multi_match with fuzziness 2 and AND on all fields
                    Assert.Equal(2, multiMatch["fuzziness"]!.Value<int>());
                    Assert.Equal("and", multiMatch["operator"]!.ToString());
                    // Layer 4 should search all fields
                    Assert.Contains("title", multiMatch["fields"]!.ToString());
                    Assert.Contains("brand", multiMatch["fields"]!.ToString());
                    Assert.Contains("description", multiMatch["fields"]!.ToString());
                    Assert.Contains("categories", multiMatch["fields"]!.ToString());
                    Assert.Contains("product_details", multiMatch["fields"]!.ToString());
                    break;
                case 4: // Layer 5: multi_match with fuzziness 2 and OR on all fields
                    Assert.Equal(2, multiMatch["fuzziness"]!.Value<int>());
                    Assert.Equal("or", multiMatch["operator"]!.ToString());
                    // Layer 5 should search all fields
                    Assert.Contains("title", multiMatch["fields"]!.ToString());
                    Assert.Contains("brand", multiMatch["fields"]!.ToString());
                    Assert.Contains("description", multiMatch["fields"]!.ToString());
                    Assert.Contains("categories", multiMatch["fields"]!.ToString());
                    Assert.Contains("product_details", multiMatch["fields"]!.ToString());
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

    [Fact]
    public void BuildMultiSearchQuery_ShouldHaveCorrectOperators()
    {
        var query = _queryBuilder.BuildMultiSearchQuery("test-index", "laptop computer", 10);
        var lines = query.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        output.WriteLine("\n=== TESTING OPERATORS FOR MULTI-WORD QUERY ===");

        // Test each layer has the correct operator
        for (int layer = 0; layer < 5; layer++)
        {
            var queryLine = JObject.Parse(lines[layer * 2 + 1]);
            var multiMatch = queryLine["query"]!["bool"]!["must"]!["multi_match"]!;
            
            var expectedOperator = layer < 4 ? "and" : "or";
            var actualOperator = multiMatch["operator"]?.ToString() ?? "default";
            
            output.WriteLine($"Layer {layer + 1}: Expected operator '{expectedOperator}', got '{actualOperator}'");
            
            Assert.Equal(expectedOperator, actualOperator);
        }
    }

    [Fact]
    public void BuildMultiSearchQuery_Layer5_ShouldExcludePreviousFourLayers()
    {
        var query = _queryBuilder.BuildMultiSearchQuery("products", "test", 5);
        var lines = query.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Get layer 5 query (index 9)
        var layer5Query = JObject.Parse(lines[9]);
        var mustNot = layer5Query["query"]!["bool"]!["must_not"] as JArray;
        
        output.WriteLine($"\nLayer 5 must_not count: {mustNot?.Count ?? 0}");
        
        Assert.NotNull(mustNot);
        Assert.Equal(4, mustNot.Count); // Should exclude all 4 previous layers
    }
}