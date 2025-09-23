using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestDapp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProfitClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProfitClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfitId = table.Column<int>(type: "int", nullable: false),
                    ClaimerWallet = table.Column<string>(type: "nvarchar(42)", maxLength: 42, nullable: false),
                    TransactionHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClaimedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfitClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfitClaims_Profits_ProfitId",
                        column: x => x.ProfitId,
                        principalTable: "Profits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfitClaims_ClaimerWallet",
                table: "ProfitClaims",
                column: "ClaimerWallet");

            migrationBuilder.CreateIndex(
                name: "IX_ProfitClaims_ProfitId",
                table: "ProfitClaims",
                column: "ProfitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfitClaims");
        }
    }
}
