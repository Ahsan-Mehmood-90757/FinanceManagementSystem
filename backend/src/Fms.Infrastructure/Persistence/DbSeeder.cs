using Fms.Application.Common;
using Fms.Domain;
using Fms.Domain.Administration;
using Fms.Domain.MasterData;
using Fms.Domain.Parties;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fms.Infrastructure.Persistence;

/// <summary>
/// Applies migrations (when enabled) and seeds the minimum the system needs to run:
/// settings row, document sequences, the Owner account, default units / expense categories and the walk-in customer.
/// Idempotent - safe to run on every start.
/// </summary>
public static class DbSeeder
{
    public static async Task InitialiseAsync(IServiceProvider root, CancellationToken ct = default)
    {
        await using var scope = root.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var config = sp.GetRequiredService<IConfiguration>();
        var env = sp.GetRequiredService<IHostEnvironment>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var clock = sp.GetRequiredService<IClock>();

        if (config.GetValue("Database:MigrateOnStartup", env.IsDevelopment()))
        {
            logger.LogInformation("Applying database migrations...");
            await db.Database.MigrateAsync(ct);
        }

        if (!await db.SystemSettings.AnyAsync(ct))
            db.SystemSettings.Add(SystemSettings.CreateDefault(clock.Today));

        var existingKeys = await db.DocumentSequences.Select(s => s.Key).ToListAsync(ct);
        foreach (var (key, prefix) in SequenceKeys.All)
            if (!existingKeys.Contains(key))
                db.DocumentSequences.Add(new DocumentSequence(key, prefix));

        if (!await db.Users.AnyAsync(ct))
        {
            var username = config["Seed:Owner:Username"] ?? "owner";
            var password = config["Seed:Owner:Password"];
            if (string.IsNullOrWhiteSpace(password))
            {
                if (!env.IsDevelopment())
                    throw new InvalidOperationException(
                        "No users exist and Seed:Owner:Password is not set. Set the Seed__Owner__Password environment variable for the first start.");
                password = "Owner@12345";
            }
            db.Users.Add(AppUser.Create(username, config["Seed:Owner:FullName"] ?? "System Owner", UserRole.Owner, hasher.Hash(password)));
            logger.LogWarning("Created Owner account '{Username}'. Change its password after first sign-in.", username);
        }

        if (!await db.Units.AnyAsync(ct))
        {
            db.Units.AddRange(
                UnitOfMeasure.Create("Pieces", "pcs", false),
                UnitOfMeasure.Create("Box", "box", false),
                UnitOfMeasure.Create("Dozen", "dz", false),
                UnitOfMeasure.Create("Kilogram", "kg", true),
                UnitOfMeasure.Create("Gram", "g", true),
                UnitOfMeasure.Create("Litre", "L", true),
                UnitOfMeasure.Create("Millilitre", "mL", true),
                UnitOfMeasure.Create("Metre", "m", true));
        }

        if (!await db.ExpenseCategories.AnyAsync(ct))
        {
            foreach (var (name, desc) in new[]
                     {
                         ("Freight & Transport", "Inbound/outbound carriage attached to bills"),
                         ("Loading & Labour", "Loading, unloading and casual labour"),
                         ("Equipment Hire", "Rented equipment used for a delivery"),
                         ("Rent", "Premises rent"),
                         ("Salaries & Wages", "Staff salaries"),
                         ("Utilities", "Electricity, gas, water, internet"),
                         ("Bank Charges", "Bank fees and commissions"),
                         ("Miscellaneous", "Other operating expenses")
                     })
                db.ExpenseCategories.Add(ExpenseCategory.Create(name, desc));
        }

        if (!await db.Parties.AnyAsync(p => p.IsSystem, ct))
            db.Parties.Add(Party.Create("WALKIN", "Walk-in Customer", PartyKind.Customer, null, null, null, null,
                "Built-in cash customer. Sales to this party must be paid in full.", isSystem: true));

        await db.SaveChangesAsync(ct);
    }
}
