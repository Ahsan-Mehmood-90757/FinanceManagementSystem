const pkr = new Intl.NumberFormat("en-PK", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const compact = new Intl.NumberFormat("en-PK", { notation: "compact", maximumFractionDigits: 1 });
const qtyFmt = new Intl.NumberFormat("en-PK", { maximumFractionDigits: 4 });

/** 1,234,567.50 — currency shown separately as "Rs" where needed. */
export const money = (n: number | null | undefined) => pkr.format(n ?? 0);
export const rs = (n: number | null | undefined) => `Rs ${money(n)}`;
export const rsCompact = (n: number) => `Rs ${compact.format(n)}`;
export const qty = (n: number | null | undefined) => qtyFmt.format(n ?? 0);

/** Party balance in words: positive = they owe us, negative = we owe them. */
export function balanceLabel(balance: number) {
  if (balance > 0) return { text: `${money(balance)} receivable`, tone: "accent" as const };
  if (balance < 0) return { text: `${money(-balance)} payable`, tone: "owe" as const };
  return { text: "Settled", tone: "muted" as const };
}

const dateFmt = new Intl.DateTimeFormat("en-GB", { day: "2-digit", month: "short", year: "numeric" });
export function date(d: string | null | undefined) {
  if (!d) return "—";
  const [y, m, day] = d.slice(0, 10).split("-").map(Number);
  return dateFmt.format(new Date(y, m - 1, day));
}
export function dateTime(d: string | null | undefined) {
  if (!d) return "—";
  return new Date(d).toLocaleString("en-GB", { dateStyle: "medium", timeStyle: "short" });
}

/** Today in the browser's local time as yyyy-MM-dd. */
export function today() {
  const d = new Date();
  return toIso(d);
}
export function toIso(d: Date) {
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${d.getFullYear()}-${m}-${day}`;
}
export function monthStart() {
  const d = new Date();
  return toIso(new Date(d.getFullYear(), d.getMonth(), 1));
}
export function daysUntil(d: string | null) {
  if (!d) return null;
  const [y, m, day] = d.split("-").map(Number);
  const target = new Date(y, m - 1, day).getTime();
  const now = new Date();
  const base = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
  return Math.round((target - base) / 86_400_000);
}

export const documentLabel: Record<string, string> = {
  Opening: "Opening",
  PurchaseBill: "Purchase",
  SaleBill: "Sale",
  PurchaseReturn: "Purchase return",
  SaleReturn: "Sale return",
  Voucher: "Voucher",
  StockAdjustment: "Write-off",
};

export function documentHref(type: string, id: string, number?: string): string | null {
  switch (type) {
    case "SaleBill":
      return `/sales/${id}`;
    case "PurchaseBill":
      return `/purchases/${id}`;
    case "SaleReturn":
      return `/sales/returns/${id}`;
    case "PurchaseReturn":
      return `/purchases/returns/${id}`;
    case "Voucher":
      return `/vouchers?open=${id}${number ? `&n=${number}` : ""}`;
    default:
      return null;
  }
}

export const cx = (...c: (string | false | null | undefined)[]) => c.filter(Boolean).join(" ");
