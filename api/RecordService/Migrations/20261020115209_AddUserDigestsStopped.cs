using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecordService.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDigestsStopped : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "digests_stopped",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "digests_stopped",
                table: "users");
        }
    }
}
