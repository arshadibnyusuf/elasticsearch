namespace ElasticSearchApi.Application.Models;

public class IndexResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? TotalProductsParsed { get; set; }
    public int? TotalProductsIndexed { get; set; }
    public List<string> Errors { get; set; } = [];
}