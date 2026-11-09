namespace Fms.Domain;

[Flags]
public enum PartyKind
{
    Customer = 1,
    Supplier = 2,
    Both = Customer | Supplier
}

public enum AccountType
{
    Cash = 1,
    Bank = 2,
    MobileWallet = 3
}

public enum DocumentStatus
{
    Posted = 1,
    Voided = 2
}

public enum VoucherType
{
    /// <summary>Money received from a party into an account.</summary>
    Receipt = 1,
    /// <summary>Money paid to a party out of an account.</summary>
    Payment = 2,
    /// <summary>Standalone operating expense paid out of an account (rent, salaries, utilities...).</summary>
    Expense = 3,
    /// <summary>Move money between two of our own accounts.</summary>
    Transfer = 4
}

public enum BatchSource
{
    Opening = 1,
    Purchase = 2
}

/// <summary>Source document for ledger and stock movement rows.</summary>
public enum DocumentType
{
    Opening = 1,
    PurchaseBill = 2,
    SaleBill = 3,
    PurchaseReturn = 4,
    SaleReturn = 5,
    Voucher = 6,
    StockAdjustment = 7
}

public enum UserRole
{
    /// <summary>Unrestricted. Created on first start; can do everything including user management.</summary>
    Owner = 1,
    /// <summary>Everything except managing users.</summary>
    Admin = 2,
    /// <summary>Day-to-day posting and master data. Cannot void documents or change setup.</summary>
    Accountant = 3,
    /// <summary>Read-only.</summary>
    Viewer = 4
}
