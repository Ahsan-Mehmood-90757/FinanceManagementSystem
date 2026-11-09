using Fms.Application.Accounts;
using Fms.Application.Common;
using Fms.Application.Inventory;
using Fms.Application.MasterData;
using Fms.Application.Parties;
using Fms.Application.Products;
using Fms.Application.Purchases;
using Fms.Application.Reports;
using Fms.Application.Sales;
using Fms.Application.Setup;
using Fms.Application.Users;
using Fms.Application.Vouchers;
using Microsoft.Extensions.DependencyInjection;

namespace Fms.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<LedgerWriter>();
        services.AddScoped<SystemState>();
        services.AddScoped<VoucherPoster>();

        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<ProductService>();
        services.AddScoped<PartyService>();
        services.AddScoped<AccountService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<PurchaseService>();
        services.AddScoped<SaleService>();
        services.AddScoped<VoucherService>();
        services.AddScoped<SetupService>();
        services.AddScoped<ReportService>();
        return services;
    }
}
