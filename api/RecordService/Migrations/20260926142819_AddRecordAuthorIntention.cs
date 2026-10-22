using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecordService.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordAuthorIntention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "author_intention",
                table: "records",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "author_intention",
                table: "records");
        }
    }
}
