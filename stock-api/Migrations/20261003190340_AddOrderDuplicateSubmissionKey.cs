using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace stock_api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderDuplicateSubmissionKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DuplicateKey",
                table: "Orders",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DuplicateKey_CreatedAt",
                table: "Orders",
                columns: new[] { "DuplicateKey", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_DuplicateKey_CreatedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DuplicateKey",
                table: "Orders");
        }
    }
}
