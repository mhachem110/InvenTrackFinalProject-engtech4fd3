using Microsoft.EntityFrameworkCore;
using InvenTrack.Models;

namespace InvenTrack.Data
{
    public class InvenTrackContext : DbContext
    {
        public InvenTrackContext(DbContextOptions<InvenTrackContext> options)
            : base(options)
        {
        }

        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<StorageLocation> StorageLocations { get; set; }
        public DbSet<StockTransaction> StockTransactions { get; set; }
        public DbSet<ItemPhoto> ItemPhotos { get; set; }
        public DbSet<ItemThumbnail> ItemThumbnails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // InventoryItem -> StockTransaction (restrict delete to keep history)
            modelBuilder.Entity<InventoryItem>()
                .HasMany(i => i.StockTransactions)
                .WithOne(t => t.InventoryItem)
                .HasForeignKey(t => t.InventoryItemID)
                .OnDelete(DeleteBehavior.Restrict);

            // Category -> InventoryItem
            modelBuilder.Entity<Category>()
                .HasMany(c => c.InventoryItems)
                .WithOne(i => i.Category)
                .HasForeignKey(i => i.CategoryID)
                .OnDelete(DeleteBehavior.SetNull);

            // StorageLocation -> InventoryItem
            modelBuilder.Entity<StorageLocation>()
                .HasMany(l => l.InventoryItems)
                .WithOne(i => i.StorageLocation)
                .HasForeignKey(i => i.StorageLocationID)
                .OnDelete(DeleteBehavior.SetNull);

            // SKU should be unique (ignore nulls in SQLite by keeping it optional but indexed)
            modelBuilder.Entity<InventoryItem>()
                .HasIndex(i => i.SKU)
                .IsUnique();

            // Optional unique reference number (only if provided)
            modelBuilder.Entity<StockTransaction>()
                .HasIndex(t => t.ReferenceNumber)
                .IsUnique();

            base.OnModelCreating(modelBuilder);
        }
    }
}
