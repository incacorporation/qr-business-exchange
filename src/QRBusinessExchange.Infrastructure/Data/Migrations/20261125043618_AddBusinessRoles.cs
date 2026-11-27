using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QRBusinessExchange.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessRoles",
                columns: table => new
                {
                    BusinessRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    BusinessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessRoles", x => x.BusinessRoleId);
                    table.ForeignKey(
                        name: "FK_BusinessRoles_Businesses",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessRoles_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessRoles_Business_User_Role",
                table: "BusinessRoles",
                columns: new[] { "BusinessId", "UserId", "RoleName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessRoles_UserId",
                table: "BusinessRoles",
                column: "UserId");

            // SuperAdmin can only be held by gmacha@incacorporation.com (BusinessRoleNames.SuperAdminEmail).
            migrationBuilder.Sql(@"
CREATE TRIGGER [dbo].[TR_BusinessRoles_SuperAdmin]
ON [dbo].[BusinessRoles]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN [dbo].[Users] u ON u.UserId = i.UserId
        WHERE i.RoleName = N'SuperAdmin'
          AND LOWER(LTRIM(RTRIM(u.Email))) <> N'gmacha@incacorporation.com')
    BEGIN
        THROW 50001, N'The SuperAdmin role can only be assigned to gmacha@incacorporation.com.', 1;
    END
END");

            // Existing businesses: their owner becomes the Administrator, as for new businesses.
            migrationBuilder.Sql(@"
INSERT INTO [dbo].[BusinessRoles] (BusinessId, UserId, RoleName)
SELECT b.BusinessId, b.OwnerUserId, N'Administrator'
FROM [dbo].[Businesses] b;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the table also drops its trigger.
            migrationBuilder.DropTable(
                name: "BusinessRoles");
        }
    }
}
