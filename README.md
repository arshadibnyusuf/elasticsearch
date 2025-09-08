# ElasticSearch Product Search API

A simplified REST API for searching product data using Elasticsearch with a layered query approach.

## Project Structure

```
ElasticSearchApi/
├── src/
│   ├── ElasticSearchApi.Api/              # Web API layer (Controllers, Middleware)
│   ├── ElasticSearchApi.Application/      # Business logic (Services, DTOs)
│   ├── ElasticSearchApi.Infrastructure/   # Data access (Elasticsearch client, Repository)
│   └── ElasticSearchApi.Domain/           # Domain models (Entities, Interfaces)
├── tests/
│   └── ElasticSearchApi.Tests/           # Unit tests
├── elasticsearch/
│   ├── elasticsearch.yml                  # Elasticsearch configuration
│   ├── settings.json                      # Custom analyzers
│   └── mappings.json                      # Index mappings
├── docker-compose.yml                     # Multi-container setup
├── Dockerfile                             # API container configuration
└── data.csv                               # Sample product dataset from https://github.com/luminati-io/Amazon-dataset-samples/
```

## Features

- **Layered Search**: Multi-level search with progressive fuzziness for optimal relevance
- **Custom Analyzers**: Specialized text processing for product search
- **Docker Support**: Multi-container setup with Elasticsearch, Kibana, and API
- **Health Checks**: Monitors Elasticsearch connectivity
- **Structured Logging**: Serilog for comprehensive logging

## Quick Start

1. **Start all services**:
   ```bash
   docker compose up -d
   ```

2. **Wait for automatic indexing**:
   The API will automatically start indexing products from data.csv 5 seconds after Elasticsearch is healthy. Wait about 15-20 seconds for the indexing to complete.

3. **Search products**:
   ```bash
   # Example search for waterproof bluetooth speaker
   curl "http://localhost:8080/api/products/search?q=waterproof%20bluetooth%20speaker&size=20"
   ```

## API Endpoints

### GET /api/products/search
Searches for products using a sophisticated layered query approach.

Parameters:
- `q` (required): Search term
- `size` (optional): Number of results to return (default: 20, max: 100)

### GET /health
Health check endpoint

## Search Strategy

The API uses a 5-layer search approach with _msearch for optimal relevance:

1. **Layer 1**: Phrase match on title, brand, and categories (slop: 2) with AND operator
2. **Layer 2**: Match on title, brand, and categories with fuzziness: 0 and AND operator
3. **Layer 3**: Match on title, brand, and categories with fuzziness: 1 and AND operator
4. **Layer 4**: Match on all fields with fuzziness: 2 and AND operator
5. **Layer 5**: Match on all fields with fuzziness: 2 and OR operator

Each layer excludes results from previous layers to avoid duplicates. Results are sorted by rank within each layer.

## Sample Searches

```bash
# Waterproof bluetooth speaker
curl "http://localhost:8080/api/products/search?q=waterproof%20bluetooth%20speaker"

# Search by brand
curl "http://localhost:8080/api/products/search?q=Saucony"

# Search with typo (fuzziness helps)
curl "http://localhost:8080/api/products/search?q=waterprof%speker"
```

## Custom Text Analysis

The API uses custom analyzers optimized for product search:
- **Character Filters**: Handle special characters, multiplication signs, and alphanumeric splitting
- **Tokenizer**: Custom pattern-based tokenization
- **Filters**: Lowercase and stemming for better matching


## Docker Configuration

- **Elasticsearch**: http://localhost:9200
- **Kibana**: http://localhost:5601
- **API**: http://localhost:8080
- **Swagger UI**: http://localhost:8080
- **Health Check**: http://localhost:8080/health

## Technologies

- .NET 8
- Elasticsearch 8.11.0
- NEST Client
- Docker & Docker Compose
- Serilog for logging
- CsvHelper for CSV parsing

## Product Data Fields

- **Searchable**: title, brand, description, categories, product_details
- **Sortable**: rank, reviews_count
- **Metadata**: rating, availability, manufacturer, department