using Microsoft.EntityFrameworkCore;
using QuotesApi.Models;

namespace QuotesApi.Data;

public class QuotesDbContext : DbContext
{
    public QuotesDbContext(DbContextOptions<QuotesDbContext> options) : base(options) { }

    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Collection> Collections => Set<Collection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Quote>(entity =>
        {
            entity.Property(q => q.Author)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(q => q.Text)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(q => q.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);
        });

        modelBuilder.Entity<Collection>(entity =>
        {
            entity.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(c => c.OwnerId)
                .IsRequired();

            entity.Navigation(c => c.Items)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.OwnsMany(c => c.Items, owned =>
            {
                owned.WithOwner().HasForeignKey("CollectionId");
                owned.HasKey("CollectionId", nameof(CollectionItem.QuoteId));
                owned.Property(ci => ci.QuoteId).IsRequired();
                owned.Property(ci => ci.AddedAt).IsRequired();
                owned.ToTable("CollectionItems");
            });
        });
    }
}