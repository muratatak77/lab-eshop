using eShop.Catalog.API.Model;

namespace eShop.Application.UnitTests;

[TestClass]
public class CatalogItemReviewTests
{
    [TestMethod]
    public void ValidRequestHasNoErrors()
    {
        var request = new CreateCatalogItemReviewRequest("Alice", 5, "Great boots", "Kept my feet dry all weekend.");

        Assert.IsEmpty(request.Validate());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(6)]
    [DataRow(-1)]
    public void RatingOutsideOneToFiveIsRejected(int rating)
    {
        var errors = new CreateCatalogItemReviewRequest("Alice", rating, null, null).Validate();

        Assert.HasCount(1, errors);
        Assert.IsTrue(errors.ContainsKey(nameof(CreateCatalogItemReviewRequest.Rating)));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    public void BlankAuthorNameIsRejected(string? authorName)
    {
        var errors = new CreateCatalogItemReviewRequest(authorName!, 3, null, null).Validate();

        Assert.IsTrue(errors.ContainsKey(nameof(CreateCatalogItemReviewRequest.AuthorName)));
    }

    [TestMethod]
    public void OverlongFieldsAreRejected()
    {
        var request = new CreateCatalogItemReviewRequest(
            new string('a', CatalogItemReview.AuthorNameMaxLength + 1),
            3,
            new string('t', CatalogItemReview.TitleMaxLength + 1),
            new string('c', CatalogItemReview.CommentMaxLength + 1));

        var errors = request.Validate();

        CollectionAssert.AreEquivalent(
            new[] { nameof(request.AuthorName), nameof(request.Title), nameof(request.Comment) },
            errors.Keys.ToArray());
    }

    [TestMethod]
    public void LengthLimitsIgnoreSurroundingWhitespace()
    {
        var request = new CreateCatalogItemReviewRequest(
            "  " + new string('a', CatalogItemReview.AuthorNameMaxLength) + "  ",
            3,
            new string('t', CatalogItemReview.TitleMaxLength) + "\n",
            null);

        Assert.IsEmpty(request.Validate());
    }

    [TestMethod]
    public void ToReviewTrimsValuesAndNormalisesBlankOptionalFields()
    {
        var createdAt = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
        var request = new CreateCatalogItemReviewRequest("  Alice ", 4, "   ", " Fits well. ");

        var review = request.ToReview(42, createdAt);

        Assert.AreEqual(42, review.CatalogItemId);
        Assert.AreEqual("Alice", review.AuthorName);
        Assert.AreEqual(4, review.Rating);
        Assert.IsNull(review.Title);
        Assert.AreEqual("Fits well.", review.Comment);
        Assert.AreEqual(createdAt, review.CreatedAt);
    }

    [TestMethod]
    public void SummaryWithoutReviewsReportsZeroForEveryRating()
    {
        var summary = CatalogItemReviewSummary.Create(7, []);

        Assert.AreEqual(7, summary.CatalogItemId);
        Assert.AreEqual(0, summary.ReviewCount);
        Assert.AreEqual(0, summary.AverageRating);
        CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1 }, summary.RatingCounts.Select(c => c.Rating).ToArray());
        Assert.IsTrue(summary.RatingCounts.All(c => c.Count == 0));
    }

    [TestMethod]
    public void SummaryFillsMissingRatingsAndComputesAverage()
    {
        // 3 x 5 stars, 1 x 2 stars => 17 / 4 = 4.25, reported as 4.3
        var summary = CatalogItemReviewSummary.Create(1, [new(5, 3), new(2, 1)]);

        Assert.AreEqual(4, summary.ReviewCount);
        Assert.AreEqual(4.3, summary.AverageRating);
        CollectionAssert.AreEqual(
            new[] { new CatalogRatingCount(5, 3), new(4, 0), new(3, 0), new(2, 1), new(1, 0) },
            summary.RatingCounts.ToArray());
    }

    [TestMethod]
    public void SummaryRoundsAverageToOneDecimal()
    {
        // (5 + 4 + 4) / 3 = 4.333...
        var summary = CatalogItemReviewSummary.Create(1, [new(4, 2), new(5, 1)]);

        Assert.AreEqual(4.3, summary.AverageRating);
    }
}
