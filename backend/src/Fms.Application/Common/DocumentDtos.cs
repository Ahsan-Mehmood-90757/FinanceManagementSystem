using Fms.Domain;

namespace Fms.Application.Common;

public sealed record ChargeRequest(Guid ExpenseCategoryId, string? Description, decimal Amount, Guid? PaidFromAccountId);

/// <summary>Optional immediate cash/bank settlement when posting a bill or return.</summary>
public sealed record SettlementRequest(Guid AccountId, decimal Amount, string? Reference);

public sealed record ChargeDto(Guid Id, Guid ExpenseCategoryId, string ExpenseCategoryName, string? Description,
    decimal Amount, Guid? PaidFromAccountId, string? PaidFromAccountName);

public sealed record LinkedVoucherDto(Guid Id, string Number, VoucherType Type, DateOnly Date, string AccountName,
    decimal Amount, DocumentStatus Status);

public sealed record ReturnSummaryDto(Guid Id, string Number, DateOnly Date, decimal Total);

public sealed record DocumentListItem(Guid Id, string Number, DateOnly Date, Guid PartyId, string PartyName,
    decimal Total, DocumentStatus Status, int LineCount, string? CreatedBy, string? Reference);

public sealed record ReturnListItem(Guid Id, string Number, DateOnly Date, Guid BillId, string BillNumber,
    Guid PartyId, string PartyName, decimal Total, string? Reason, string? CreatedBy);

public sealed record ReturnLineRequest(Guid BillLineId, decimal Quantity);

/// <summary>Return against a posted bill; <see cref="Refund"/> optionally settles cash immediately.</summary>
public sealed record ReturnRequest(DateOnly Date, string? Reason, IReadOnlyList<ReturnLineRequest> Lines, SettlementRequest? Refund);
