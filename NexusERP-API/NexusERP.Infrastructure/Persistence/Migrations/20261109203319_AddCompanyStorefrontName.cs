using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyStorefrontName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StorefrontName",
                table: "Companies",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Companies that already work with the Khazar store (they received online orders, or imported its catalog) keep
            // the connection; every other company starts without one and no longer sends products there.
            migrationBuilder.Sql("""
                UPDATE Companies SET StorefrontName = N'Khazar'
                WHERE EXISTS (SELECT 1 FROM Sales s WHERE s.CompanyId = Companies.Id AND s.Channel = 2)
                   OR EXISTS (SELECT 1 FROM Categories c WHERE c.CompanyId = Companies.Id AND c.Name = N'Khazar')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StorefrontName",
                table: "Companies");
        }
    }
}
