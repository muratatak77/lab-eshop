using System.Text.Json.Serialization;

namespace eShop.Catalog.API.Model;

/// <summary>
/// A shopper's review of a single catalog item: a 1-5 star rating with an optional title and comment.
/// </summary>
public class CatalogItemReview
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int AuthorNameMaxLength = 100;
    public const int TitleMaxLength = 120;
    public const int CommentMaxLength = 2000;

    public int Id { get; set; }

    public int CatalogItemId { get; set; }

    [JsonIgnore]
    public CatalogItem? CatalogItem { get; set; }

    public string AuthorName { get; set; }

    public int Rating { get; set; }

    public string? Title { get; set; }

    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public CatalogItemReview(string authorName) { AuthorName = authorName; }
}
