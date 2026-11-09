using Fms.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fms.Infrastructure.Persistence;

/// <summary>Used only by `dotnet ef` (migrations). Connection string comes from FMS_DESIGN_CONNECTION or a local default.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("FMS_DESIGN_CONNECTION")
                 ?? "Host=localhost;Port=5432;Database=fms_dev;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(cs, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;
        return new AppDbContext(options, new DesignTimeUser(), new DesignTimeClock());
    }

    private sealed class DesignTimeUser : ICurrentUser
    {
        public Guid? Id => null;
        public string? UserName => "design-time";
    }

    private sealed class DesignTimeClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    }
}
