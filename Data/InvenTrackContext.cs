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
            // StockTransaction -> InventoryItem (keep history)
            modelBuilder.Entity<StockTransaction>()
                .HasOne(t => t.InventoryItem)
                .WithMany(i => i.StockTransactions)
                .HasForeignKey(t => t.InventoryItemID)
                .OnDelete(DeleteBehavior.Restrict);

            // InventoryItem -> Category
            // FIX: SetNull is invalid when FK is required (NOT NULL). Use Restrict instead.
            modelBuilder.Entity<InventoryItem>()
                .HasOne(i => i.Category)
                .WithMany(c => c.InventoryItems)
                .HasForeignKey(i => i.CategoryID)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            // InventoryItem -> StorageLocation
            modelBuilder.Entity<InventoryItem>()
                .HasOne(i => i.StorageLocation)
                .WithMany(l => l.InventoryItems)
                .HasForeignKey(i => i.StorageLocationID)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            // InventoryItem -> ItemPhoto (1:1)
            modelBuilder.Entity<InventoryItem>()
                .HasOne(i => i.ItemPhoto)
                .WithOne(p => p.InventoryItem)
                .HasForeignKey<ItemPhoto>(p => p.InventoryItemID)
                .OnDelete(DeleteBehavior.Cascade);

            // InventoryItem -> ItemThumbnail (1:1)
            modelBuilder.Entity<InventoryItem>()
                .HasOne(i => i.ItemThumbnail)
                .WithOne(t => t.InventoryItem)
                .HasForeignKey<ItemThumbnail>(t => t.InventoryItemID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InventoryItem>()
                .HasIndex(i => i.SKU)
                .IsUnique();

            modelBuilder.Entity<StockTransaction>()
                .HasIndex(t => t.ReferenceNumber)
                .IsUnique();

            base.OnModelCreating(modelBuilder);
        }
    }
}