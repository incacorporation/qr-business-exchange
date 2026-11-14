using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QRBusinessExchange.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileFieldVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowBio",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowCompanyName",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowEmail",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowPhoneNumber",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowProfileImage",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowServicePricing",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowSkills",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowBio",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ShowCompanyName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ShowEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ShowPhoneNumber",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ShowProfileImage",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ShowServicePricing",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ShowSkills",
                table: "Users");
        }
    }
}
