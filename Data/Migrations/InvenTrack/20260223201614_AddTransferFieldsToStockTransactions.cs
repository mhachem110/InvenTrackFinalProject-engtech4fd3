using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvenTrackFinalProject.Data.Migrations.InvenTrack
{
    /// <inheritdoc />
    public partial class AddTransferFieldsToStockTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FromStorageLocationID",
                table: "StockTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToStorageLocationID",
                table: "StockTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_FromStorageLocationID",
                table: "StockTransactions",
                column: "FromStorageLocationID");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_ToStorageLocationID",
                table: "StockTransactions",
                column: "ToStorageLocationID");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockTransactions_StorageLocations_FromStorageLocationID",
                table: "StockTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransactions_StorageLocations_ToStorageLocationID",
                table: "StockTransactions");

            migrationBuilder.DropIndex(
                name: "IX_StockTransactions_FromStorageLocationID",
                table: "StockTransactions");

            migrationBuilder.DropIndex(
                name: "IX_StockTransactions_ToStorageLocationID",
                table: "StockTransactions");

            migrationBuilder.DropColumn(
                name: "FromStorageLocationID",
                table: "StockTransactions");

            migrationBuilder.DropColumn(
                name: "ToStorageLocationID",
                table: "StockTransactions");
        }
    }
}
