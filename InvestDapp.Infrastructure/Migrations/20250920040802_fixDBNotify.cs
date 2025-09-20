using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestDapp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class fixDBNotify : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop columns only if they exist to make this migration safe to run multiple times
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE Name = N'Amount' AND Object_ID = OBJECT_ID(N'[dbo].[Refunds]')
)
BEGIN
    ALTER TABLE [dbo].[Refunds] DROP COLUMN [Amount];
END");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE Name = N'CreatedAt' AND Object_ID = OBJECT_ID(N'[dbo].[Refunds]')
)
BEGIN
    ALTER TABLE [dbo].[Refunds] DROP COLUMN [CreatedAt];
END");

            migrationBuilder.AlterColumn<string>(
                name: "TransactionHash",
                table: "Refunds",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // Add AmountInWei only if it doesn't already exist
            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE Name = N'AmountInWei' AND Object_ID = OBJECT_ID(N'[dbo].[Refunds]')
)
BEGIN
    ALTER TABLE [dbo].[Refunds] ADD [AmountInWei] nvarchar(max) NOT NULL CONSTRAINT DF_Refunds_AmountInWei DEFAULT('');
END");

            // Add ClaimedAt only if it doesn't already exist
            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE Name = N'ClaimedAt' AND Object_ID = OBJECT_ID(N'[dbo].[Refunds]')
)
BEGIN
    ALTER TABLE [dbo].[Refunds] ADD [ClaimedAt] datetime2 NULL;
END");

            // Add RefundReason only if it doesn't already exist
            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE Name = N'RefundReason' AND Object_ID = OBJECT_ID(N'[dbo].[Refunds]')
)
BEGIN
    ALTER TABLE [dbo].[Refunds] ADD [RefundReason] nvarchar(max) NULL;
END");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Notifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Data",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountInWei",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "RefundReason",
                table: "Refunds");

            migrationBuilder.AlterColumn<string>(
                name: "TransactionHash",
                table: "Refunds",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Amount",
                table: "Refunds",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Refunds",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Notifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Data",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
