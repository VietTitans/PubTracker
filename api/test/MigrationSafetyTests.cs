using System.Text.RegularExpressions;

namespace test;

/// <summary>
/// Migrations run when the new api image starts, and a failed deploy rolls back by restarting the
/// previous image against the already-migrated database (docs/runbooks/rollback.md). So every new
/// migration must leave the previous image working: no drops, renames or type changes, no new
/// required column without a default, no new unique index on an existing table.
///
/// A migration that has to do one of these on purpose (the second release of an expand/contract
/// change) opts out with a comment containing "migration-safety: allow" and the reason.
///
/// This reads the generated Up() code, so it catches the common cases, not every incompatibility:
/// raw SQL (migrationBuilder.Sql) is not inspected.
/// </summary>
public class MigrationSafetyTests
{
    // Migrations up to and including this id shipped before the rule existed.
    private const string LastGrandfatheredMigration = "20261015155655";

    private const string AllowMarker = "migration-safety: allow";

    public static IReadOnlyList<string> FindUnsafeOperations(string migrationSource)
    {
        var up = ExtractUp(migrationSource);
        var problems = new List<string>();

        foreach (var banned in new[] { "DropColumn", "DropTable", "RenameColumn", "RenameTable", "AlterColumn" })
        {
            if (Regex.IsMatch(up, $@"\.{banned}[<(]"))
            {
                problems.Add($"{banned}: the previous image still uses the old shape");
            }
        }

        foreach (Match add in Regex.Matches(up, @"\.AddColumn<[^>]+>\((.*?)\);", RegexOptions.Singleline))
        {
            var args = add.Groups[1].Value;
            if (args.Contains("nullable: false") && !args.Contains("defaultValue"))
            {
                problems.Add($"AddColumn without nullable or a default: the previous image's inserts would fail ({Regex.Match(args, "name: \"([^\"]+)\"").Groups[1].Value})");
            }
        }

        var createdTables = Regex.Matches(up, @"\.CreateTable\(\s*name: ""([^""]+)""")
            .Select(m => m.Groups[1].Value).ToHashSet();
        foreach (Match index in Regex.Matches(up, @"\.CreateIndex\((.*?)\);", RegexOptions.Singleline))
        {
            var args = index.Groups[1].Value;
            var table = Regex.Match(args, "table: \"([^\"]+)\"").Groups[1].Value;
            if (args.Contains("unique: true") && !createdTables.Contains(table))
            {
                problems.Add($"unique index on existing table '{table}': the previous image could violate it");
            }
        }

        return problems;
    }

    private static string ExtractUp(string source)
    {
        var start = source.IndexOf("void Up(", StringComparison.Ordinal);
        var end = source.IndexOf("void Down(", StringComparison.Ordinal);
        return start >= 0 && end > start ? source[start..end] : source;
    }

    [Fact]
    public void AddingANullableOrDefaultedColumnOrANewTable_IsSafe()
    {
        const string source = """
            protected override void Up(MigrationBuilder migrationBuilder)
            {
                migrationBuilder.AddColumn<bool>(name: "a", table: "users", type: "boolean", nullable: false, defaultValue: false);
                migrationBuilder.AddColumn<string>(name: "b", table: "users", type: "text", nullable: true);
                migrationBuilder.CreateTable(
                    name: "things",
                    columns: table => new { id = table.Column<int>(type: "integer", nullable: false) });
                migrationBuilder.CreateIndex(name: "ix_things_id", table: "things", column: "id", unique: true);
                migrationBuilder.CreateIndex(name: "ix_users_a", table: "users", column: "a");
            }
            protected override void Down(MigrationBuilder migrationBuilder) { migrationBuilder.DropTable(name: "things"); }
            """;

        Assert.Empty(FindUnsafeOperations(source));
    }

    [Theory]
    [InlineData("migrationBuilder.DropColumn(name: \"a\", table: \"users\");", "DropColumn")]
    [InlineData("migrationBuilder.DropTable(name: \"users\");", "DropTable")]
    [InlineData("migrationBuilder.RenameColumn(name: \"a\", table: \"users\", newName: \"b\");", "RenameColumn")]
    [InlineData("migrationBuilder.RenameTable(name: \"users\", newName: \"people\");", "RenameTable")]
    [InlineData("migrationBuilder.AlterColumn<string>(name: \"a\", table: \"users\", type: \"character varying(10)\", nullable: false);", "AlterColumn")]
    [InlineData("migrationBuilder.AddColumn<string>(name: \"a\", table: \"users\", type: \"text\", nullable: false);", "AddColumn without")]
    [InlineData("migrationBuilder.CreateIndex(name: \"ix\", table: \"users\", column: \"email\", unique: true);", "unique index on existing table 'users'")]
    public void UnsafeOperation_IsReported(string statement, string expected)
    {
        var source = $"protected override void Up(MigrationBuilder migrationBuilder)\n{{\n{statement}\n}}\nprotected override void Down(MigrationBuilder migrationBuilder) {{ }}";

        Assert.Contains(FindUnsafeOperations(source), p => p.Contains(expected));
    }

    [Fact]
    public void OperationsOnlyInDown_AreIgnored()
    {
        const string source = """
            protected override void Up(MigrationBuilder migrationBuilder) { }
            protected override void Down(MigrationBuilder migrationBuilder) { migrationBuilder.DropColumn(name: "a", table: "users"); }
            """;

        Assert.Empty(FindUnsafeOperations(source));
    }

    [Fact]
    public void EveryMigrationAfterTheRule_LeavesThePreviousImageWorking()
    {
        var failures = new List<string>();
        foreach (var file in Directory.GetFiles(FindMigrationsDirectory(), "*.cs"))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".Designer.cs") || name.EndsWith("ModelSnapshot.cs"))
            {
                continue;
            }
            if (string.CompareOrdinal(name[..14], LastGrandfatheredMigration) <= 0)
            {
                continue;
            }

            var source = File.ReadAllText(file);
            if (source.Contains(AllowMarker, StringComparison.Ordinal))
            {
                continue;
            }
            failures.AddRange(FindUnsafeOperations(source).Select(p => $"{name}: {p}"));
        }

        Assert.True(failures.Count == 0,
            "Migrations must stay compatible with the previous image (see CLAUDE.md). Split the change over two releases, " +
            $"or add a '// {AllowMarker}: <reason>' comment.\n" + string.Join("\n", failures));
    }

    [Fact]
    public void TheMigrationsDirectory_IsFound_AndHasMigrationsAfterTheCutoff()
    {
        // Guards the scan above against passing because it looked at nothing.
        var names = Directory.GetFiles(FindMigrationsDirectory(), "*.cs").Select(Path.GetFileName).ToList();

        Assert.Contains(names, n => n!.EndsWith("_InitialCreate.cs"));
        Assert.Contains(names, n => !n!.EndsWith(".Designer.cs") && string.CompareOrdinal(n![..14], LastGrandfatheredMigration) > 0);
    }

    private static string FindMigrationsDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "RecordService", "Migrations");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }
        throw new DirectoryNotFoundException("api/RecordService/Migrations not found above " + AppContext.BaseDirectory);
    }
}
