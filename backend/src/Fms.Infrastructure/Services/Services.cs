using Fms.Application.Common;
using Fms.Domain.Administration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Fms.Infrastructure.Services;

public sealed class AppOptions
{
    public const string Section = "App";
    /// <summary>IANA or Windows time zone id used to decide "today" for documents.</summary>
    public string TimeZone { get; set; } = "Asia/Karachi";
}

public sealed class SystemClock : IClock
{
    private readonly TimeZoneInfo _zone;

    public SystemClock(IOptions<AppOptions> options)
    {
        _zone = Resolve(options.Value.TimeZone);
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _zone).DateTime);

    private static TimeZoneInfo Resolve(string id)
    {
        foreach (var candidate in new[] { id, "Asia/Karachi", "Pakistan Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(candidate); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }
}

/// <summary>PBKDF2 (ASP.NET Core Identity v3 format) password hashing.</summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<AppUser> _inner = new();

    public string Hash(string password) => _inner.HashPassword(null!, password);

    public bool Verify(string hash, string password)
    {
        if (string.IsNullOrEmpty(hash)) return false;
        try
        {
            return _inner.VerifyHashedPassword(null!, hash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
