using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qrbound.POS.Data.Migrations
{
    /// <inheritdoc />
    public partial class OrderCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerEmail",
                schema: "pos",
                table: "Orders",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerUserId",
                schema: "pos",
                table: "Orders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerUserId",
                schema: "pos",
                table: "Orders",
                column: "CustomerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_CustomerUserId",
                schema: "pos",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerEmail",
                schema: "pos",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerUserId",
                schema: "pos",
                table: "Orders");
        }
    }
}
