using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIsApproved : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every account that exists before this migration keeps its access: only new sign-ups start unapproved.
            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Users");
        }
    }
}
