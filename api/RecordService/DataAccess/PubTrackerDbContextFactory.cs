using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RecordService.DataAccess;

/// <summary>
/// Lets `dotnet ef` tools (migrations add/remove, database update, ...) construct a DbContext
/// without booting Program.cs (which throws if DB/email config env vars aren't set). `dotnet ef`
/// always prefers a design-time factory over the app host, so commands like `database update`
/// still need a real, reachable connection string - read from the same
/// ConnectionStrings__DefaultConnection env var Program.cs and docker-compose use, falling back
/// to a local default for `migrations add`, which never actually connects.
/// </summary>
public class PubTrackerDbContextFactory : IDesignTimeDbContextFactory<PubTrackerDbContext>
{
    public PubTrackerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=pubtracker;Username=postgres;Password=postgres";
        var optionsBuilder = new DbContextOptionsBuilder<PubTrackerDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new PubTrackerDbContext(optionsBuilder.Options);
    }
}
