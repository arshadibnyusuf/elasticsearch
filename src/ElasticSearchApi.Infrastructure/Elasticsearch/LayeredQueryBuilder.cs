using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ElasticSearchApi.Infrastructure.Elasticsearch;

public class LayeredQueryBuilder : ILayeredQueryBuilder
{
    private readonly string[] _searchableFields = ["title", "brand", "description", "categories", "product_details"];

    public string BuildMultiSearchQuery(string indexName, string searchTerm, int pageSize)
    {
        var sb = new StringBuilder();

        // Layer 1: multi_match phrase query on title, brand, categories
        var layer1Query = BuildLayer1Query(searchTerm);
        AppendQuery(sb, indexName, layer1Query, pageSize);

        // Layer 2: multi_match with fuzziness 0, excluding Layer 1
        var layer2Query = BuildLayer2Query(searchTerm, layer1Query);
        AppendQuery(sb, indexName, layer2Query, pageSize);

        // Layer 3: multi_match with fuzziness 1, excluding Layers 1-2
        var layer3Query = BuildLayer3Query(searchTerm, layer1Query, layer2Query);
        AppendQuery(sb, indexName, layer3Query, pageSize);

        // Layer 4: multi_match with fuzziness 2, excluding Layers 1-3
        var layer4Query = BuildLayer4Query(searchTerm, layer1Query, layer2Query, layer3Query);
        AppendQuery(sb, indexName, layer4Query, pageSize);

        return sb.ToString();
    }

    private JObject BuildLayer1Query(string searchTerm)
    {
        return new JObject
        {
            ["query"] = new JObject
            {
                ["bool"] = new JObject
                {
                    ["must"] = new JObject
                    {
                        ["multi_match"] = new JObject
                        {
                            ["query"] = searchTerm,
                            ["fields"] = new JArray("title", "brand", "categories"),
                            ["type"] = "phrase",
                            ["slop"] = 2
                        }
                    }
                }
            },
            ["sort"] = new JArray
            {
                new JObject { ["rank"] = "asc" }
            }
        };
    }

    private JObject BuildLayer2Query(string searchTerm, JObject layer1Query)
    {
        return new JObject
        {
            ["query"] = new JObject
            {
                ["bool"] = new JObject
                {
                    ["must"] = new JObject
                    {
                        ["multi_match"] = new JObject
                        {
                            ["query"] = searchTerm,
                            ["fields"] = JArray.FromObject(_searchableFields),
                            ["fuzziness"] = 0
                        }
                    },
                    ["must_not"] = new JArray
                    {
                        layer1Query["query"]!["bool"]!["must"]!
                    }
                }
            },
            ["sort"] = new JArray
            {
                new JObject { ["rank"] = "asc" }
            }
        };
    }

    private JObject BuildLayer3Query(string searchTerm, JObject layer1Query, JObject layer2Query)
    {
        return new JObject
        {
            ["query"] = new JObject
            {
                ["bool"] = new JObject
                {
                    ["must"] = new JObject
                    {
                        ["multi_match"] = new JObject
                        {
                            ["query"] = searchTerm,
                            ["fields"] = JArray.FromObject(_searchableFields),
                            ["fuzziness"] = 1
                        }
                    },
                    ["must_not"] = new JArray
                    {
                        layer1Query["query"]!["bool"]!["must"]!,
                        layer2Query["query"]!["bool"]!["must"]!
                    }
                }
            },
            ["sort"] = new JArray
            {
                new JObject { ["rank"] = "asc" }
            }
        };
    }

    private JObject BuildLayer4Query(string searchTerm, JObject layer1Query, JObject layer2Query, JObject layer3Query)
    {
        return new JObject
        {
            ["query"] = new JObject
            {
                ["bool"] = new JObject
                {
                    ["must"] = new JObject
                    {
                        ["multi_match"] = new JObject
                        {
                            ["query"] = searchTerm,
                            ["fields"] = JArray.FromObject(_searchableFields),
                            ["fuzziness"] = 2
                        }
                    },
                    ["must_not"] = new JArray
                    {
                        layer1Query["query"]!["bool"]!["must"]!,
                        layer2Query["query"]!["bool"]!["must"]!,
                        layer3Query["query"]!["bool"]!["must"]!
                    }
                }
            },
            ["sort"] = new JArray
            {
                new JObject { ["rank"] = "asc" }
            }
        };
    }

    private void AppendQuery(StringBuilder sb, string indexName, JObject query, int pageSize)
    {
        // Add index specification
        sb.AppendLine(JsonConvert.SerializeObject(new { index = indexName }, Formatting.None));
        
        // Add query with size
        query["size"] = pageSize;
        sb.AppendLine(JsonConvert.SerializeObject(query, Formatting.None));
    }
}