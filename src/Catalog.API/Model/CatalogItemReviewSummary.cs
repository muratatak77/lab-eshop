namespace eShop.Catalog.API.Model;

public record CatalogRatingCount(int Rating, int Count);

/// <summary>
/// Aggregate rating information for a catalog item.
/// </summary>
/// <param name="CatalogItemId">The catalog item the summary describes.</param>
/// <param name="ReviewCount">The total number of reviews.</param>
/// <param name="AverageRating">The mean rating rounded to one decimal place, or 0 when there are no reviews.</param>
/// <param name="RatingCounts">The number of reviews for every star value, highest first. Always contains one entry per star value.</param>
public record CatalogItemReviewSummary(
    int CatalogItemId,
    int ReviewCount,
    double AverageRating,
    IReadOnlyList<CatalogRatingCount> RatingCounts)
{
    /// <summary>
    /// Builds a summary from per-rating counts as produced by a GROUP BY query. Ratings with no
    /// reviews may be omitted from <paramref name="counts"/>; they are reported with a zero count.
    /// </summary>
    public static CatalogItemReviewSummary Create(int catalogItemId, IEnumerable<CatalogRatingCount> counts)
    {
        var byRating = counts
            .GroupBy(c => c.Rating)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Count));

        var ratingCounts = new List<CatalogRatingCount>(CatalogItemReview.MaxRating);
        for (var rating = CatalogItemReview.MaxRating; rating >= CatalogItemReview.MinRating; rating--)
        {
            ratingCounts.Add(new CatalogRatingCount(rating, byRating.GetValueOrDefault(rating)));
        }

        var reviewCount = ratingCounts.Sum(c => c.Count);
        var average = reviewCount == 0
            ? 0
            : Math.Round((double)ratingCounts.Sum(c => c.Rating * c.Count) / reviewCount, 1, MidpointRounding.AwayFromZero);

        return new CatalogItemReviewSummary(catalogItemId, reviewCount, average, ratingCounts);
    }
}
