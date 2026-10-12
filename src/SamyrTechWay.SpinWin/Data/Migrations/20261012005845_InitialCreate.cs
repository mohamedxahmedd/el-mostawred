using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SamyrTechWay.SpinWin.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AdminName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TargetId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NormalizedPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InvoiceReference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    LaptopModel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AccessCodeHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccessCodeVersion = table.Column<int>(type: "int", nullable: false),
                    SpinCredits = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    LockoutEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                    table.CheckConstraint("CK_Customers_SpinCredits", "[SpinCredits] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FriendlyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Xml = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrizeImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ByteLength = table.Column<int>(type: "int", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrizeImages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SiteTitle = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PromoSubheadingEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PromoSubheadingAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PickupInstructionsEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PickupInstructionsAr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AnnouncementEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AnnouncementAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PausedMessageEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PausedMessageAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DefaultLanguage = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    SoundEnabledByDefault = table.Column<bool>(type: "bit", nullable: false),
                    SpinDurationMinMs = table.Column<int>(type: "int", nullable: false),
                    SpinDurationMaxMs = table.Column<int>(type: "int", nullable: false),
                    AnimationIntensity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CampaignEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteSettings", x => x.Id);
                    table.CheckConstraint("CK_SiteSettings_Duration", "[SpinDurationMinMs] >= 1500 AND [SpinDurationMaxMs] <= 12000 AND [SpinDurationMinMs] <= [SpinDurationMaxMs]");
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WheelItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameEn = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DescriptionAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    ProbabilityBasisPoints = table.Column<int>(type: "int", nullable: false),
                    ImageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IconKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ColorKey = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StockQuantity = table.Column<int>(type: "int", nullable: true),
                    LowStockThreshold = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CelebrationTier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CollectionNoteEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CollectionNoteAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WheelItems", x => x.Id);
                    table.CheckConstraint("CK_WheelItems_Probability", "[ProbabilityBasisPoints] >= 0 AND [ProbabilityBasisPoints] <= 10000");
                    table.CheckConstraint("CK_WheelItems_Stock", "[StockQuantity] IS NULL OR [StockQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_WheelItems_PrizeImages_ImageId",
                        column: x => x.ImageId,
                        principalTable: "PrizeImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpinAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    WheelItemId = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ItemNameEn = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ItemNameAr = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ItemKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ImageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IconKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ColorKey = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CelebrationTier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ConfiguredBasisPoints = table.Column<int>(type: "int", nullable: false),
                    EffectiveWeight = table.Column<int>(type: "int", nullable: false),
                    EffectiveTotalWeight = table.Column<int>(type: "int", nullable: false),
                    CreditsBefore = table.Column<int>(type: "int", nullable: false),
                    CreditsAfter = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpinAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpinAttempts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpinAttempts_WheelItems_WheelItemId",
                        column: x => x.WheelItemId,
                        principalTable: "WheelItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Awards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SpinAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    WheelItemId = table.Column<int>(type: "int", nullable: false),
                    PrizeNameEn = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PrizeNameAr = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ImageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IconKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ColorKey = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CelebrationTier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CollectionNoteEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CollectionNoteAr = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredByAdminId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DeliveredByAdminName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AdminNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Awards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Awards_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Awards_SpinAttempts_SpinAttemptId",
                        column: x => x.SpinAttemptId,
                        principalTable: "SpinAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Awards_WheelItems_WheelItemId",
                        column: x => x.WheelItemId,
                        principalTable: "WheelItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Delta = table.Column<int>(type: "int", nullable: false),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SpinAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdminUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AdminName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditLedgerEntries_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditLedgerEntries_SpinAttempts_SpinAttemptId",
                        column: x => x.SpinAttemptId,
                        principalTable: "SpinAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_CreatedAtUtc",
                table: "AdminAuditLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Awards_CustomerId",
                table: "Awards",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Awards_SpinAttemptId",
                table: "Awards",
                column: "SpinAttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Awards_Status_CreatedAtUtc",
                table: "Awards",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Awards_WheelItemId",
                table: "Awards",
                column: "WheelItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedgerEntries_CustomerId_CreatedAtUtc",
                table: "CreditLedgerEntries",
                columns: new[] { "CustomerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedgerEntries_SpinAttemptId",
                table: "CreditLedgerEntries",
                column: "SpinAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CreatedAtUtc",
                table: "Customers",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_InvoiceReference",
                table: "Customers",
                column: "InvoiceReference");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_NormalizedPhone",
                table: "Customers",
                column: "NormalizedPhone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpinAttempts_CreatedAtUtc",
                table: "SpinAttempts",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SpinAttempts_CustomerId_RequestId",
                table: "SpinAttempts",
                columns: new[] { "CustomerId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpinAttempts_WheelItemId",
                table: "SpinAttempts",
                column: "WheelItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WheelItems_ImageId",
                table: "WheelItems",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "UX_WheelItems_SingleActiveTryAgain",
                table: "WheelItems",
                column: "Kind",
                unique: true,
                filter: "[Kind] = 'TryAgain' AND [IsActive] = 1 AND [IsArchived] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditLogs");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Awards");

            migrationBuilder.DropTable(
                name: "CreditLedgerEntries");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "SiteSettings");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "SpinAttempts");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "WheelItems");

            migrationBuilder.DropTable(
                name: "PrizeImages");
        }
    }
}
