using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyInviteCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InviteCode",
                table: "Companies",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            // Existing companies get their own random code (same alphabet as InviteCode.Generate) before the
            // unique index is created; otherwise every row would share "".
            migrationBuilder.Sql("""
                DECLARE @alphabet char(31) = 'ABCDEFGHJKMNPQRSTUVWXYZ23456789';
                DECLARE @id nvarchar(450), @code nvarchar(8), @i int;
                DECLARE companies CURSOR LOCAL FAST_FORWARD FOR SELECT Id FROM Companies WHERE InviteCode = '';
                OPEN companies;
                FETCH NEXT FROM companies INTO @id;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    WHILE 1 = 1
                    BEGIN
                        SET @code = '';
                        SET @i = 0;
                        WHILE @i < 8
                        BEGIN
                            SET @code = @code + SUBSTRING(@alphabet, 1 + CAST(CRYPT_GEN_RANDOM(1) AS int) % 31, 1);
                            SET @i += 1;
                        END
                        IF NOT EXISTS (SELECT 1 FROM Companies WHERE InviteCode = @code) BREAK;
                    END
                    UPDATE Companies SET InviteCode = @code WHERE Id = @id;
                    FETCH NEXT FROM companies INTO @id;
                END
                CLOSE companies;
                DEALLOCATE companies;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_InviteCode",
                table: "Companies",
                column: "InviteCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Companies_InviteCode",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "InviteCode",
                table: "Companies");
        }
    }
}
