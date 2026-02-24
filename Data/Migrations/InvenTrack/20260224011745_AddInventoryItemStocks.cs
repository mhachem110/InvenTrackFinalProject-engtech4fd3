using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvenTrackFinalProject.Data.Migrations.InvenTrack
{
    /// <inheritdoc />
    public partial class AddInventoryItemStocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockTransactions_StorageLocations_FromStorageLocationID",
                table: "StockTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransactions_StorageLocations_ToStorageLocationID",
                table: "StockTransactions");

            migrationBuilder.CreateTable(
                name: "InventoryItemStocks",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryItemID = table.Column<int>(type: "int", nullable: false),
                    StorageLocationID = table.Column<int>(type: "int", nullable: false),
                    QuantityOnHand = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItemStocks", x => x.ID);
                    table.ForeignKey(
                        name: "FK_InventoryItemStocks_InventoryItems_InventoryItemID",
                        column: x => x.InventoryItemID,
                        principalTable: "InventoryItems",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryItemStocks_StorageLocations_StorageLocationID",
                        column: x => x.StorageLocationID,
                        principalTable: "StorageLocations",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItemStocks_InventoryItemID_StorageLocationID",
                table: "InventoryItemStocks",
                columns: new[] { "InventoryItemID", "StorageLocationID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItemStocks_StorageLocationID",
                table: "InventoryItemStocks",
                column: "StorageLocationID");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransactions_StorageLocations_FromStorageLocationID",
                table: "StockTransactions",
                column: "FromStorageLocationID",
                principalTable: "StorageLocations",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransactions_StorageLocations_ToStorageLocationID",
                table: "StockTransactions",
                column: "ToStorageLocationID",
                principalTable: "StorageLocations",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockTransactions_StorageLocations_FromStorageLocationID",
                table: "StockTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransactions_StorageLocations_ToStorageLocationID",
                table: "StockTransactions");

            migrationBuilder.DropTable(
                name: "InventoryItemStocks");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransactions_StorageLocations_FromStorageLocationID",
                table: "StockTransactions",
                column: "FromStorageLocationID",
                principalTable: "StorageLocations",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransactions_StorageLocations_ToStorageLocationID",
                table: "StockTransactions",
                column: "ToStorageLocationID",
                principalTable: "StorageLocations",
                principalColumn: "ID");
        }
    }
}
