using Fms.Application.Common;
using Fms.Domain;
using Fms.Domain.Administration;
using Fms.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Fms.Application.Users;

public sealed record LoginRequest(string Username, string Password);
public sealed record UserDto(Guid Id, string Username, string FullName, UserRole Role, bool IsActive, DateTimeOffset? LastLoginAt);
public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt, UserDto User);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record CreateUserRequest(string Username, string FullName, UserRole Role, string Password);
public sealed record UpdateUserRequest(string FullName, UserRole Role, bool IsActive);
public sealed record ResetPasswordRequest(string NewPassword);

public static class PasswordPolicy
{
    public const int MinLength = 8;

    public static void Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
            throw new ValidationException($"Password must be at least {MinLength} characters.");
    }
}

public sealed class AuthService(
    IRepository<AppUser> users,
    IPasswordHasher hasher,
    ITokenService tokens,
    ICurrentUser current,
    IClock clock,
    IUnitOfWork uow)
{
    public static UserDto ToDto(AppUser u) => new(u.Id, u.Username, u.FullName, u.Role, u.IsActive, u.LastLoginAt);

    public Task<LoginResponse> LoginAsync(LoginRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var username = (r.Username ?? "").Trim().ToLowerInvariant();
        var user = await users.Query().FirstOrDefaultAsync(u => u.Username == username, t);
        // Same message for unknown user and wrong password to avoid user enumeration.
        if (user is null || !user.IsActive || !hasher.Verify(user.PasswordHash, r.Password ?? ""))
            throw new UnauthorizedException("Invalid username or password.");
        user.RecordLogin(clock.UtcNow);
        var (token, expires) = tokens.CreateToken(user);
        return new LoginResponse(token, expires, ToDto(user));
    }, ct);

    public async Task<UserDto> MeAsync(CancellationToken ct)
    {
        var id = current.Id ?? throw new UnauthorizedException("Not signed in.");
        var user = await users.GetByIdAsync(id, ct) ?? throw new UnauthorizedException("User no longer exists.");
        return ToDto(user);
    }

    public Task<Guid> ChangePasswordAsync(ChangePasswordRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var id = current.Id ?? throw new UnauthorizedException("Not signed in.");
        var user = await users.GetByIdAsync(id, t) ?? throw new UnauthorizedException("User no longer exists.");
        if (!hasher.Verify(user.PasswordHash, r.CurrentPassword ?? ""))
            throw new ValidationException("Current password is incorrect.");
        PasswordPolicy.Validate(r.NewPassword);
        user.SetPasswordHash(hasher.Hash(r.NewPassword));
        return id;
    }, ct);
}

public sealed class UserService(
    IRepository<AppUser> users,
    IPasswordHasher hasher,
    ICurrentUser current,
    IReadDb read,
    IUnitOfWork uow)
{
    public Task<List<UserDto>> ListAsync(CancellationToken ct) =>
        read.Query<AppUser>().OrderBy(u => u.Role).ThenBy(u => u.Username)
            .Select(u => new UserDto(u.Id, u.Username, u.FullName, u.Role, u.IsActive, u.LastLoginAt))
            .ToListAsync(ct);

    public Task<Guid> CreateAsync(CreateUserRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        PasswordPolicy.Validate(r.Password);
        var username = Guard.Required(r.Username, "Username", 50).ToLowerInvariant();
        if (await read.Query<AppUser>().AnyAsync(u => u.Username == username, t))
            throw new ConflictException($"Username '{username}' is taken.");
        var user = AppUser.Create(username, r.FullName, r.Role, hasher.Hash(r.Password));
        users.Add(user);
        return user.Id;
    }, ct);

    public Task<Guid> UpdateAsync(Guid id, UpdateUserRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        var user = await users.GetByIdAsync(id, t) ?? throw new NotFoundException("User", id);
        var losingOwner = user.Role == UserRole.Owner && (r.Role != UserRole.Owner || !r.IsActive);
        if (losingOwner && !await read.Query<AppUser>().AnyAsync(u => u.Id != id && u.Role == UserRole.Owner && u.IsActive, t))
            throw new ConflictException("There must always be at least one active Owner.");
        if (id == current.Id && !r.IsActive)
            throw new ConflictException("You cannot deactivate your own account.");
        user.Update(r.FullName, r.Role, r.IsActive);
        return id;
    }, ct);

    public Task<Guid> ResetPasswordAsync(Guid id, ResetPasswordRequest r, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        PasswordPolicy.Validate(r.NewPassword);
        var user = await users.GetByIdAsync(id, t) ?? throw new NotFoundException("User", id);
        user.SetPasswordHash(hasher.Hash(r.NewPassword));
        return id;
    }, ct);
}
