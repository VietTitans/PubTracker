using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecordService.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Expand step: the model no longer has users.name, but the previous image still writes it.
            // The column is dropped in the next release (see docs/runbooks/rollback.md).
            migrationBuilder.Sql("ALTER TABLE users ALTER COLUMN name DROP NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE users SET name = username WHERE name IS NULL; ALTER TABLE users ALTER COLUMN name SET NOT NULL;");
        }
    }
}
