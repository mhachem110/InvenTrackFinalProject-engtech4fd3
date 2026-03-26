using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvenTrackFinalProject.Data.Migrations.Identity
{
    public partial class AddUserProfileFieldsSafe : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.AspNetUsers', 'FullName') IS NULL
BEGIN
    ALTER TABLE [dbo].[AspNetUsers]
    ADD [FullName] NVARCHAR(120) NULL;
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.AspNetUsers', 'JobTitle') IS NULL
BEGIN
    ALTER TABLE [dbo].[AspNetUsers]
    ADD [JobTitle] NVARCHAR(120) NULL;
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.AspNetUsers', 'Department') IS NULL
BEGIN
    ALTER TABLE [dbo].[AspNetUsers]
    ADD [Department] NVARCHAR(120) NULL;
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.AspNetUsers', 'AssignedStorageLocationId') IS NULL
BEGIN
    ALTER TABLE [dbo].[AspNetUsers]
    ADD [AssignedStorageLocationId] INT NULL;
END
");

            migrationBuilder.Sql(@"
UPDATE [dbo].[AspNetUsers]
SET [FullName] = N''
WHERE [FullName] IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE [dbo].[AspNetUsers]
SET [JobTitle] = N''
WHERE [JobTitle] IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE [dbo].[AspNetUsers]
SET [Department] = N''
WHERE [Department] IS NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.AspNetUsers', 'AssignedStorageLocationId') IS NOT NULL
AND OBJECT_ID(N'[dbo].[StorageLocations]', N'U') IS NOT NULL
AND NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_AspNetUsers_StorageLocations_AssignedStorageLocationId'
)
BEGIN
    ALTER TABLE [dbo].[AspNetUsers]
    ADD CONSTRAINT [FK_AspNetUsers_StorageLocations_AssignedStorageLocationId]
        FOREIGN KEY ([AssignedStorageLocationId])
        REFERENCES [dbo].[StorageLocations]([ID]);
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.AspNetUsers', 'AssignedStorageLocationId') IS NOT NULL
AND NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_AspNetUsers_AssignedStorageLocationId'
      AND object_id = OBJECT_ID(N'[dbo].[AspNetUsers]')
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_AssignedStorageLocationId]
        ON [dbo].[AspNetUsers]([AssignedStorageLocationId]);
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_AspNetUsers_StorageLocations_AssignedStorageLocationId'
)
BEGIN
    ALTER TABLE [dbo].[AspNetUsers]
    DROP CONSTRAINT [FK_AspNetUsers_StorageLocations_AssignedStorageLocationId];
END
");

            migrationBuilder.Sql(@"
IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_AspNetUsers_AssignedStorageLocationId'
      AND object_id = OBJECT_ID(N'[dbo].[AspNetUsers]')
)
BEGIN
    DROP INDEX [IX_AspNetUsers_AssignedStorageLocationId] ON [dbo].[AspNetUsers];
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.AspNetUsers', 'AssignedStorageLocationId') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[AspNetUsers]
    DROP COLUMN [AssignedStorageLocationId];
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.AspNetUsers', 'Department') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[AspNetUsers]
    DROP COLUMN [Department];
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.AspNetUsers', 'JobTitle') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[AspNetUsers]
    DROP COLUMN [JobTitle];
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.AspNetUsers', 'FullName') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[AspNetUsers]
    DROP COLUMN [FullName];
END
");
        }
    }
}