namespace eShop.Catalog.API.Infrastructure.EntityConfigurations;

class CatalogItemReviewEntityTypeConfiguration
    : IEntityTypeConfiguration<CatalogItemReview>
{
    public void Configure(EntityTypeBuilder<CatalogItemReview> builder)
    {
        builder.ToTable("CatalogItemReview", t =>
            t.HasCheckConstraint(
                "CK_CatalogItemReview_Rating",
                $"\"Rating\" BETWEEN {CatalogItemReview.MinRating} AND {CatalogItemReview.MaxRating}"));

        builder.Property(r => r.AuthorName)
            .HasMaxLength(CatalogItemReview.AuthorNameMaxLength);

        builder.Property(r => r.Title)
            .HasMaxLength(CatalogItemReview.TitleMaxLength);

        builder.Property(r => r.Comment)
            .HasMaxLength(CatalogItemReview.CommentMaxLength);

        builder.HasOne(r => r.CatalogItem)
            .WithMany()
            .HasForeignKey(r => r.CatalogItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Reviews are always listed per item, newest first.
        builder.HasIndex(r => new { r.CatalogItemId, r.CreatedAt });
    }
}
