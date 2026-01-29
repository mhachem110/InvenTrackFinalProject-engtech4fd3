# InvenTrack (Stage 1 Starter)

This repository is a **Stage 1 starter** for the InvenTrack project (ENGTECH 4FD3), built with **ASP.NET Core MVC** + **Entity Framework Core** + **SQLite**.

Stage 1 focus:
- Inventory domain models (Item, Category, Location, Stock Transactions)
- EF Core DbContext for inventory (`InvenTrackContext`)
- Starter CRUD screens for Inventory Items (with optional photo + thumbnail upload)

## Quick Start (Visual Studio)

1. Open the solution file:
   - `InvenTrack.sln`

2. Run the project (F5).
   - The app will create a local SQLite database file: `InvenTrackDatabase.db`
   - Seed data is added automatically the first time.

> Note: This starter uses `EnsureCreated()` for quick setup in Stage 1.
> When you're ready for migrations, switch the initializer to `Database.Migrate()` and create migrations.

## Where to Look

- Models: `Models/InventoryItem.cs`, `Models/Category.cs`, `Models/StorageLocation.cs`, `Models/StockTransaction.cs`
- DbContext: `Data/InvenTrackContext.cs`
- Seed data: `Data/InvenTrackInitializer.cs`
- Inventory CRUD: `Controllers/InventoryItemsController.cs` + `Views/InventoryItems/`

