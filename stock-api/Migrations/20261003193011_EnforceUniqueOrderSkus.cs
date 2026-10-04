using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace stock_api.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueOrderSkus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderLineItems_OrderId",
                table: "OrderLineItems");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedSku",
                table: "OrderLineItems",
                type: "text",
                nullable: true,
                computedColumnSql: "upper(btrim(\"Sku\"))",
                stored: true);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "OrderLineItems"
                        WHERE "NormalizedSku" IS NOT NULL
                        GROUP BY "OrderId", "NormalizedSku"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot enforce unique SKUs: an order already contains duplicate SKUs after trimming and case normalization.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_OrderLineItems_OrderId_NormalizedSku",
                table: "OrderLineItems",
                columns: new[] { "OrderId", "NormalizedSku" },
                unique: true,
                filter: "\"NormalizedSku\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderLineItems_OrderId_NormalizedSku",
                table: "OrderLineItems");

            migrationBuilder.DropColumn(
                name: "NormalizedSku",
                table: "OrderLineItems");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLineItems_OrderId",
                table: "OrderLineItems",
                column: "OrderId");
        }
    }
}
