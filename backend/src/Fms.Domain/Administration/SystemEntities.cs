using Fms.Domain.Common;

namespace Fms.Domain.Administration;

/// <summary>Single-row company settings and Day-0 setup state.</summary>
public sealed class SystemSettings : AuditableEntity
{
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private SystemSettings() { }

    public string CompanyName { get; private set; } = "My Company";
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? TaxNumber { get; private set; }
    public string? InvoiceFooter { get; private set; }
    /// <summary>Opening balances are dated the day before go-live; transactions must be on or after it.</summary>
    public DateOnly GoLiveDate { get; private set; }
    public DateTimeOffset? SetupCompletedAt { get; private set; }
    public string? SetupCompletedBy { get; private set; }

    public bool IsOperational => SetupCompletedAt is not null;
    public DateOnly OpeningDate => GoLiveDate.AddDays(-1);

    public static SystemSettings CreateDefault(DateOnly goLive) => new() { Id = SingletonId, GoLiveDate = goLive };

    public void UpdateCompany(string companyName, string? address, string? phone, string? email, string? taxNumber, string? invoiceFooter)
    {
        CompanyName = Guard.Required(companyName, "Company name", 200);
        Address = Guard.Optional(address, "Address", 500);
        Phone = Guard.Optional(phone, "Phone", 50);
        Email = Guard.Optional(email, "Email", 200);
        TaxNumber = Guard.Optional(taxNumber, "Tax number", 50);
        InvoiceFooter = Guard.Optional(invoiceFooter, "Invoice footer", 500);
    }

    public void SetGoLiveDate(DateOnly date)
    {
        EnsureInSetup();
        GoLiveDate = date;
    }

    public void CompleteSetup(string? by, DateTimeOffset at)
    {
        EnsureInSetup();
        SetupCompletedAt = at;
        SetupCompletedBy = by;
    }

    public void EnsureInSetup()
    {
        if (IsOperational)
            throw new DomainException("Day-0 setup is already completed; opening balances are locked.", "setup_locked");
    }

    public void EnsureOperational(DateOnly transactionDate)
    {
        if (!IsOperational)
            throw new DomainException("Complete the Day-0 setup before posting transactions.", "setup_incomplete");
        if (transactionDate < GoLiveDate)
            throw new DomainException($"Transactions cannot be dated before the go-live date ({GoLiveDate:yyyy-MM-dd}).", "before_go_live");
    }
}

public sealed class AppUser : AuditableEntity
{
    private AppUser() { }

    public string Username { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? LastLoginAt { get; private set; }

    public static AppUser Create(string username, string fullName, UserRole role, string passwordHash)
    {
        var u = new AppUser { Username = Guard.Required(username, "Username", 50).ToLowerInvariant(), PasswordHash = passwordHash };
        u.Update(fullName, role, true);
        return u;
    }

    public void Update(string fullName, UserRole role, bool isActive)
    {
        if (!Enum.IsDefined(role)) throw new DomainException("Invalid role.");
        FullName = Guard.Required(fullName, "Full name", 100);
        Role = role;
        IsActive = isActive;
    }

    public void SetPasswordHash(string hash) => PasswordHash = hash;
    public void RecordLogin(DateTimeOffset at) => LastLoginAt = at;
}

/// <summary>Gap-free per-type document counters (PB-000001, SB-000001...).</summary>
public sealed class DocumentSequence
{
    private DocumentSequence() { }

    public DocumentSequence(string key, string prefix)
    {
        Key = key;
        Prefix = prefix;
        NextValue = 1;
    }

    public string Key { get; private set; } = default!;
    public string Prefix { get; private set; } = default!;
    public long NextValue { get; private set; }
}
