using Fms.Api.Auth;
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fms.Api.Controllers;

public sealed record IdResponse(Guid Id);

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class ApiController : ControllerBase
{
    protected static IdResponse Id(Guid id) => new(id);
}

// ===================================================================== Auth & users
[Route("api/auth")]
public sealed class AuthController(AuthService auth) : ApiController
{
    [AllowAnonymous, EnableRateLimiting("login"), HttpPost("login")]
    public Task<LoginResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    [HttpGet("me")]
    public Task<UserDto> Me(CancellationToken ct) => auth.MeAsync(ct);

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(request, ct);
        return NoContent();
    }
}

[Authorize(Policies.Users)]
public sealed class UsersController(UserService users) : ApiController
{
    [HttpGet] public Task<List<UserDto>> List(CancellationToken ct) => users.ListAsync(ct);
    [HttpPost] public async Task<IdResponse> Create(CreateUserRequest r, CancellationToken ct) => Id(await users.CreateAsync(r, ct));
    [HttpPut("{id:guid}")] public async Task<IdResponse> Update(Guid id, UpdateUserRequest r, CancellationToken ct) => Id(await users.UpdateAsync(id, r, ct));
    [HttpPost("{id:guid}/reset-password")] public async Task<IdResponse> Reset(Guid id, ResetPasswordRequest r, CancellationToken ct) => Id(await users.ResetPasswordAsync(id, r, ct));
}

// ===================================================================== Setup (Day-0)
public sealed class SetupController(SetupService setup) : ApiController
{
    [HttpGet("company")] public Task<CompanyDto> Company(CancellationToken ct) => setup.GetCompanyAsync(ct);
    [HttpGet("status")] public Task<SetupStatusDto> Status(CancellationToken ct) => setup.GetStatusAsync(ct);

    [Authorize(Policies.Setup), HttpPut("company")]
    public Task<CompanyDto> UpdateCompany(CompanyRequest r, CancellationToken ct) => setup.UpdateCompanyAsync(r, ct);

    [Authorize(Policies.Setup), HttpPut("parties/{id:guid}/opening-balance")]
    public async Task<IdResponse> PartyOpening(Guid id, OpeningBalanceRequest r, CancellationToken ct) => Id(await setup.SetPartyOpeningAsync(id, r, ct));

    [Authorize(Policies.Setup), HttpPut("accounts/{id:guid}/opening-balance")]
    public async Task<IdResponse> AccountOpening(Guid id, OpeningBalanceRequest r, CancellationToken ct) => Id(await setup.SetAccountOpeningAsync(id, r, ct));

    [Authorize(Policies.Setup), HttpPost("opening-stock")]
    public async Task<IdResponse> AddStock(OpeningStockRequest r, CancellationToken ct) => Id(await setup.AddOpeningStockAsync(r, ct));

    [Authorize(Policies.Setup), HttpDelete("opening-stock/{batchId:guid}")]
    public async Task<IdResponse> RemoveStock(Guid batchId, CancellationToken ct) => Id(await setup.RemoveOpeningStockAsync(batchId, ct));

    [Authorize(Policies.Setup), HttpPost("complete")]
    public Task<CompanyDto> Complete(CancellationToken ct) => setup.CompleteAsync(ct);
}

// ===================================================================== Master data
public sealed class UnitsController(CatalogService catalog) : ApiController
{
    [HttpGet] public Task<List<UnitDto>> List(CancellationToken ct) => catalog.ListUnitsAsync(ct);
    [Authorize(Policies.MasterData), HttpPost] public async Task<IdResponse> Create(UnitRequest r, CancellationToken ct) => Id(await catalog.CreateUnitAsync(r, ct));
    [Authorize(Policies.MasterData), HttpPut("{id:guid}")] public async Task<IdResponse> Update(Guid id, UnitRequest r, CancellationToken ct) => Id(await catalog.UpdateUnitAsync(id, r, ct));
    [Authorize(Policies.MasterData), HttpDelete("{id:guid}")] public async Task<IdResponse> Delete(Guid id, CancellationToken ct) => Id(await catalog.DeleteUnitAsync(id, ct));
}

public sealed class CategoriesController(CatalogService catalog) : ApiController
{
    [HttpGet] public Task<List<CategoryDto>> List(CancellationToken ct) => catalog.ListCategoriesAsync(ct);
    [Authorize(Policies.MasterData), HttpPost] public async Task<IdResponse> Create(CategoryRequest r, CancellationToken ct) => Id(await catalog.CreateCategoryAsync(r, ct));
    [Authorize(Policies.MasterData), HttpPut("{id:guid}")] public async Task<IdResponse> Update(Guid id, CategoryRequest r, CancellationToken ct) => Id(await catalog.UpdateCategoryAsync(id, r, ct));
    [Authorize(Policies.MasterData), HttpDelete("{id:guid}")] public async Task<IdResponse> Delete(Guid id, CancellationToken ct) => Id(await catalog.DeleteCategoryAsync(id, ct));
}

