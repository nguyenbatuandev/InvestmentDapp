using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestDapp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class fixTableRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amount",
                table: "WithdrawalRequests");

            migrationBuilder.AddColumn<string>(
                name: "InvestorAddress",
                table: "Refunds",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvestorAddress",
                table: "Refunds");

            migrationBuilder.AddColumn<double>(
                name: "Amount",
                table: "WithdrawalRequests",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
