using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every existing sale was entered in NexusERP: SaleChannel.Direct.
            migrationBuilder.AddColumn<int>(
                name: "Channel",
                table: "Sales",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ExternalOrderNumber",
                table: "Sales",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetailCustomerAddress",
                table: "Sales",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetailCustomerEmail",
                table: "Sales",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetailCustomerName",
                table: "Sales",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetailCustomerPhone",
                table: "Sales",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemKey",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CompanyId_ExternalOrderNumber",
                table: "Sales",
                columns: new[] { "CompanyId", "ExternalOrderNumber" },
                unique: true,
                filter: "[ExternalOrderNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CompanyId_SystemKey",
                table: "Customers",
                columns: new[] { "CompanyId", "SystemKey" },
                unique: true,
                filter: "[SystemKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_CompanyId_ExternalOrderNumber",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CompanyId_SystemKey",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ExternalOrderNumber",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "RetailCustomerAddress",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "RetailCustomerEmail",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "RetailCustomerName",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "RetailCustomerPhone",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "SystemKey",
                table: "Customers");
        }
    }
}
