using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestDapp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeToWalletWithdrawalRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Fee",
                table: "WalletWithdrawalRequests",
                type: "decimal(18,8)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "TradingAccountLocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserWallet = table.Column<string>(type: "nvarchar(42)", maxLength: 42, nullable: false),
                    LockType = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LockedByAdmin = table.Column<string>(type: "nvarchar(42)", maxLength: 42, nullable: false),
                    LockedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsUnlocked = table.Column<bool>(type: "bit", nullable: false),
                    UnlockedByAdmin = table.Column<string>(type: "nvarchar(42)", maxLength: 42, nullable: true),
                    UnlockedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UnlockReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingAccountLocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradingFeeConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MakerFeePercent = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TakerFeePercent = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    WithdrawalFeePercent = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MinWithdrawalFee = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    MinWithdrawalAmount = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    MaxWithdrawalAmount = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    DailyWithdrawalLimit = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingFeeConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradingAccountLocks_IsUnlocked",
                table: "TradingAccountLocks",
                column: "IsUnlocked");

            migrationBuilder.CreateIndex(
                name: "IX_TradingAccountLocks_UserWallet",
                table: "TradingAccountLocks",
                column: "UserWallet");

            migrationBuilder.CreateIndex(
                name: "IX_TradingFeeConfigs_IsActive",
                table: "TradingFeeConfigs",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradingAccountLocks");

            migrationBuilder.DropTable(
                name: "TradingFeeConfigs");

            migrationBuilder.DropColumn(
                name: "Fee",
                table: "WalletWithdrawalRequests");
        }
    }
}
