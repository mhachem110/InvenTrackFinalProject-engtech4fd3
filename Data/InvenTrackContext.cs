using InvenTrack.Models;
using Microsoft.EntityFrameworkCore;

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
        public DbSet<StockTransferRequest> StockTransferRequests { get; set; }
        public DbSet<ChatConversation> ChatConversations { get; set; }
        public DbSet<ChatConversationMember> ChatConversationMembers { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<InventoryOrderRequest> InventoryOrderRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StockTransaction>()
                .HasOne(t => t.InventoryItem)
                .WithMany(i => i.StockTransactions)
                .HasForeignKey(t => t.InventoryItemID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockTransaction>()
                .HasOne(t => t.FromStorageLocation)
                .WithMany(l => l.FromStockTransactions)
                .HasForeignKey(t => t.FromStorageLocationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockTransaction>()
                .HasOne(t => t.ToStorageLocation)
                .WithMany(l => l.ToStockTransactions)
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

            modelBuilder.Entity<InventoryItem>()
                .HasIndex(i => i.Barcode)
                .IsUnique()
                .HasFilter("[Barcode] IS NOT NULL AND [Barcode] <> ''");

            modelBuilder.Entity<StockTransaction>()
                .HasIndex(t => t.ReferenceNumber)
                .IsUnique()
                .HasFilter("[ReferenceNumber] IS NOT NULL");

            modelBuilder.Entity<InventoryItemStock>()
                .HasIndex(x => new { x.InventoryItemID, x.StorageLocationID })
                .IsUnique();

            modelBuilder.Entity<InventoryItemStock>()
                .HasOne(x => x.InventoryItem)
                .WithMany(i => i.InventoryItemStocks)
                .HasForeignKey(x => x.InventoryItemID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InventoryItemStock>()
                .HasOne(x => x.StorageLocation)
                .WithMany(l => l.InventoryItemStocks)
                .HasForeignKey(x => x.StorageLocationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockTransferRequest>()
                .HasOne(r => r.InventoryItem)
                .WithMany()
                .HasForeignKey(r => r.InventoryItemID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockTransferRequest>()
                .HasOne(r => r.FromStorageLocation)
                .WithMany(l => l.TransferRequestsFromHere)
                .HasForeignKey(r => r.FromStorageLocationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockTransferRequest>()
                .HasOne(r => r.ToStorageLocation)
                .WithMany(l => l.TransferRequestsToHere)
                .HasForeignKey(r => r.ToStorageLocationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockTransferRequest>()
                .Property(r => r.RequestedByUserId)
                .HasMaxLength(450);

            modelBuilder.Entity<StockTransferRequest>()
                .Property(r => r.ReviewedByUserId)
                .HasMaxLength(450);


            modelBuilder.Entity<InventoryOrderRequest>()
                .HasOne(x => x.InventoryItem)
                .WithMany()
                .HasForeignKey(x => x.InventoryItemID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryOrderRequest>()
                .HasOne(x => x.DestinationStorageLocation)
                .WithMany()
                .HasForeignKey(x => x.DestinationStorageLocationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryOrderRequest>()
                .HasOne(x => x.StockTransaction)
                .WithMany()
                .HasForeignKey(x => x.StockTransactionID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatConversation>()
                .Property(x => x.CreatedByUserId)
                .HasMaxLength(450);

            modelBuilder.Entity<ChatConversationMember>()
                .Property(x => x.UserId)
                .HasMaxLength(450);

            modelBuilder.Entity<ChatConversationMember>()
                .HasIndex(x => new { x.ChatConversationID, x.UserId })
                .IsUnique();

            modelBuilder.Entity<ChatConversationMember>()
                .HasOne(x => x.ChatConversation)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.ChatConversationID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessage>()
                .Property(x => x.SenderUserId)
                .HasMaxLength(450);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(x => x.ChatConversation)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ChatConversationID)
                .OnDelete(DeleteBehavior.Cascade);


            base.OnModelCreating(modelBuilder);
        }
    }
}