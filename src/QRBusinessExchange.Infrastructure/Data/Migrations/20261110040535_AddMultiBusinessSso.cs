using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QRBusinessExchange.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiBusinessSso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Businesses",
                columns: table => new
                {
                    BusinessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Businesses", x => x.BusinessId);
                    table.ForeignKey(
                        name: "FK_Businesses_OwnerUser",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessMembers",
                columns: table => new
                {
                    BusinessMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    BusinessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessMembers", x => x.BusinessMemberId);
                    table.ForeignKey(
                        name: "FK_BusinessMembers_Businesses",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessMembers_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SsoApplications",
                columns: table => new
                {
                    SsoApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    BusinessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClientSecretHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RedirectUris = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequireMembership = table.Column<bool>(type: "bit", nullable: false),
                    TokenLifetimeMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SsoApplications", x => x.SsoApplicationId);
                    table.ForeignKey(
                        name: "FK_SsoApplications_Businesses",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "BusinessId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QrLoginSessions",
                columns: table => new
                {
                    QrLoginSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PollSecretHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SsoApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VerificationCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RequesterIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RequesterUserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrLoginSessions", x => x.QrLoginSessionId);
                    table.ForeignKey(
                        name: "FK_QrLoginSessions_SsoApplications",
                        column: x => x.SsoApplicationId,
                        principalTable: "SsoApplications",
                        principalColumn: "SsoApplicationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QrLoginSessions_Users",
                        column: x => x.ApprovedUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SsoAuthorizationCodes",
                columns: table => new
                {
                    SsoAuthorizationCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SsoApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RedirectUri = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CodeChallenge = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CodeChallengeMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Nonce = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AuthMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AuthTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SsoAuthorizationCodes", x => x.SsoAuthorizationCodeId);
                    table.ForeignKey(
                        name: "FK_SsoAuthorizationCodes_SsoApplications",
                        column: x => x.SsoApplicationId,
                        principalTable: "SsoApplications",
                        principalColumn: "SsoApplicationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SsoAuthorizationCodes_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_OwnerUserId",
                table: "Businesses",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_Slug",
                table: "Businesses",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessMembers_Business_User",
                table: "BusinessMembers",
                columns: new[] { "BusinessId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessMembers_UserId",
                table: "BusinessMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_QrLoginSessions_ApprovedUserId",
                table: "QrLoginSessions",
                column: "ApprovedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QrLoginSessions_ExpiresAt",
                table: "QrLoginSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_QrLoginSessions_SsoApplicationId",
                table: "QrLoginSessions",
                column: "SsoApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_SsoApplications_BusinessId",
                table: "SsoApplications",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_SsoApplications_ClientId",
                table: "SsoApplications",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SsoAuthorizationCodes_CodeHash",
                table: "SsoAuthorizationCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SsoAuthorizationCodes_ExpiresAt",
                table: "SsoAuthorizationCodes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_SsoAuthorizationCodes_SsoApplicationId",
                table: "SsoAuthorizationCodes",
                column: "SsoApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_SsoAuthorizationCodes_UserId",
                table: "SsoAuthorizationCodes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessMembers");

            migrationBuilder.DropTable(
                name: "QrLoginSessions");

            migrationBuilder.DropTable(
                name: "SsoAuthorizationCodes");

            migrationBuilder.DropTable(
                name: "SsoApplications");

            migrationBuilder.DropTable(
                name: "Businesses");
        }
    }
}
