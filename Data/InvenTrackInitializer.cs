using Microsoft.EntityFrameworkCore;
using InvenTrack.Models;

namespace InvenTrack.Data
{
    public static class InvenTrackInitializer
    {
        public static void Seed(IApplicationBuilder applicationBuilder)
        {
            using var scope = applicationBuilder.ApplicationServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<InvenTrackContext>();

            // Use migrations so schema changes work without deleting DB
            context.Database.Migrate();

            if (!context.Categories.Any())
            {
                context.Categories.AddRange(
                    new Category { Name = "IT Equipment", Description = "Computers, monitors, peripherals" },
                    new Category { Name = "Office Supplies", Description = "Consumables and stationery" }
                );
                context.SaveChanges();
            }

            if (!context.StorageLocations.Any())
            {
                context.StorageLocations.AddRange(
                    new StorageLocation { Name = "Main Office", Building = "A", Room = "101" },
                    new StorageLocation { Name = "Storage Room", Building = "A", Room = "B12" }
                );
                context.SaveChanges();
            }

            if (!context.InventoryItems.Any())
            {
                var it = context.Categories.First(c => c.Name == "IT Equipment");
                var supplies = context.Categories.First(c => c.Name == "Office Supplies");
                var office = context.StorageLocations.First(l => l.Name == "Main Office");
                var storage = context.StorageLocations.First(l => l.Name == "Storage Room");

                context.InventoryItems.AddRange(
                    new InventoryItem
                    {
                        ItemName = "Dell Laptop (Sample)",
                        SKU = "IT-0001",
                        Description = "Starter sample item for Stage 1",
                        QuantityOnHand = 3,
                        ReorderLevel = 1,
                        CategoryID = it.ID,
                        StorageLocationID = office.ID
                    },
                    new InventoryItem
                    {
                        ItemName = "Printer Paper (Sample)",
                        SKU = "SUP-0100",
                        Description = "A4 paper ream",
                        QuantityOnHand = 20,
                        ReorderLevel = 5,
                        CategoryID = supplies.ID,
                        StorageLocationID = storage.ID
                    }
                );
                context.SaveChanges();
            }

            if (!context.StockTransactions.Any())
            {
                var laptop = context.InventoryItems.First(i => i.SKU == "IT-0001");
                var paper = context.InventoryItems.First(i => i.SKU == "SUP-0100");

                context.StockTransactions.AddRange(
                    new StockTransaction
                    {
                        DateCreated = DateTime.Today,
                        ActionType = StockActionType.CheckIn,
                        QuantityChange = 3,
                        Notes = "Initial stock seed",
                        InventoryItemID = laptop.ID
                    },
                    new StockTransaction
                    {
                        DateCreated = DateTime.Today,
                        ActionType = StockActionType.CheckIn,
                        QuantityChange = 20,
                        Notes = "Initial stock seed",
                        InventoryItemID = paper.ID
                    }
                );
                context.SaveChanges();
            }
        }
    }
}