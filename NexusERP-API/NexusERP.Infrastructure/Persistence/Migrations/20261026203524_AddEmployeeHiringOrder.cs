using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeHiringOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Fin",
                table: "Employees",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "HiringOrderDate",
                table: "Employees",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "HiringOrderNumber",
                table: "Employees",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ProbationMonths",
                table: "Employees",
                type: "int",
                nullable: true);

            // Employees hired before hiring orders existed get one dated the day they were added (Baku time, UTC+4),
            // numbered per company and year in the order they were added.
            migrationBuilder.Sql("""
                WITH Numbered AS (
                    SELECT HiringOrderNumber, HiringOrderDate, OrderDate,
                           ROW_NUMBER() OVER (PARTITION BY CompanyId, YEAR(OrderDate) ORDER BY CreatedAt, Id) AS Seq
                    FROM (SELECT *, CAST(DATEADD(HOUR, 4, CreatedAt) AS date) AS OrderDate FROM Employees) AS e
                )
                UPDATE Numbered
                SET HiringOrderDate = OrderDate,
                    HiringOrderNumber = CONCAT(N'ƏMR-', YEAR(OrderDate), N'-', FORMAT(Seq, '000'));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_HiringOrderNumber",
                table: "Employees",
                columns: new[] { "CompanyId", "HiringOrderNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_CompanyId_HiringOrderNumber",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Fin",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "HiringOrderDate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "HiringOrderNumber",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ProbationMonths",
                table: "Employees");
        }
    }
}
