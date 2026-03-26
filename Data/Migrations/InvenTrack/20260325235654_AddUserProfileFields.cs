using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvenTrackFinalProject.Data.Migrations.InvenTrack
{
    public partial class AddUserProfileFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[InventoryOrderRequests]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[InventoryOrderRequests]
    (
        [ID] INT NOT NULL IDENTITY(1,1),
        [InventoryItemID] INT NOT NULL,
        [DestinationStorageLocationID] INT NOT NULL,
        [RelatedLocationIdsCsv] NVARCHAR(500) NULL,
        [RelatedLocationNames] NVARCHAR(500) NULL,
        [CurrentVisibleQuantity] INT NOT NULL,
        [SuggestedQuantity] INT NOT NULL,
        [RequestedQuantity] INT NOT NULL,
        [Notes] NVARCHAR(500) NULL,
        [RequestedByUserId] NVARCHAR(450) NOT NULL,
        [RequestedByName] NVARCHAR(120) NOT NULL,
        [DateRequested] DATETIME2 NOT NULL,
        [Status] INT NOT NULL,
        [ReviewedByUserId] NVARCHAR(450) NULL,
        [ReviewedByName] NVARCHAR(120) NULL,
        [DateReviewed] DATETIME2 NULL,
        [ReviewDecision] NVARCHAR(200) NULL,
        [FulfilledBy] NVARCHAR(120) NULL,
        [DateFulfilled] DATETIME2 NULL,
        [StockTransactionID] INT NULL,
        [RequiresApproval] BIT NOT NULL,
        CONSTRAINT [PK_InventoryOrderRequests] PRIMARY KEY ([ID]),
        CONSTRAINT [FK_InventoryOrderRequests_InventoryItems_InventoryItemID]
            FOREIGN KEY ([InventoryItemID]) REFERENCES [dbo].[InventoryItems]([ID]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryOrderRequests_StockTransactions_StockTransactionID]
            FOREIGN KEY ([StockTransactionID]) REFERENCES [dbo].[StockTransactions]([ID]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryOrderRequests_StorageLocations_DestinationStorageLocationID]
            FOREIGN KEY ([DestinationStorageLocationID]) REFERENCES [dbo].[StorageLocations]([ID]) ON DELETE NO ACTION
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[InventoryOrderRequests]', N'U') IS NOT NULL
AND NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_InventoryOrderRequests_DestinationStorageLocationID'
      AND object_id = OBJECT_ID(N'[dbo].[InventoryOrderRequests]')
)
BEGIN
    CREATE INDEX [IX_InventoryOrderRequests_DestinationStorageLocationID]
        ON [dbo].[InventoryOrderRequests]([DestinationStorageLocationID]);
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[InventoryOrderRequests]', N'U') IS NOT NULL
AND NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_InventoryOrderRequests_InventoryItemID'
      AND object_id = OBJECT_ID(N'[dbo].[InventoryOrderRequests]')
)
BEGIN
    CREATE INDEX [IX_InventoryOrderRequests_InventoryItemID]
        ON [dbo].[InventoryOrderRequests]([InventoryItemID]);
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[InventoryOrderRequests]', N'U') IS NOT NULL
AND NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_InventoryOrderRequests_StockTransactionID'
      AND object_id = OBJECT_ID(N'[dbo].[InventoryOrderRequests]')
)
BEGIN
    CREATE INDEX [IX_InventoryOrderRequests_StockTransactionID]
        ON [dbo].[InventoryOrderRequests]([StockTransactionID]);
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[InventoryOrderRequests]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[InventoryOrderRequests];
END
");
        }
    }
}