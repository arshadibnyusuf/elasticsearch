using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ElasticSearchApi.Application.Interfaces;
using ElasticSearchApi.Domain.Entities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ElasticSearchApi.Application.Services;

public class CsvParserService(ILogger<CsvParserService> logger) : ICsvParserService
{

    public async Task<IEnumerable<Product>> ParseProductsAsync(string filePath)
    {
        List<Product> products = [];

        try
        {
            var fileContent = await File.ReadAllTextAsync(filePath);
            
            using var reader = new StringReader(fileContent);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = context =>
                {
                    var row = context.Context.Parser?.Row ?? 0;
                    logger.LogWarning("Bad data found at row {Row}: {RawRecord}", row, context.RawRecord);
                }
            });

            await csv.ReadAsync();
            csv.ReadHeader();

            var rowNumber = 1;
            while (await csv.ReadAsync())
            {
                try
                {
                        var product = new Product
                        {
                            Id = Guid.NewGuid().ToString(),
                            Title = csv.GetField<string>("title") ?? string.Empty,
                            Brand = csv.GetField<string>("brand") ?? string.Empty,
                            Description = csv.GetField<string>("description") ?? string.Empty,
                            Availability = csv.GetField<string>("availability") ?? string.Empty,
                            ReviewsCount = ParseInt(csv.GetField<string>("reviews_count")),
                            Categories = ParseJsonArray(csv.GetField<string>("categories")),
                            Rank = ParseInt(csv.GetField<string>("rank")),
                            Rating = ParseFloat(csv.GetField<string>("rating")),
                            Manufacturer = csv.GetField<string>("manufacturer") ?? string.Empty,
                            Department = csv.GetField<string>("department") ?? string.Empty,
                            TopReview = csv.GetField<string>("top_review") ?? string.Empty,
                            Delivery = ParseJsonArray(csv.GetField<string>("delivery")),
                            Features = ParseJsonArray(csv.GetField<string>("features")),
                            Ingredients = csv.GetField<string>("ingredients") ?? string.Empty,
                            IsAvailable = ParseBool(csv.GetField<string>("is_available")),
                            RootBsCategory = csv.GetField<string>("root_bs_category") ?? string.Empty,
                            ProductDetails = csv.GetField<string>("product_details") ?? string.Empty
                        };

                        products.Add(product);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error parsing row {RowNumber}", rowNumber);
                }

                rowNumber++;
                
                // Log progress every 1000 rows
                if (rowNumber % 1000 == 0)
                {
                    logger.LogInformation("CSV parsing progress: {RowNumber} rows processed, {ProductCount} products parsed", 
                        rowNumber, products.Count);
                }
            }

            logger.LogInformation("Successfully parsed {ProductCount} products from CSV (processed {TotalRows} rows total)", 
                products.Count, rowNumber - 1);
            return products;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading CSV file");
            throw;
        }
    }

    private int ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        return int.TryParse(value, out var result) ? result : 0;
    }

    private float ParseFloat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0f;

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0f;
    }

    private bool ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private List<string> ParseJsonArray(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "null")
            return [];

        try
        {
            var result = JsonConvert.DeserializeObject<List<string>>(value);
            return result ?? [];
        }
        catch
        {
            // If JSON parsing fails, treat as a single value
            return [value];
        }
    }
}