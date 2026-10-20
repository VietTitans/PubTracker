using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecordService.Migrations
{
    /// <inheritdoc />
    public partial class AddUserInactivityTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "inactivity_notice_sent_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_active_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "inactivity_notice_sent_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_active_at",
                table: "users");
        }
    }
}
