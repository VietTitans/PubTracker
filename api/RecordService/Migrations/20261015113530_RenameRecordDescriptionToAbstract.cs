using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecordService.Migrations
{
    /// <inheritdoc />
    public partial class RenameRecordDescriptionToAbstract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "description",
                table: "records",
                newName: "abstract");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "abstract",
                table: "records",
                newName: "description");
        }
    }
}
