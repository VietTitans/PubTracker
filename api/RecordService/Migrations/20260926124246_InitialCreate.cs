using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RecordService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "records",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    external_id = table.Column<string>(type: "text", nullable: false),
                    doi = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    source_url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sources",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    base_url = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    keycloak_sub = table.Column<string>(type: "text", nullable: true),
                    is_marked_for_deletion = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deletion_requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "search_queries",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source_id = table.Column<int>(type: "integer", nullable: true),
                    target_url = table.Column<string>(type: "text", nullable: true),
                    last_digest_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_polled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    source_record_count = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_queries", x => x.id);
                    table.ForeignKey(
                        name: "FK_search_queries_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "sources",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "source_records",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    record_id = table.Column<int>(type: "integer", nullable: true),
                    source_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_source_records_records_record_id",
                        column: x => x.record_id,
                        principalTable: "records",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_source_records_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "sources",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "search_query_records",
                columns: table => new
                {
                    search_query_id = table.Column<int>(type: "integer", nullable: false),
                    record_id = table.Column<int>(type: "integer", nullable: false),
                    first_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_query_records", x => new { x.search_query_id, x.record_id });
                    table.ForeignKey(
                        name: "FK_search_query_records_records_record_id",
                        column: x => x.record_id,
                        principalTable: "records",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_search_query_records_search_queries_search_query_id",
                        column: x => x.search_query_id,
                        principalTable: "search_queries",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_search_queries",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    search_query_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_search_queries", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_search_queries_search_queries_search_query_id",
                        column: x => x.search_query_id,
                        principalTable: "search_queries",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_user_search_queries_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_search_query_digests",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    search_query_id = table.Column<int>(type: "integer", nullable: false),
                    last_digest_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_search_query_digests", x => new { x.user_id, x.search_query_id });
                    table.ForeignKey(
                        name: "FK_user_search_query_digests_search_queries_search_query_id",
                        column: x => x.search_query_id,
                        principalTable: "search_queries",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_user_search_query_digests_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_records_external_id",
                table: "records",
                column: "external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_search_queries_source_id_target_url",
                table: "search_queries",
                columns: new[] { "source_id", "target_url" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_search_query_records_record_id",
                table: "search_query_records",
                column: "record_id");

            migrationBuilder.CreateIndex(
                name: "IX_source_records_record_id_source_id",
                table: "source_records",
                columns: new[] { "record_id", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_source_records_source_id",
                table: "source_records",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "IX_sources_name",
                table: "sources",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_search_queries_search_query_id",
                table: "user_search_queries",
                column: "search_query_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_search_queries_user_id_search_query_id",
                table: "user_search_queries",
                columns: new[] { "user_id", "search_query_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_search_query_digests_search_query_id",
                table: "user_search_query_digests",
                column: "search_query_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_keycloak_sub",
                table: "users",
                column: "keycloak_sub",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_query_records");

            migrationBuilder.DropTable(
                name: "source_records");

            migrationBuilder.DropTable(
                name: "user_search_queries");

            migrationBuilder.DropTable(
                name: "user_search_query_digests");

            migrationBuilder.DropTable(
                name: "records");

            migrationBuilder.DropTable(
                name: "search_queries");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "sources");
        }
    }
}
