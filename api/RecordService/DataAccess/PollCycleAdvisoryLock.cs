using Npgsql;

namespace RecordService.DataAccess;

/// <summary>
/// Cross-process lock for a poll cycle, backed by a Postgres session-level advisory lock. Holds
/// a dedicated connection for the lifetime of the lock (a cycle can take minutes, so it can't
/// borrow the request-scoped DbContext connection). Postgres releases the lock if the
/// connection drops or the process dies, so a crashed instance never leaves it stuck.
/// </summary>
public class PollCycleAdvisoryLock
{
    // Arbitrary constant key, shared by every API instance. ("PUBPOLL" as ASCII.)
    private const long LockKey = 0x505542504F4C4CL;

    private readonly string _connectionString;

    public PollCycleAdvisoryLock(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>Returns null if another session already holds the lock.</summary>
    public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        var connection = await OpenAsync(cancellationToken);
        try
        {
            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection);
            command.Parameters.AddWithValue("key", LockKey);
            if ((bool)(await command.ExecuteScalarAsync(cancellationToken))!)
            {
                return new Handle(connection);
            }
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        await connection.DisposeAsync();
        return null;
    }

    /// <summary>Waits until the lock is free.</summary>
    public async Task<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        var connection = await OpenAsync(cancellationToken);
        try
        {
            await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", connection);
            command.Parameters.AddWithValue("key", LockKey);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new Handle(connection);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class Handle : IAsyncDisposable
    {
        private readonly NpgsqlConnection _connection;

        public Handle(NpgsqlConnection connection) => _connection = connection;

        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", _connection);
                command.Parameters.AddWithValue("key", LockKey);
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                await _connection.DisposeAsync();
            }
        }
    }
}
