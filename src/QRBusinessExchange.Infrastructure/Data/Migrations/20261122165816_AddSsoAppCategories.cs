using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QRBusinessExchange.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSsoAppCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequireApprovedCategory",
                table: "SsoApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RequestedCategory",
                table: "QrLoginSessions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SsoAppCategories",
                columns: table => new
                {
                    SsoAppCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    SsoApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    RequiresBusiness = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SsoAppCategories", x => x.SsoAppCategoryId);
                    table.ForeignKey(
                        name: "FK_SsoAppCategories_SsoApplications",
                        column: x => x.SsoApplicationId,
                        principalTable: "SsoApplications",
                        principalColumn: "SsoApplicationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SsoAppAccess",
                columns: table => new
                {
                    SsoAppAccessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    SsoApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SsoAppCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SsoAppAccess", x => x.SsoAppAccessId);
                    table.ForeignKey(
                        name: "FK_SsoAppAccess_Businesses",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId");
                    table.ForeignKey(
                        name: "FK_SsoAppAccess_SsoAppCategories",
                        column: x => x.SsoAppCategoryId,
                        principalTable: "SsoAppCategories",
                        principalColumn: "SsoAppCategoryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SsoAppAccess_SsoApplications",
                        column: x => x.SsoApplicationId,
                        principalTable: "SsoApplications",
                        principalColumn: "SsoApplicationId");
                    table.ForeignKey(
                        name: "FK_SsoAppAccess_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SsoAppAccess_Application_Status",
                table: "SsoAppAccess",
                columns: new[] { "SsoApplicationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SsoAppAccess_BusinessId",
                table: "SsoAppAccess",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_SsoAppAccess_Category_User",
                table: "SsoAppAccess",
                columns: new[] { "SsoAppCategoryId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SsoAppAccess_UserId",
                table: "SsoAppAccess",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SsoAppCategories_Application_Key",
                table: "SsoAppCategories",
                columns: new[] { "SsoApplicationId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SsoAppAccess");

            migrationBuilder.DropTable(
                name: "SsoAppCategories");

            migrationBuilder.DropColumn(
                name: "RequireApprovedCategory",
                table: "SsoApplications");

            migrationBuilder.DropColumn(
                name: "RequestedCategory",
                table: "QrLoginSessions");
        }
    }
}
