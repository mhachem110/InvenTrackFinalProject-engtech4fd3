using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvenTrackFinalProject.Data.Migrations.InvenTrack
{
    public partial class ReconcileChatNotifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[ChatConversations]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatConversations]
    (
        [ID] INT NOT NULL IDENTITY(1,1),
        [Name] NVARCHAR(120) NOT NULL,
        [IsGroupChat] BIT NOT NULL,
        [CreatedByUserId] NVARCHAR(450) NOT NULL,
        [CreatedByName] NVARCHAR(120) NULL,
        [DateCreated] DATETIME2 NOT NULL,
        [LastMessageAt] DATETIME2 NULL,
        CONSTRAINT [PK_ChatConversations] PRIMARY KEY ([ID])
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[ChatConversationMembers]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatConversationMembers]
    (
        [ID] INT NOT NULL IDENTITY(1,1),
        [ChatConversationID] INT NOT NULL,
        [UserId] NVARCHAR(450) NOT NULL,
        [DisplayName] NVARCHAR(120) NOT NULL,
        [IsAdmin] BIT NOT NULL,
        [JoinedAt] DATETIME2 NOT NULL,
        [LeftAt] DATETIME2 NULL,
        CONSTRAINT [PK_ChatConversationMembers] PRIMARY KEY ([ID]),
        CONSTRAINT [FK_ChatConversationMembers_ChatConversations_ChatConversationID]
            FOREIGN KEY ([ChatConversationID]) REFERENCES [dbo].[ChatConversations]([ID]) ON DELETE CASCADE
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[ChatMessages]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatMessages]
    (
        [ID] INT NOT NULL IDENTITY(1,1),
        [ChatConversationID] INT NOT NULL,
        [SenderUserId] NVARCHAR(450) NOT NULL,
        [SenderDisplayName] NVARCHAR(120) NOT NULL,
        [Body] NVARCHAR(4000) NOT NULL,
        [DateSent] DATETIME2 NOT NULL,
        [IsSystemMessage] BIT NOT NULL CONSTRAINT [DF_ChatMessages_IsSystemMessage] DEFAULT(0),
        CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([ID]),
        CONSTRAINT [FK_ChatMessages_ChatConversations_ChatConversationID]
            FOREIGN KEY ([ChatConversationID]) REFERENCES [dbo].[ChatConversations]([ID]) ON DELETE CASCADE
    );
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.ChatConversationMembers', 'LastReadAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[ChatConversationMembers]
    ADD [LastReadAt] DATETIME2 NULL;
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ChatConversationMembers_ChatConversationID_UserId'
      AND object_id = OBJECT_ID(N'[dbo].[ChatConversationMembers]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_ChatConversationMembers_ChatConversationID_UserId]
        ON [dbo].[ChatConversationMembers]([ChatConversationID], [UserId]);
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ChatMessages_ChatConversationID'
      AND object_id = OBJECT_ID(N'[dbo].[ChatMessages]')
)
BEGIN
    CREATE INDEX [IX_ChatMessages_ChatConversationID]
        ON [dbo].[ChatMessages]([ChatConversationID]);
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ChatMessages_ChatConversationID'
      AND object_id = OBJECT_ID(N'[dbo].[ChatMessages]')
)
BEGIN
    DROP INDEX [IX_ChatMessages_ChatConversationID] ON [dbo].[ChatMessages];
END
");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ChatConversationMembers_ChatConversationID_UserId'
      AND object_id = OBJECT_ID(N'[dbo].[ChatConversationMembers]')
)
BEGIN
    DROP INDEX [IX_ChatConversationMembers_ChatConversationID_UserId] ON [dbo].[ChatConversationMembers];
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.ChatConversationMembers', 'LastReadAt') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[ChatConversationMembers]
    DROP COLUMN [LastReadAt];
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[ChatMessages]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[ChatMessages];
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[ChatConversationMembers]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[ChatConversationMembers];
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[ChatConversations]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[ChatConversations];
END
");
        }
    }
}