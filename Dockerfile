FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/ElasticSearchApi.Api/ElasticSearchApi.Api.csproj", "src/ElasticSearchApi.Api/"]
COPY ["src/ElasticSearchApi.Application/ElasticSearchApi.Application.csproj", "src/ElasticSearchApi.Application/"]
COPY ["src/ElasticSearchApi.Infrastructure/ElasticSearchApi.Infrastructure.csproj", "src/ElasticSearchApi.Infrastructure/"]
COPY ["src/ElasticSearchApi.Domain/ElasticSearchApi.Domain.csproj", "src/ElasticSearchApi.Domain/"]

RUN dotnet restore "./src/ElasticSearchApi.Api/ElasticSearchApi.Api.csproj"

COPY . .
WORKDIR "/src/src/ElasticSearchApi.Api"
RUN dotnet build "./ElasticSearchApi.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ElasticSearchApi.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ElasticSearchApi.Api.dll"]