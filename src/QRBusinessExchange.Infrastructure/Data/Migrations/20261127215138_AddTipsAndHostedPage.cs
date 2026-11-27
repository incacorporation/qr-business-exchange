using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QRBusinessExchange.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTipsAndHostedPage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtraTransactionId",
                table: "QrPaymentRequests",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HostedStartedAt",
                table: "QrPaymentRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PaidAmountCents",
                table: "QrPaymentRequests",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "QrPaymentRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QuoteExpiresAt",
                table: "QrPaymentRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "QuoteId",
                table: "QrPaymentRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "QuoteTipCents",
                table: "QrPaymentRequests",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TipCents",
                table: "QrPaymentRequests",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "AllowCustomTip",
                table: "BusinessPaymentSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxTipPercent",
                table: "BusinessPaymentSettings",
                type: "int",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<string>(
                name: "TipPercentages",
                table: "BusinessPaymentSettings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "15,18,20");

            migrationBuilder.AddColumn<bool>(
                name: "TipsEnabled",
                table: "BusinessPaymentSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Bills paid before tips existed were charged exactly their amount. How they were paid
            // wasn't recorded, so PaymentMethod stays empty for them.
            migrationBuilder.Sql(
                "UPDATE QrPaymentRequests SET PaidAmountCents = AmountCents WHERE Status = 'Paid' AND PaidAmountCents IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtraTransactionId",
                table: "QrPaymentRequests");

            migrationBuilder.DropColumn(
                name: "HostedStartedAt",
                table: "QrPaymentRequests");

            migrationBuilder.DropColumn(
                name: "PaidAmountCents",
                table: "QrPaymentRequests");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "QrPaymentRequests");

            migrationBuilder.DropColumn(
                name: "QuoteExpiresAt",
                table: "QrPaymentRequests");

            migrationBuilder.DropColumn(
                name: "QuoteId",
                table: "QrPaymentRequests");

            migrationBuilder.DropColumn(
                name: "QuoteTipCents",
                table: "QrPaymentRequests");

            migrationBuilder.DropColumn(
                name: "TipCents",
                table: "QrPaymentRequests");

            migrationBuilder.DropColumn(
                name: "AllowCustomTip",
                table: "BusinessPaymentSettings");

            migrationBuilder.DropColumn(
                name: "MaxTipPercent",
                table: "BusinessPaymentSettings");

            migrationBuilder.DropColumn(
                name: "TipPercentages",
                table: "BusinessPaymentSettings");

            migrationBuilder.DropColumn(
                name: "TipsEnabled",
                table: "BusinessPaymentSettings");
        }
    }
}