[Route("api/expense-categories")]
public sealed class ExpenseCategoriesController(CatalogService catalog) : ApiController
{
    [HttpGet] public Task<List<ExpenseCategoryDto>> List([FromQuery] bool activeOnly, CancellationToken ct) => catalog.ListExpenseCategoriesAsync(activeOnly, ct);
    [Authorize(Policies.MasterData), HttpPost] public async Task<IdResponse> Create(ExpenseCategoryRequest r, CancellationToken ct) => Id(await catalog.CreateExpenseCategoryAsync(r, ct));
    [Authorize(Policies.MasterData), HttpPut("{id:guid}")] public async Task<IdResponse> Update(Guid id, ExpenseCategoryRequest r, CancellationToken ct) => Id(await catalog.UpdateExpenseCategoryAsync(id, r, ct));
    [Authorize(Policies.MasterData), HttpDelete("{id:guid}")] public async Task<IdResponse> Delete(Guid id, CancellationToken ct) => Id(await catalog.DeleteExpenseCategoryAsync(id, ct));
}

public sealed class ProductsController(ProductService products, InventoryService inventory) : ApiController
{
    [HttpGet] public Task<PagedResult<ProductDto>> List([FromQuery] ProductQuery q, CancellationToken ct) => products.ListAsync(q, ct);
    [HttpGet("{id:guid}")] public Task<ProductDto> Get(Guid id, CancellationToken ct) => products.GetAsync(id, ct);
    [Authorize(Policies.MasterData), HttpPost] public async Task<IdResponse> Create(ProductRequest r, CancellationToken ct) => Id(await products.CreateAsync(r, ct));
    [Authorize(Policies.MasterData), HttpPut("{id:guid}")] public async Task<IdResponse> Update(Guid id, ProductRequest r, CancellationToken ct) => Id(await products.UpdateAsync(id, r, ct));

    [HttpGet("{id:guid}/movements")]
    public Task<PagedResult<MovementDto>> Movements(Guid id, [FromQuery] PageQuery q, CancellationToken ct) => inventory.ListMovementsAsync(id, q, ct);

    /// <summary>Which batches a sale of the given quantity would consume (FIFO), without posting anything.</summary>
    [HttpGet("{id:guid}/fifo-preview")]
    public Task<FifoPreviewDto> FifoPreview(Guid id, [FromQuery] decimal quantity, [FromQuery] DateOnly? date, CancellationToken ct) =>
        inventory.PreviewFifoAsync(id, quantity, date, ct);
}

public sealed class PartiesController(PartyService parties) : ApiController
{
    [HttpGet] public Task<PagedResult<PartyDto>> List([FromQuery] PartyQuery q, CancellationToken ct) => parties.ListAsync(q, ct);
    [HttpGet("{id:guid}")] public Task<PartyDto> Get(Guid id, CancellationToken ct) => parties.GetAsync(id, ct);
    [Authorize(Policies.MasterData), HttpPost] public async Task<IdResponse> Create(PartyRequest r, CancellationToken ct) => Id(await parties.CreateAsync(r, ct));
    [Authorize(Policies.MasterData), HttpPut("{id:guid}")] public async Task<IdResponse> Update(Guid id, PartyRequest r, CancellationToken ct) => Id(await parties.UpdateAsync(id, r, ct));

    [HttpGet("{id:guid}/statement")]
    public Task<StatementDto> Statement(Guid id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        parties.StatementAsync(id, from, to, ct);
}

public sealed class AccountsController(AccountService accounts) : ApiController
{
    [HttpGet] public Task<List<AccountDto>> List([FromQuery] bool activeOnly, CancellationToken ct) => accounts.ListAsync(activeOnly, ct);
    [HttpGet("{id:guid}")] public Task<AccountDto> Get(Guid id, CancellationToken ct) => accounts.GetAsync(id, ct);
    [Authorize(Policies.MasterData), HttpPost] public async Task<IdResponse> Create(AccountRequest r, CancellationToken ct) => Id(await accounts.CreateAsync(r, ct));
    [Authorize(Policies.MasterData), HttpPut("{id:guid}")] public async Task<IdResponse> Update(Guid id, AccountRequest r, CancellationToken ct) => Id(await accounts.UpdateAsync(id, r, ct));

    [HttpGet("{id:guid}/statement")]
    public Task<StatementDto> Statement(Guid id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        accounts.StatementAsync(id, from, to, ct);
}

// ===================================================================== Inventory
[Route("api/inventory")]
public sealed class InventoryController(InventoryService inventory) : ApiController
{
    [HttpGet("batches")]
    public Task<PagedResult<BatchDto>> Batches([FromQuery] BatchQuery q, CancellationToken ct) => inventory.ListBatchesAsync(q, ct);

    [HttpGet("adjustments")]
    public Task<PagedResult<StockAdjustmentDto>> Adjustments([FromQuery] DateRangeQuery q, CancellationToken ct) => inventory.ListAdjustmentsAsync(q, ct);

