using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestDapp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletWithdrawalRequestsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WalletWithdrawalRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserWallet = table.Column<string>(type: "nvarchar(42)", maxLength: 42, nullable: false),
                    RecipientAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AdminNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletWithdrawalRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawalRequests_Status",
                table: "WalletWithdrawalRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawalRequests_UserWallet",
                table: "WalletWithdrawalRequests",
                column: "UserWallet");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WalletWithdrawalRequests");
        }
    }
}
