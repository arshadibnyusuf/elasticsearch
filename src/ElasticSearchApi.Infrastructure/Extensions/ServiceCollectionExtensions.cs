using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nest;
using ElasticSearchApi.Domain.Interfaces;
using ElasticSearchApi.Infrastructure.Configuration;
using ElasticSearchApi.Infrastructure.Elasticsearch;

namespace ElasticSearchApi.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddElasticsearch(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ElasticSearchSettings>(configuration.GetSection(ElasticSearchSettings.SectionName));

        services.AddSingleton<IElasticClient>(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<ElasticSearchSettings>>().Value;
            var connectionSettings = new ConnectionSettings(new Uri(settings.Url))
                .DefaultIndex(settings.DefaultIndex)
                .RequestTimeout(TimeSpan.FromSeconds(settings.RequestTimeoutSeconds))
                .MaxRetryTimeout(TimeSpan.FromSeconds(settings.ConnectionTimeoutSeconds))
                .MaximumRetries(settings.MaxRetryAttempts)
                .DefaultMappingFor<Domain.Entities.Product>(m => m
                    .PropertyName(p => p.ReviewsCount, "reviews_count")
                    .PropertyName(p => p.TopReview, "top_review")
                    .PropertyName(p => p.IsAvailable, "is_available")
                    .PropertyName(p => p.ProductDetails, "product_details")
                );

            if (!string.IsNullOrEmpty(settings.Username) && !string.IsNullOrEmpty(settings.Password))
            {
                connectionSettings = connectionSettings.BasicAuthentication(settings.Username, settings.Password);
            }

            return new ElasticClient(connectionSettings);
        });

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ILayeredQueryBuilder, LayeredQueryBuilder>();

        return services;
    }
}