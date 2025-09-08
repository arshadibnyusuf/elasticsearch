using Serilog;
using ElasticSearchApi.Application.Interfaces;
using ElasticSearchApi.Application.Services;
using ElasticSearchApi.Infrastructure.Extensions;
using ElasticSearchApi.Api.Middleware;
using ElasticSearchApi.Api.Services;
using HealthChecks.Elasticsearch;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
    { 
        Title = "ElasticSearch Product Search API", 
        Version = "v1",
        Description = "A simplified REST API for searching Amazon product data using Elasticsearch with sophisticated layered query approach."
    });
    
    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (System.IO.File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddElasticsearch(builder.Configuration);

builder.Services.AddScoped<ICsvParserService, CsvParserService>();
builder.Services.AddScoped<IProductSearchService, ProductSearchService>();

// Add the startup indexing service
builder.Services.AddHostedService<StartupIndexingService>();

builder.Services.AddHealthChecks()
    .AddElasticsearch(
        elasticsearchUri: builder.Configuration["ElasticSearch:Url"] ?? "http://localhost:9200",
        name: "elasticsearch",
        timeout: TimeSpan.FromSeconds(5));

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ElasticSearch API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseRouting();
app.UseSerilogRequestLogging();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();