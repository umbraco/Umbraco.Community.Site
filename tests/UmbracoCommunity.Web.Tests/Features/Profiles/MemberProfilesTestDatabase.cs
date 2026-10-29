using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UmbracoCommunity.Web.Features.Profiles.Data;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

/// <summary>
/// An in-memory SQLite database for <see cref="MemberProfilesDbContext"/>, built by running the
/// real migrations. The connection stays open for the lifetime of the instance so the
/// database survives between contexts.
/// </summary>
public sealed class MemberProfilesTestDatabase : IDbContextFactory<MemberProfilesDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MemberProfilesDbContext> _options;

    public MemberProfilesTestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MemberProfilesDbContext>()
            .UseSqlite(_connection, sqlite => sqlite.MigrationsAssembly("UmbracoCommunity.Web"))
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
    }

    public SqliteConnection Connection => _connection;

    public MemberProfilesDbContext CreateDbContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
