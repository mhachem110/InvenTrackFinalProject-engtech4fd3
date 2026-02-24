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
        public DbSet<InventoryItemStock> InventoryItemStocks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StockTransaction>()
                .HasOne(t => t.InventoryItem)
                .WithMany(i => i.StockTransactions)
                .HasForeignKey(t => t.InventoryItemID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockTransaction>()
                .HasOne(t => t.FromStorageLocation)
                .WithMany()
                .HasForeignKey(t => t.FromStorageLocationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockTransaction>()
                .HasOne(t => t.ToStorageLocation)
                .WithMany()
                .HasForeignKey(t => t.ToStorageLocationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryItem>()
                .HasOne(i => i.Category)
                .WithMany(c => c.InventoryItems)
                .HasForeignKey(i => i.CategoryID)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryItem>()
                .HasOne(i => i.StorageLocation)
                .WithMany(l => l.InventoryItems)
                .HasForeignKey(i => i.StorageLocationID)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryItem>()
                .HasOne(i => i.ItemPhoto)
                .WithOne(p => p.InventoryItem)
                .HasForeignKey<ItemPhoto>(p => p.InventoryItemID)
                .OnDelete(DeleteBehavior.Cascade);

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

            modelBuilder.Entity<InventoryItemStock>()
                .HasIndex(x => new { x.InventoryItemID, x.StorageLocationID })
                .IsUnique();

            modelBuilder.Entity<InventoryItemStock>()
                .HasOne(x => x.InventoryItem)
                .WithMany()
                .HasForeignKey(x => x.InventoryItemID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InventoryItemStock>()
                .HasOne(x => x.StorageLocation)
                .WithMany()
                .HasForeignKey(x => x.StorageLocationID)
                .OnDelete(DeleteBehavior.Restrict);

            base.OnModelCreating(modelBuilder);
        }
    }
}