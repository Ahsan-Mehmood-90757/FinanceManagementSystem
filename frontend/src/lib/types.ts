// DTOs mirror the ASP.NET Core API (System.Text.Json camelCase, enums as strings).

export type Guid = string;
export type DateStr = string; // yyyy-MM-dd

export type PartyKind = "Customer" | "Supplier" | "Both";
export type AccountType = "Cash" | "Bank" | "MobileWallet";
export type DocumentStatus = "Posted" | "Voided";
export type VoucherType = "Receipt" | "Payment" | "Expense" | "Transfer";
export type BatchSource = "Opening" | "Purchase";
export type DocumentType =
  | "Opening"
  | "PurchaseBill"
  | "SaleBill"
  | "PurchaseReturn"
  | "SaleReturn"
  | "Voucher"
  | "StockAdjustment";
export type UserRole = "Owner" | "Admin" | "Accountant" | "Viewer";

export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface IdResponse {
  id: Guid;
}

// ---- auth
export interface User {
  id: Guid;
  username: string;
  fullName: string;
  role: UserRole;
  isActive: boolean;
  lastLoginAt: string | null;
}
export interface LoginResponse {
  token: string;
  expiresAt: string;
  user: User;
}

// ---- master data
export interface Unit {
  id: Guid;
  name: string;
  symbol: string;
  allowsFractions: boolean;
  productCount: number;
}
export interface Category {
  id: Guid;
  name: string;
  parentId: Guid | null;
  parentName: string | null;
  productCount: number;
}
export interface ExpenseCategory {
  id: Guid;
  name: string;
  description: string | null;
  isActive: boolean;
}
export interface Product {
  id: Guid;
  sku: string;
  name: string;
  description: string | null;
  categoryId: Guid | null;
  categoryName: string | null;
  unitId: Guid;
  unitSymbol: string;
  allowsFractions: boolean;
  defaultSalePrice: number;
  reorderLevel: number;
  tracksExpiry: boolean;
  isActive: boolean;
  stockOnHand: number;
  stockValue: number;
}
export interface Party {
  id: Guid;
  code: string;
  name: string;
  kind: PartyKind;
  phone: string | null;
  email: string | null;
  address: string | null;
  taxNumber: string | null;
  notes: string | null;
  openingBalance: number;
  balance: number;
  isActive: boolean;
  isSystem: boolean;
}
export interface Account {
  id: Guid;
  name: string;
  type: AccountType;
  bankName: string | null;
  accountNumber: string | null;
  openingBalance: number;
  balance: number;
  allowNegative: boolean;
  isActive: boolean;
}

export interface StatementLine {
  date: DateStr;
  documentType: DocumentType;
  documentId: Guid;
  documentNumber: string;
  description: string;
  debit: number;
  credit: number;
  runningBalance: number;
  isReversal: boolean;
}
export interface Statement {
  id: Guid;
  name: string;
  from: DateStr | null;
  to: DateStr | null;
  openingBalance: number;
  totalDebit: number;
  totalCredit: number;
  closingBalance: number;
  lines: StatementLine[];
}

// ---- inventory
export interface Batch {
  id: Guid;
  productId: Guid;
  productName: string;
  sku: string;
  unitSymbol: string;
  batchNumber: string;
  source: BatchSource;
  receivedOn: DateStr;
  expiryDate: DateStr | null;
  unitCost: number;
  originalQuantity: number;
  remainingQuantity: number;
  remainingValue: number;
}
export interface Movement {
  id: Guid;
  date: DateStr;
  documentType: DocumentType;
  documentId: Guid;
  documentNumber: string;
  batchNumber: string;
  quantityIn: number;
  quantityOut: number;
  unitCost: number;
  isReversal: boolean;
  note: string | null;
}
export interface FifoPreview {
  productId: Guid;
  requested: number;
  available: number;
  sufficient: boolean;
  totalCost: number;
  allocations: {
    batchId: Guid;
    batchNumber: string;
    receivedOn: DateStr;
    expiryDate: DateStr | null;
    quantity: number;
    unitCost: number;
    cost: number;
  }[];
}
export interface StockAdjustment {
  id: Guid;
  number: string;
  date: DateStr;
  productId: Guid;
  productName: string;
  batchNumber: string;
  quantity: number;
  unitCost: number;
  value: number;
  reason: string;
  createdBy: string | null;
}

