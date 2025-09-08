namespace ElasticSearchApi.Infrastructure.Configuration;

public class ElasticSearchSettings
{
    public const string SectionName = "ElasticSearch";
    
    public string Url { get; set; } = "http://localhost:9200";
    public string DefaultIndex { get; set; } = "default";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool EnableDebugMode { get; set; } = false;
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int MaxRetryAttempts { get; set; } = 3;
}