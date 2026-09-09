using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UmbracoCommunity.MeetBooking.Storage;

/// <summary>
/// Design-time factory for the EF Core CLI (<c>dotnet ef migrations add</c>). A standalone SQLite configuration is
/// enough for scaffolding; the runtime configuration in the composer reads the real Umbraco connection string.
/// </summary>
public class MeetBookingDbContextFactory : IDesignTimeDbContextFactory<MeetBookingDbContext>
{
    public MeetBookingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MeetBookingDbContext>();
        optionsBuilder.UseSqlite("Data Source=meet-booking-design-time.db", sqlite =>
            sqlite.MigrationsAssembly("UmbracoCommunity.MeetBooking"));
        return new MeetBookingDbContext(optionsBuilder.Options);
    }
}
