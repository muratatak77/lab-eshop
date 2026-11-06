using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using Asp.Versioning;
using Asp.Versioning.Http;
using eShop.Catalog.API.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace eShop.Catalog.FunctionalTests;

public sealed class CatalogItemReviewsApiTests : IClassFixture<CatalogApiFixture>
{
    private readonly WebApplicationFactory<Program> _webApplicationFactory;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public CatalogItemReviewsApiTests(CatalogApiFixture fixture)
    {
        _webApplicationFactory = fixture;
    }

    private HttpClient CreateHttpClient(ApiVersion apiVersion)
    {
        var handler = new ApiVersionHandler(new QueryStringApiVersionWriter(), apiVersion);
        return _webApplicationFactory.CreateDefaultClient(handler);
    }

    // Each test reviews its own freshly created item so that counts and ordering
    // are not affected by other tests running against the same database.
    private static async Task CreateItemAsync(HttpClient httpClient, int id, CancellationToken cancellationToken)
    {
        var item = new CatalogItem($"Review test item {id}")
        {
            Id = id,
            Price = 10m,
            CatalogTypeId = 1,
            CatalogBrandId = 1,
            AvailableStock = 10,
            MaxStockThreshold = 100
        };

        var response = await httpClient.PostAsJsonAsync("/api/catalog/items", item, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static Task<HttpResponseMessage> PostReviewAsync(HttpClient httpClient, int id, CreateCatalogItemReviewRequest request, CancellationToken cancellationToken)
        => httpClient.PostAsJsonAsync($"/api/catalog/items/{id}/reviews", request, cancellationToken);

    [Theory]
    [InlineData(1.0, 20001)]
    [InlineData(2.0, 20002)]
    public async Task CreateItemReviewReturnsCreatedReview(double version, int id)
    {
        using var httpClient = CreateHttpClient(new ApiVersion(version));
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateItemAsync(httpClient, id, cancellationToken);

        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        var response = await PostReviewAsync(httpClient, id,
            new CreateCatalogItemReviewRequest("  Alice  ", 4, "  Comfortable  ", "   "), cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/catalog/items/{id}/reviews", response.Headers.Location?.OriginalString);

        var review = await response.Content.ReadFromJsonAsync<CatalogItemReview>(_jsonSerializerOptions, cancellationToken);
        Assert.NotNull(review);
        Assert.True(review.Id > 0);
        Assert.Equal(id, review.CatalogItemId);
        Assert.Equal("Alice", review.AuthorName);
        Assert.Equal(4, review.Rating);
        Assert.Equal("Comfortable", review.Title);
        Assert.Null(review.Comment);
        Assert.True(review.CreatedAt > before);
    }

    [Theory]
    [InlineData(1.0, 20011)]
    [InlineData(2.0, 20012)]
    public async Task GetItemReviewsReturnsNewestFirstAndRespectsPageSize(double version, int id)
    {
        using var httpClient = CreateHttpClient(new ApiVersion(version));
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateItemAsync(httpClient, id, cancellationToken);

        foreach (var (author, rating) in new[] { ("First", 5), ("Second", 3), ("Third", 1) })
        {
            var created = await PostReviewAsync(httpClient, id, new CreateCatalogItemReviewRequest(author, rating, null, $"{author} comment"), cancellationToken);
            created.EnsureSuccessStatusCode();
        }

        var response = await httpClient.GetAsync($"/api/catalog/items/{id}/reviews?pageIndex=0&pageSize=2", cancellationToken);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PaginatedItems<CatalogItemReview>>(_jsonSerializerOptions, cancellationToken);

        Assert.NotNull(page);
        Assert.Equal(3, page.Count);
        Assert.Equal(0, page.PageIndex);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(["Third", "Second"], page.Data.Select(r => r.AuthorName));

        response = await httpClient.GetAsync($"/api/catalog/items/{id}/reviews?pageIndex=1&pageSize=2", cancellationToken);
        response.EnsureSuccessStatusCode();
        page = await response.Content.ReadFromJsonAsync<PaginatedItems<CatalogItemReview>>(_jsonSerializerOptions, cancellationToken);

        Assert.NotNull(page);
        Assert.Equal(["First"], page.Data.Select(r => r.AuthorName));
    }

    [Theory]
    [InlineData(1.0, 20021)]
    [InlineData(2.0, 20022)]
    public async Task GetItemReviewSummaryAggregatesRatings(double version, int id)
    {
        using var httpClient = CreateHttpClient(new ApiVersion(version));
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateItemAsync(httpClient, id, cancellationToken);

        foreach (var rating in new[] { 5, 5, 4, 2 })
        {
            var created = await PostReviewAsync(httpClient, id, new CreateCatalogItemReviewRequest("Shopper", rating, null, null), cancellationToken);
            created.EnsureSuccessStatusCode();
        }

        var summary = await httpClient.GetFromJsonAsync<CatalogItemReviewSummary>($"/api/catalog/items/{id}/reviews/summary", _jsonSerializerOptions, cancellationToken);

        Assert.NotNull(summary);
        Assert.Equal(id, summary.CatalogItemId);
        Assert.Equal(4, summary.ReviewCount);
        Assert.Equal(4.0, summary.AverageRating);
        Assert.Equal(
            [new(5, 2), new(4, 1), new(3, 0), new(2, 1), new(1, 0)],
            summary.RatingCounts);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetItemReviewSummaryForUnreviewedItemIsEmpty(double version)
    {
        using var httpClient = CreateHttpClient(new ApiVersion(version));
        var cancellationToken = TestContext.Current.CancellationToken;

        // Seeded items have no reviews.
        var summary = await httpClient.GetFromJsonAsync<CatalogItemReviewSummary>("/api/catalog/items/3/reviews/summary", _jsonSerializerOptions, cancellationToken);

        Assert.NotNull(summary);
        Assert.Equal(0, summary.ReviewCount);
        Assert.Equal(0, summary.AverageRating);
        Assert.Equal(5, summary.RatingCounts.Count);
        Assert.All(summary.RatingCounts, c => Assert.Equal(0, c.Count));
    }

    [Theory]
    [InlineData(1.0, 0)]
    [InlineData(2.0, 6)]
    public async Task CreateItemReviewRejectsOutOfRangeRating(double version, int rating)
    {
        using var httpClient = CreateHttpClient(new ApiVersion(version));
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await PostReviewAsync(httpClient, 1, new CreateCatalogItemReviewRequest("Alice", rating, null, null), cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(_jsonSerializerOptions, cancellationToken);
        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateCatalogItemReviewRequest.Rating), problem.Errors.Keys);

        var summary = await httpClient.GetFromJsonAsync<CatalogItemReviewSummary>("/api/catalog/items/1/reviews/summary", _jsonSerializerOptions, cancellationToken);
        Assert.NotNull(summary);
        Assert.Equal(0, summary.ReviewCount);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task CreateItemReviewRequiresAuthorName(double version)
    {
        using var httpClient = CreateHttpClient(new ApiVersion(version));
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await PostReviewAsync(httpClient, 1, new CreateCatalogItemReviewRequest(" ", 5, null, null), cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(_jsonSerializerOptions, cancellationToken);
        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateCatalogItemReviewRequest.AuthorName), problem.Errors.Keys);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task ReviewEndpointsReturnNotFoundForUnknownItem(double version)
    {
        using var httpClient = CreateHttpClient(new ApiVersion(version));
        var cancellationToken = TestContext.Current.CancellationToken;
        const int unknownId = 987654;

        var list = await httpClient.GetAsync($"/api/catalog/items/{unknownId}/reviews", cancellationToken);
        var summary = await httpClient.GetAsync($"/api/catalog/items/{unknownId}/reviews/summary", cancellationToken);
        var create = await PostReviewAsync(httpClient, unknownId, new CreateCatalogItemReviewRequest("Alice", 5, null, null), cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, summary.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
    }

    [Theory]
    [InlineData(1.0, 20031)]
    [InlineData(2.0, 20032)]
    public async Task DeletingItemRemovesItsReviews(double version, int id)
    {
        using var httpClient = CreateHttpClient(new ApiVersion(version));
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateItemAsync(httpClient, id, cancellationToken);

        var created = await PostReviewAsync(httpClient, id, new CreateCatalogItemReviewRequest("Alice", 5, null, null), cancellationToken);
        created.EnsureSuccessStatusCode();

        var deleted = await httpClient.DeleteAsync($"/api/catalog/items/{id}", cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        // Re-creating an item with the same id must not resurface the old reviews.
        await CreateItemAsync(httpClient, id, cancellationToken);
        var summary = await httpClient.GetFromJsonAsync<CatalogItemReviewSummary>($"/api/catalog/items/{id}/reviews/summary", _jsonSerializerOptions, cancellationToken);

        Assert.NotNull(summary);
        Assert.Equal(0, summary.ReviewCount);
    }
}
