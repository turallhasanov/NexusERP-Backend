using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PromoteCompanyFounderToITAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Company founders are now IT admins. A company that has no manager at all (no admin, no itadmin)
            // gets one: its earliest user becomes the IT admin. Companies that already have a manager are left alone.
            migrationBuilder.Sql("""
                UPDATE u
                SET u.Role = 'itadmin'
                FROM Users u
                WHERE u.Id IN (
                    SELECT first.Id
                    FROM Companies c
                    CROSS APPLY (
                        SELECT TOP 1 x.Id
                        FROM Users x
                        WHERE x.CompanyId = c.Id
                        ORDER BY x.CreatedAt, x.Id
                    ) AS first
                    WHERE NOT EXISTS (
                        SELECT 1 FROM Users m
                        WHERE m.CompanyId = c.Id AND m.Role IN ('admin', 'itadmin')
                    )
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data only: the users' previous roles are not recorded, so there is nothing to restore.
        }
    }
}
