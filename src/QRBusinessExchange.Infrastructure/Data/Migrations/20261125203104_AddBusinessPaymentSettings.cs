using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QRBusinessExchange.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessPaymentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessPaymentSettings",
                columns: table => new
                {
                    BusinessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MinAmountCents = table.Column<long>(type: "bigint", nullable: true),
                    MaxAmountCents = table.Column<long>(type: "bigint", nullable: true),
                    DefaultExpiresInMinutes = table.Column<int>(type: "int", nullable: false),
                    AllowSavedCards = table.Column<bool>(type: "bit", nullable: false),
                    ManagersOnly = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPaymentSettings", x => x.BusinessId);
                    table.ForeignKey(
                        name: "FK_BusinessPaymentSettings_Businesses",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessPaymentSettings");
        }
    }
}
