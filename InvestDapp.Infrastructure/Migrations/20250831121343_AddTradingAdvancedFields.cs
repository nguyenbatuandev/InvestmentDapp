using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestDapp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTradingAdvancedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsIsolated",
                table: "Positions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LiquidationPrice",
                table: "Positions",
                type: "decimal(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaintenanceMarginRate",
                table: "Positions",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "StopLossPrice",
                table: "Positions",
                type: "decimal(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TakeProfitPrice",
                table: "Positions",
                type: "decimal(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReduceOnly",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "StopLossPrice",
                table: "Orders",
                type: "decimal(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TakeProfitPrice",
                table: "Orders",
                type: "decimal(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsIsolated",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "LiquidationPrice",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "MaintenanceMarginRate",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "StopLossPrice",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "TakeProfitPrice",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "ReduceOnly",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "StopLossPrice",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TakeProfitPrice",
                table: "Orders");
        }
    }
}
