using System.Collections.Generic;

namespace ElasticSearchApi.Domain.Entities;

public class Product
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Availability { get; set; } = string.Empty;
    public int ReviewsCount { get; set; }
    public List<string> Categories { get; set; } = new();
    public int Rank { get; set; }
    public float Rating { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string TopReview { get; set; } = string.Empty;
    public List<string> Delivery { get; set; } = new();
    public List<string> Features { get; set; } = new();
    public string Ingredients { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string RootBsCategory { get; set; } = string.Empty;
    public string ProductDetails { get; set; } = string.Empty;
}