// ---- documents
export interface DocumentListItem {
  id: Guid;
  number: string;
  date: DateStr;
  partyId: Guid;
  partyName: string;
  total: number;
  status: DocumentStatus;
  lineCount: number;
  createdBy: string | null;
  reference: string | null;
}
export interface ReturnListItem {
  id: Guid;
  number: string;
  date: DateStr;
  billId: Guid;
  billNumber: string;
  partyId: Guid;
  partyName: string;
  total: number;
  reason: string | null;
  createdBy: string | null;
}
export interface Charge {
  id: Guid;
  expenseCategoryId: Guid;
  expenseCategoryName: string;
  description: string | null;
  amount: number;
  paidFromAccountId: Guid | null;
  paidFromAccountName: string | null;
}
export interface LinkedVoucher {
  id: Guid;
  number: string;
  type: VoucherType;
  date: DateStr;
  accountName: string;
  amount: number;
  status: DocumentStatus;
}
export interface ReturnSummary {
  id: Guid;
  number: string;
  date: DateStr;
  total: number;
}
interface DocMeta {
  createdAt: string;
  createdBy: string | null;
  voidedAt: string | null;
  voidedBy: string | null;
  voidReason: string | null;
}
export interface PurchaseLine {
  id: Guid;
  productId: Guid;
  productName: string;
  sku: string;
  unitSymbol: string;
  quantity: number;
  unitCost: number;
  lineTotal: number;
  returnedQuantity: number;
  batchId: Guid;
  batchNumber: string;
  expiryDate: DateStr | null;
  batchRemaining: number;
}
export interface PurchaseBill extends DocMeta {
  id: Guid;
  number: string;
  date: DateStr;
  partyId: Guid;
  partyCode: string;
  partyName: string;
  supplierReference: string | null;
  notes: string | null;
  status: DocumentStatus;
  subtotal: number;
  chargesOnParty: number;
  chargesTotal: number;
  total: number;
  lines: PurchaseLine[];
  charges: Charge[];
  settlements: LinkedVoucher[];
  returns: ReturnSummary[];
}
export interface Allocation {
  batchId: Guid;
  batchNumber: string;
  expiryDate: DateStr | null;
  quantity: number;
  unitCost: number;
  cost: number;
  returnedQuantity: number;
}
export interface SaleLine {
  id: Guid;
  productId: Guid;
  productName: string;
  sku: string;
  unitSymbol: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  costTotal: number;
  returnedQuantity: number;
  allocations: Allocation[];
}
export interface SaleBill extends DocMeta {
  id: Guid;
  number: string;
  date: DateStr;
  partyId: Guid;
  partyCode: string;
  partyName: string;
  partyPhone: string | null;
  partyAddress: string | null;
  customerReference: string | null;
  notes: string | null;
  status: DocumentStatus;
  subtotal: number;
  costOfGoods: number;
  grossProfit: number;
  chargesTotal: number;
  total: number;
  lines: SaleLine[];
  charges: Charge[];
  settlements: LinkedVoucher[];
  returns: ReturnSummary[];
}
export interface ReturnDoc {
  id: Guid;
  number: string;
  date: DateStr;
  billId: Guid;
  billNumber: string;
  partyId: Guid;
  partyName: string;
  reason: string | null;
  total: number;
  costTotal?: number;
  lines: {
    productId: Guid;
    productName: string;
    unitSymbol: string;
    quantity: number;
    unitCost?: number;
    unitPrice?: number;
    amount: number;
    costReversed?: number;
  }[];
  refunds: LinkedVoucher[];
  createdAt: string;
  createdBy: string | null;
}
export interface Voucher {
  id: Guid;
  number: string;
  type: VoucherType;
  date: DateStr;
  partyId: Guid | null;
  partyName: string | null;
  accountId: Guid;
  accountName: string;
  toAccountId: Guid | null;
  toAccountName: string | null;
  expenseCategoryId: Guid | null;
  expenseCategoryName: string | null;
  amount: number;
  reference: string | null;
  notes: string | null;
  status: DocumentStatus;
  linkedDocumentType: DocumentType | null;
  linkedDocumentId: Guid | null;
  linkedDocumentNumber: string | null;
  createdAt: string;
  createdBy: string | null;
  voidedAt: string | null;
  voidedBy: string | null;
  voidReason: string | null;
}

// ---- setup & reports
export interface Company {
  companyName: string;
  address: string | null;
  phone: string | null;
  email: string | null;
  taxNumber: string | null;
  invoiceFooter: string | null;
  goLiveDate: DateStr;
  isOperational: boolean;
  setupCompletedAt: string | null;
  setupCompletedBy: string | null;
}
export interface SetupStatus {
  company: Company;
  openingDate: DateStr;
  partiesWithOpening: number;
  openingReceivable: number;
  openingPayable: number;
  accountsWithOpening: number;
  openingCashBank: number;
  openingBatches: number;
  openingStockValue: number;
}
export interface Dashboard {
  today: DateStr;
  salesToday: number;
  salesMonth: number;
  grossProfitMonth: number;
  expensesMonth: number;
  netProfitMonth: number;
  cashAndBank: number;
  receivables: number;
  payables: number;
  stockValue: number;
  lowStockCount: number;
  expiringSoonCount: number;
  accounts: { id: Guid; name: string; type: AccountType; balance: number }[];
  trend: { date: DateStr; sales: number; grossProfit: number }[];
  recent: {
    id: Guid;
    kind: "sale" | "purchase" | "voucher";
    number: string;
    date: DateStr;
    counterparty: string | null;
    amount: number;
    status: DocumentStatus;
  }[];
  topProducts: { productId: Guid; name: string; quantity: number; revenue: number; grossProfit: number }[];
}
export interface AmountByName {
  name: string;
  amount: number;
}
export interface ProfitLoss {
  from: DateStr;
  to: DateStr;
  grossSales: number;
  salesReturns: number;
  netSales: number;
  costOfGoodsSold: number;
  costReversedOnReturns: number;
  netCostOfGoodsSold: number;
  grossProfit: number;
  grossMarginPercent: number;
  billCharges: AmountByName[];
  billChargesTotal: number;
  operatingExpenses: AmountByName[];
  operatingExpensesTotal: number;
  stockWriteOffs: number;
  totalExpenses: number;
  netProfit: number;
  netMarginPercent: number;
}
export interface Valuation {
  totalValue: number;
  totalQuantityLines: number;
  rows: {
    productId: Guid;
    sku: string;
    name: string;
    category: string | null;
    unitSymbol: string;
    quantity: number;
    value: number;
    averageCost: number;
    earliestExpiry: DateStr | null;
    batchCount: number;
  }[];
}
export interface PartyBalanceRow {
  id: Guid;
  code: string;
  name: string;
  kind: PartyKind;
  phone: string | null;
  balance: number;
}
export interface Balances {
  totalReceivable: number;
  totalPayable: number;
  receivables: PartyBalanceRow[];
  payables: PartyBalanceRow[];
}
