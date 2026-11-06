namespace eShop.WebAppComponents.Catalog;

public record CatalogItemReview(
    int Id,
    int CatalogItemId,
    string AuthorName,
    int Rating,
    string? Title,
    string? Comment,
    DateTimeOffset CreatedAt);

public record CatalogItemReviewResult(int PageIndex, int PageSize, int Count, List<CatalogItemReview> Data);

public record CatalogRatingCount(int Rating, int Count);

public record CatalogItemReviewSummary(
    int CatalogItemId,
    int ReviewCount,
    double AverageRating,
    IReadOnlyList<CatalogRatingCount> RatingCounts);

public record CreateCatalogItemReviewRequest(string AuthorName, int Rating, string? Title, string? Comment);
