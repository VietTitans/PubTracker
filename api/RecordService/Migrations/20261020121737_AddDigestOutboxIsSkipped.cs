using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecordService.Migrations
{
    /// <inheritdoc />
    public partial class AddDigestOutboxIsSkipped : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_skipped",
                table: "digest_outbox",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_skipped",
                table: "digest_outbox");
        }
    }
}
