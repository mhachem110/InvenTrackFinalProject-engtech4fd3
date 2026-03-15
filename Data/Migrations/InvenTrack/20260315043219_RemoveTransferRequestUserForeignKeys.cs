using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvenTrackFinalProject.Data.Migrations.InvenTrack
{
    /// <inheritdoc />
    public partial class RemoveTransferRequestUserForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[StockTransferRequests]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_StockTransferRequests_AspNetUsers_RequestedByUserId'
    )
    BEGIN
        ALTER TABLE [dbo].[StockTransferRequests]
        DROP CONSTRAINT [FK_StockTransferRequests_AspNetUsers_RequestedByUserId];
    END

    IF EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_StockTransferRequests_AspNetUsers_ReviewedByUserId'
    )
    BEGIN
        ALTER TABLE [dbo].[StockTransferRequests]
        DROP CONSTRAINT [FK_StockTransferRequests_AspNetUsers_ReviewedByUserId];
    END

    IF EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_StockTransferRequests_ApplicationUser_RequestedByUserId'
    )
    BEGIN
        ALTER TABLE [dbo].[StockTransferRequests]
        DROP CONSTRAINT [FK_StockTransferRequests_ApplicationUser_RequestedByUserId];
    END

    IF EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_StockTransferRequests_ApplicationUser_ReviewedByUserId'
    )
    BEGIN
        ALTER TABLE [dbo].[StockTransferRequests]
        DROP CONSTRAINT [FK_StockTransferRequests_ApplicationUser_ReviewedByUserId];
    END
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[StockTransferRequests]', N'U') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1
            FROM sys.foreign_keys
            WHERE name = N'FK_StockTransferRequests_AspNetUsers_RequestedByUserId'
        )
        BEGIN
            ALTER TABLE [dbo].[StockTransferRequests]
            ADD CONSTRAINT [FK_StockTransferRequests_AspNetUsers_RequestedByUserId]
            FOREIGN KEY ([RequestedByUserId]) REFERENCES [dbo].[AspNetUsers]([Id]);
        END

        IF NOT EXISTS (
            SELECT 1
            FROM sys.foreign_keys
            WHERE name = N'FK_StockTransferRequests_AspNetUsers_ReviewedByUserId'
        )
        BEGIN
            ALTER TABLE [dbo].[StockTransferRequests]
            ADD CONSTRAINT [FK_StockTransferRequests_AspNetUsers_ReviewedByUserId]
            FOREIGN KEY ([ReviewedByUserId]) REFERENCES [dbo].[AspNetUsers]([Id]);
        END
    END
    ELSE IF OBJECT_ID(N'[dbo].[ApplicationUser]', N'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1
            FROM sys.foreign_keys
            WHERE name = N'FK_StockTransferRequests_ApplicationUser_RequestedByUserId'
        )
        BEGIN
            ALTER TABLE [dbo].[StockTransferRequests]
            ADD CONSTRAINT [FK_StockTransferRequests_ApplicationUser_RequestedByUserId]
            FOREIGN KEY ([RequestedByUserId]) REFERENCES [dbo].[ApplicationUser]([Id]);
        END

        IF NOT EXISTS (
            SELECT 1
            FROM sys.foreign_keys
            WHERE name = N'FK_StockTransferRequests_ApplicationUser_ReviewedByUserId'
        )
        BEGIN
            ALTER TABLE [dbo].[StockTransferRequests]
            ADD CONSTRAINT [FK_StockTransferRequests_ApplicationUser_ReviewedByUserId]
            FOREIGN KEY ([ReviewedByUserId]) REFERENCES [dbo].[ApplicationUser]([Id]);
        END
    END
END
");
        }
    }
}