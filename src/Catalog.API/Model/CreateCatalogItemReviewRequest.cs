using System.ComponentModel;

namespace eShop.Catalog.API.Model;

public record CreateCatalogItemReviewRequest(
    [property: Description("The display name of the shopper writing the review")]
    string AuthorName,

    [property: Description("The star rating, from 1 (worst) to 5 (best)")]
    int Rating,

    [property: Description("An optional one-line headline for the review")]
    string? Title,

    [property: Description("An optional free-text comment")]
    string? Comment)
{
    /// <summary>
    /// Validates the request, returning the errors keyed by property name in the shape expected by
    /// <see cref="Microsoft.AspNetCore.Http.TypedResults.ValidationProblem(IDictionary{string, string[]}, string?, string?, string?, string?, IDictionary{string, object?}?)"/>.
    /// An empty dictionary means the request is valid.
    /// </summary>
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(AuthorName))
        {
            errors[nameof(AuthorName)] = ["An author name is required."];
        }
        else if (AuthorName.Trim().Length > CatalogItemReview.AuthorNameMaxLength)
        {
            errors[nameof(AuthorName)] = [$"The author name must be at most {CatalogItemReview.AuthorNameMaxLength} characters."];
        }

        if (Rating is < CatalogItemReview.MinRating or > CatalogItemReview.MaxRating)
        {
            errors[nameof(Rating)] = [$"The rating must be between {CatalogItemReview.MinRating} and {CatalogItemReview.MaxRating}."];
        }

        if (Title is not null && Title.Trim().Length > CatalogItemReview.TitleMaxLength)
        {
            errors[nameof(Title)] = [$"The title must be at most {CatalogItemReview.TitleMaxLength} characters."];
        }

        if (Comment is not null && Comment.Trim().Length > CatalogItemReview.CommentMaxLength)
        {
            errors[nameof(Comment)] = [$"The comment must be at most {CatalogItemReview.CommentMaxLength} characters."];
        }

        return errors;
    }

    /// <summary>
    /// Creates the review entity for <paramref name="catalogItemId"/>. Whitespace is trimmed and blank
    /// optional fields are stored as <see langword="null"/>. Call <see cref="Validate"/> first.
    /// </summary>
    public CatalogItemReview ToReview(int catalogItemId, DateTimeOffset createdAt) =>
        new(AuthorName.Trim())
        {
            CatalogItemId = catalogItemId,
            Rating = Rating,
            Title = NullIfBlank(Title),
            Comment = NullIfBlank(Comment),
            CreatedAt = createdAt,
        };

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
