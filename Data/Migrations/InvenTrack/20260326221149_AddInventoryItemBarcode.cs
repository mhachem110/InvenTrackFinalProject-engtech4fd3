using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvenTrackFinalProject.Data.Migrations.InvenTrack
{
    /// <inheritdoc />
    public partial class AddInventoryItemBarcode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "InventoryItems",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_Barcode",
                table: "InventoryItems",
                column: "Barcode",
                unique: true,
                filter: "[Barcode] IS NOT NULL AND [Barcode] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_Barcode",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "InventoryItems");
        }
    }
}
