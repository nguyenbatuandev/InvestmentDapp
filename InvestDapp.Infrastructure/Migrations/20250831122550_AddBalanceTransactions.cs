using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestDapp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBalanceTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InternalOrderId",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BalanceTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserWallet = table.Column<string>(type: "nvarchar(42)", maxLength: 42, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BalanceTransactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BalanceTransactions_Type",
                table: "BalanceTransactions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_BalanceTransactions_UserWallet",
                table: "BalanceTransactions",
                column: "UserWallet");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BalanceTransactions");

            migrationBuilder.DropColumn(
                name: "InternalOrderId",
                table: "Orders");
        }
    }
}