    [Authorize(Policies.Post), HttpPost("adjustments")]
    public async Task<IdResponse> WriteOff(StockAdjustmentRequest r, CancellationToken ct) => Id(await inventory.WriteOffAsync(r, ct));
}

// ===================================================================== Documents
public sealed class PurchasesController(PurchaseService purchases) : ApiController
{
    [HttpGet] public Task<PagedResult<DocumentListItem>> List([FromQuery] DateRangeQuery q, CancellationToken ct) => purchases.ListAsync(q, ct);
    [HttpGet("{id:guid}")] public Task<PurchaseBillDto> Get(Guid id, CancellationToken ct) => purchases.GetAsync(id, ct);
    [Authorize(Policies.Post), HttpPost] public async Task<IdResponse> Post(PurchaseBillRequest r, CancellationToken ct) => Id(await purchases.PostAsync(r, ct));
    [Authorize(Policies.Void), HttpPost("{id:guid}/void")] public async Task<IdResponse> Void(Guid id, VoidRequest r, CancellationToken ct) => Id(await purchases.VoidAsync(id, r, ct));
    [Authorize(Policies.Post), HttpPost("{id:guid}/returns")] public async Task<IdResponse> Return(Guid id, ReturnRequest r, CancellationToken ct) => Id(await purchases.ReturnAsync(id, r, ct));
}

[Route("api/purchase-returns")]
public sealed class PurchaseReturnsController(PurchaseService purchases) : ApiController
{
    [HttpGet] public Task<PagedResult<ReturnListItem>> List([FromQuery] DateRangeQuery q, CancellationToken ct) => purchases.ListReturnsAsync(q, ct);
    [HttpGet("{id:guid}")] public Task<PurchaseReturnDto> Get(Guid id, CancellationToken ct) => purchases.GetReturnAsync(id, ct);
}

public sealed class SalesController(SaleService sales) : ApiController
{
    [HttpGet] public Task<PagedResult<DocumentListItem>> List([FromQuery] DateRangeQuery q, CancellationToken ct) => sales.ListAsync(q, ct);
    [HttpGet("{id:guid}")] public Task<SaleBillDto> Get(Guid id, CancellationToken ct) => sales.GetAsync(id, ct);
    [Authorize(Policies.Post), HttpPost] public async Task<IdResponse> Post(SaleBillRequest r, CancellationToken ct) => Id(await sales.PostAsync(r, ct));
    [Authorize(Policies.Void), HttpPost("{id:guid}/void")] public async Task<IdResponse> Void(Guid id, VoidRequest r, CancellationToken ct) => Id(await sales.VoidAsync(id, r, ct));
    [Authorize(Policies.Post), HttpPost("{id:guid}/returns")] public async Task<IdResponse> Return(Guid id, ReturnRequest r, CancellationToken ct) => Id(await sales.ReturnAsync(id, r, ct));
}

[Route("api/sale-returns")]
public sealed class SaleReturnsController(SaleService sales) : ApiController
{
    [HttpGet] public Task<PagedResult<ReturnListItem>> List([FromQuery] DateRangeQuery q, CancellationToken ct) => sales.ListReturnsAsync(q, ct);
    [HttpGet("{id:guid}")] public Task<SaleReturnDto> Get(Guid id, CancellationToken ct) => sales.GetReturnAsync(id, ct);
}

public sealed class VouchersController(VoucherService vouchers) : ApiController
{
    [HttpGet] public Task<PagedResult<VoucherDto>> List([FromQuery] VoucherQuery q, CancellationToken ct) => vouchers.ListAsync(q, ct);
    [HttpGet("{id:guid}")] public Task<VoucherDto> Get(Guid id, CancellationToken ct) => vouchers.GetAsync(id, ct);
    [Authorize(Policies.Post), HttpPost] public async Task<IdResponse> Create(VoucherRequest r, CancellationToken ct) => Id(await vouchers.CreateAsync(r, ct));
    [Authorize(Policies.Void), HttpPost("{id:guid}/void")] public async Task<IdResponse> Void(Guid id, VoidRequest r, CancellationToken ct) => Id(await vouchers.VoidAsync(id, r, ct));
}

// ===================================================================== Reports
public sealed class ReportsController(ReportService reports) : ApiController
{
    [HttpGet("dashboard")] public Task<DashboardDto> Dashboard(CancellationToken ct) => reports.DashboardAsync(ct);

    [HttpGet("profit-loss")]
    public Task<ProfitLossDto> ProfitLoss([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        reports.ProfitLossAsync(from, to, ct);

    [HttpGet("inventory-valuation")] public Task<InventoryValuationDto> Valuation(CancellationToken ct) => reports.InventoryValuationAsync(ct);
    [HttpGet("balances")] public Task<BalancesDto> Balances(CancellationToken ct) => reports.BalancesAsync(ct);
}
