using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestDapp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefundTableRecreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Save existing refund data temporarily
            migrationBuilder.Sql(@"
                SELECT r.Id AS CampaignId, r.InvestorAddress, CAST(r.Amount AS NVARCHAR(50)) AS AmountInWei, r.CreatedAt AS ClaimedAt, r.TransactionHash, NULL AS RefundReason
                INTO #TempRefunds
                FROM Refunds r
            ");

            // Drop the old table
            migrationBuilder.DropTable(
                name: "Refunds");

            // Recreate table with proper structure
            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    InvestorAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AmountInWei = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransactionHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefundReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Refunds_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_CampaignId",
                table: "Refunds",
                column: "CampaignId");

            // Restore data with new structure
            migrationBuilder.Sql(@"
                INSERT INTO Refunds (CampaignId, InvestorAddress, AmountInWei, ClaimedAt, TransactionHash, RefundReason)
                SELECT CampaignId, InvestorAddress, AmountInWei, ClaimedAt, TransactionHash, RefundReason
                FROM #TempRefunds
                
                DROP TABLE #TempRefunds
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Refunds_Campaigns_CampaignId",
                table: "Refunds");

            migrationBuilder.DropIndex(
                name: "IX_Refunds_CampaignId",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "Refunds");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Refunds",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Refunds_Campaigns_Id",
                table: "Refunds",
                column: "Id",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
