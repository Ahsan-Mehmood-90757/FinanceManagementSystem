using System.Security.Claims;
using System.Text;
using Fms.Application.Common;
using Fms.Domain;
using Fms.Domain.Administration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Fms.Api.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "fms-api";
    public string Audience { get; set; } = "fms-web";
    /// <summary>HMAC key, at least 32 characters. Supply via user-secrets / env var Jwt__SigningKey in production.</summary>
    public string SigningKey { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 720;
}

/// <summary>Named authorization policies. Owner passes every policy.</summary>
public static class Policies
{
    public const string Post = "post";          // create bills, returns, vouchers, write-offs
    public const string MasterData = "master";  // parties, products, accounts, categories
    public const string Void = "void";          // reverse posted documents
    public const string Setup = "setup";        // Day-0 opening balances, company settings
    public const string Users = "users";        // manage users

    public static readonly IReadOnlyDictionary<string, UserRole[]> Map = new Dictionary<string, UserRole[]>
    {
        [Post] = [UserRole.Owner, UserRole.Admin, UserRole.Accountant],
        [MasterData] = [UserRole.Owner, UserRole.Admin, UserRole.Accountant],
        [Void] = [UserRole.Owner, UserRole.Admin],
        [Setup] = [UserRole.Owner, UserRole.Admin],
        [Users] = [UserRole.Owner]
    };
}

public static class ClaimNames
{
    public const string Role = "role";
    public const string Name = "name";
    public const string FullName = "full_name";
}

public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : ITokenService
{
    public (string Token, DateTimeOffset ExpiresAt) CreateToken(AppUser user)
    {
        var o = options.Value;
        var expires = clock.UtcNow.AddMinutes(o.ExpiryMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            Expires = expires.UtcDateTime,
            NotBefore = clock.UtcNow.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey)), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimNames.Name, user.Username),
                new Claim(ClaimNames.FullName, user.FullName),
                new Claim(ClaimNames.Role, user.Role.ToString())
            })
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? Id =>
        Guid.TryParse(Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;

    public string? UserName => Principal?.FindFirst(ClaimNames.Name)?.Value;
}

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddFmsAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        var jwt = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        if (jwt.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters. Set it in appsettings or the Jwt__SigningKey environment variable.");

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = ClaimNames.Name,
                    RoleClaimType = ClaimNames.Role
                };
            });

        services.AddAuthorization(options =>
        {
            // Every endpoint requires a signed-in user unless marked [AllowAnonymous].
            options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().Build();
            foreach (var (name, roles) in Policies.Map)
                options.AddPolicy(name, p => p.RequireRole(roles.Select(r => r.ToString())));
        });
        return services;
    }
}
