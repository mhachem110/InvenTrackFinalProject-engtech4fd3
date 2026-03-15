using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvenTrackFinalProject.Data.Migrations.InvenTrack
{
    /// <inheritdoc />
    public partial class RevampAccessLevelsAndLocationScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedStorageLocationId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_AssignedStorageLocationId",
                table: "AspNetUsers",
                column: "AssignedStorageLocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_StorageLocations_AssignedStorageLocationId",
                table: "AspNetUsers",
                column: "AssignedStorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_StorageLocations_AssignedStorageLocationId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_AssignedStorageLocationId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AssignedStorageLocationId",
                table: "AspNetUsers");
        }
    }
}