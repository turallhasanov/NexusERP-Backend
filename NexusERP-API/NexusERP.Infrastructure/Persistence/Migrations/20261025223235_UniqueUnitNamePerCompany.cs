using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueUnitNamePerCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Units_CompanyId",
                table: "Units");

            // Existing duplicates would block the unique index. Names are trimmed first; then, per company and name
            // (compared case-insensitively by the column collation), the unit most products use is kept, the
            // other products move to it and the extra units are deleted. Down cannot split them again.
            migrationBuilder.Sql("""
                UPDATE Units SET Name = LTRIM(RTRIM(Name))
                WHERE DATALENGTH(Name) <> DATALENGTH(LTRIM(RTRIM(Name)));

                WITH Usage AS (
                    SELECT u.Id, u.CompanyId, u.Name, COUNT(p.Id) AS ProductCount
                    FROM Units u
                    LEFT JOIN Products p ON p.UnitId = u.Id
                    GROUP BY u.Id, u.CompanyId, u.Name
                ),
                Ranked AS (
                    SELECT Id, CompanyId, Name,
                        ROW_NUMBER() OVER (PARTITION BY CompanyId, Name ORDER BY ProductCount DESC, Id) AS Position
                    FROM Usage
                )
                SELECT dup.Id, keep.Id AS KeepId INTO #UnitMerge
                FROM Ranked dup
                JOIN Ranked keep ON keep.CompanyId = dup.CompanyId AND keep.Name = dup.Name AND keep.Position = 1
                WHERE dup.Position > 1;

                UPDATE p SET p.UnitId = m.KeepId
                FROM Products p
                JOIN #UnitMerge m ON p.UnitId = m.Id;

                DELETE u FROM Units u
                JOIN #UnitMerge m ON u.Id = m.Id;

                DROP TABLE #UnitMerge;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Units_CompanyId_Name",
                table: "Units",
                columns: new[] { "CompanyId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Units_CompanyId_Name",
                table: "Units");

            migrationBuilder.CreateIndex(
                name: "IX_Units_CompanyId",
                table: "Units",
                column: "CompanyId");
        }
    }
}
