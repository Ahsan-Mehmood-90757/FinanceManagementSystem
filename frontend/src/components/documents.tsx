import { useEffect, useState, type ReactNode } from "react";
import { Link, useNavigate, useSearchParams } from "react-router";
import { Plus, Trash2 } from "lucide-react";
import { api } from "../lib/api";
import { useAction, useApi, useCompany, useDebounced } from "../lib/hooks";
import { useAuth } from "../lib/auth";
import { cx, date, money, qty, today } from "../lib/format";
import type { DocumentListItem, LinkedVoucher, Paged, ReturnDoc, ReturnListItem } from "../lib/types";
import {
  Badge,
  Button,
  Card,
  EmptyState,
  ErrorNote,
  Field,
  Input,
  NumberInput,
  PageHeader,
  Pagination,
  SearchInput,
  Spinner,
  StatusBadge,
  Tabs,
  Textarea,
  Modal,
} from "./ui";
import { AccountSelect, ExpenseCategorySelect } from "./pickers";

type Kind = "sale" | "purchase";
const cfg = {
  sale: { base: "/sales", api: "/sales", returnsApi: "/sale-returns", title: "Sales", party: "Customer", noun: "sale" },
  purchase: { base: "/purchases", api: "/purchases", returnsApi: "/purchase-returns", title: "Purchases", party: "Supplier", noun: "purchase" },
} as const;

// ------------------------------------------------------------------ list page (bills + returns tabs)
export function DocumentList({ kind }: { kind: Kind }) {
  const c = cfg[kind];
  const { can } = useAuth();
  const [params, setParams] = useSearchParams();
  const tab = (params.get("tab") as "bills" | "returns") ?? "bills";
  const [search, setSearch] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [page, setPage] = useState(1);
  const debounced = useDebounced(search);
  useEffect(() => setPage(1), [debounced, from, to, tab]);

  const query = { search: debounced, from, to, page, pageSize: 25 };
  const bills = useApi<Paged<DocumentListItem>>(tab === "bills" ? c.api : null, query, { keepPrevious: true });
  const returns = useApi<Paged<ReturnListItem>>(tab === "returns" ? c.returnsApi : null, query, { keepPrevious: true });
  const active = tab === "bills" ? bills : returns;

  return (
    <>
      <PageHeader
        title={c.title}
        description={kind === "sale" ? "Posted sale bills draw stock from the oldest batches first." : "Each purchase line becomes a stock batch at its cost."}
        actions={
          can("post") && (
            <Link to={`${c.base}/new`}>
              <Button variant="primary" icon={<Plus className="size-3.5" />}>
                New {c.noun}
              </Button>
            </Link>
          )
        }
      />
      <div className="mb-3 flex flex-wrap items-center gap-2">
        <Tabs
          value={tab}
          onChange={(v) => setParams(v === "bills" ? {} : { tab: v })}
          tabs={[
            { value: "bills", label: "Bills" },
            { value: "returns", label: "Returns" },
          ]}
        />
        <SearchInput value={search} onChange={setSearch} placeholder={`Number or ${c.party.toLowerCase()}`} />
        <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="w-[150px]" aria-label="From date" />
        <span className="text-muted">to</span>
        <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="w-[150px]" aria-label="To date" />
      </div>

      <div className="overflow-hidden rounded-xl border border-rule bg-surface">
        <ErrorNote error={active.error} />
        {active.isLoading ? (
          <Spinner />
        ) : tab === "bills" ? (
          bills.data && bills.data.items.length > 0 ? (
            <table className="table-dense w-full">
              <thead>
                <tr>
                  <th>Number</th>
                  <th>Date</th>
                  <th>{c.party}</th>
                  <th>Reference</th>
                  <th className="num">Lines</th>
                  <th>Status</th>
                  <th className="num">Total</th>
                </tr>
              </thead>
              <tbody>
                {bills.data.items.map((b) => (
                  <tr key={b.id} className={cx(b.status === "Voided" && "text-muted")}>
                    <td>
                      <Link to={`${c.base}/${b.id}`} className="font-medium text-ink hover:text-accent-strong">
                        {b.number}
                      </Link>
                    </td>
                    <td>{date(b.date)}</td>
                    <td>{b.partyName}</td>
                    <td className="text-muted">{b.reference ?? ""}</td>
                    <td className="num">{b.lineCount}</td>
                    <td>
                      <StatusBadge status={b.status} />
                    </td>
                    <td className={cx("num font-medium", b.status === "Voided" && "line-through")}>{money(b.total)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <EmptyState title={`No ${c.noun} bills found`}>
              {debounced || from || to ? "Try a wider date range or a different search." : `Post your first ${c.noun} to see it here.`}
            </EmptyState>
          )
        ) : returns.data && returns.data.items.length > 0 ? (
          <table className="table-dense w-full">
            <thead>
              <tr>
                <th>Number</th>
                <th>Date</th>
                <th>Against bill</th>
                <th>{c.party}</th>
                <th>Reason</th>
                <th className="num">Total</th>
              </tr>
            </thead>
            <tbody>
              {returns.data.items.map((r) => (
                <tr key={r.id}>
                  <td>
                    <Link to={`${c.base}/returns/${r.id}`} className="font-medium hover:text-accent-strong">
                      {r.number}
                    </Link>
                  </td>
                  <td>{date(r.date)}</td>
                  <td>
                    <Link to={`${c.base}/${r.billId}`} className="hover:text-accent-strong">
                      {r.billNumber}
                    </Link>
                  </td>
                  <td>{r.partyName}</td>
                  <td className="max-w-[260px] truncate text-muted">{r.reason ?? ""}</td>
                  <td className="num font-medium">{money(r.total)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <EmptyState title="No returns found">Returns are created from a posted bill.</EmptyState>
        )}
        {active.data && <Pagination page={active.data.page} pageSize={active.data.pageSize} total={active.data.total} onPage={setPage} />}
      </div>
    </>
  );
}

// ------------------------------------------------------------------ charges editor
export interface ChargeDraft {
  key: number;
  expenseCategoryId: string;
  description: string;
  amount: number | "";
  paidFromAccountId: string; // "" = added to supplier bill (purchases only)
}

export function ChargesEditor({
  charges,
  setCharges,
  allowOnParty,
}: {
  charges: ChargeDraft[];
  setCharges: (c: ChargeDraft[]) => void;
  allowOnParty: boolean;
}) {
  const update = (key: number, patch: Partial<ChargeDraft>) => setCharges(charges.map((c) => (c.key === key ? { ...c, ...patch } : c)));
  return (
    <Card
      title="Operational charges"
      actions={
        <Button
          size="sm"
          variant="ghost"
          icon={<Plus className="size-3.5" />}
          onClick={() => setCharges([...charges, { key: Date.now(), expenseCategoryId: "", description: "", amount: "", paidFromAccountId: "" }])}
        >
          Add charge
        </Button>
      }
      bodyClassName={charges.length ? "p-0" : "px-4 py-3"}
    >
      {charges.length === 0 ? (
        <p className="text-[12.5px] text-muted">
          Freight, loading or equipment hire for this bill. Tracked against the bill, kept out of cost of goods, charged to net profit.
        </p>
      ) : (
        <table className="table-dense w-full">
          <thead>
            <tr>
              <th className="w-[200px]">Category</th>
              <th>Description</th>
              <th className="w-[220px]">Paid from</th>
              <th className="num w-[130px]">Amount</th>
              <th className="w-8" />
            </tr>
          </thead>
          <tbody>
            {charges.map((c) => (
              <tr key={c.key}>
                <td>
                  <ExpenseCategorySelect value={c.expenseCategoryId} onChange={(v) => update(c.key, { expenseCategoryId: v })} />
                </td>
                <td>
                  <Input value={c.description} onChange={(e) => update(c.key, { description: e.target.value })} placeholder="Optional" />
                </td>
                <td>
                  <AccountSelect
                    value={c.paidFromAccountId}
                    onChange={(v) => update(c.key, { paidFromAccountId: v })}
                    allowEmpty={allowOnParty}
                    emptyLabel={allowOnParty ? "Added to supplier bill" : "Select account"}
                  />
                </td>
                <td>
                  <NumberInput value={c.amount} onChange={(v) => update(c.key, { amount: v })} min={0} />
                </td>
                <td>
                  <button className="rounded p-1 text-muted hover:bg-owe-soft hover:text-owe" aria-label="Remove charge" onClick={() => setCharges(charges.filter((x) => x.key !== c.key))}>
                    <Trash2 className="size-3.5" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </Card>
  );
}

export const chargesToRequest = (charges: ChargeDraft[]) =>
  charges
    .filter((c) => c.expenseCategoryId && Number(c.amount) > 0)
    .map((c) => ({
      expenseCategoryId: c.expenseCategoryId,
      description: c.description || null,
      amount: Number(c.amount),
      paidFromAccountId: c.paidFromAccountId || null,
    }));

// ------------------------------------------------------------------ return dialog
export interface ReturnableLine {
  id: string;
  productName: string;
  unitSymbol: string;
  returnable: number;
  rate: number;
  note?: string;
}

export function ReturnDialog({
  kind,
  billId,
  billNumber,
  billDate,
  lines,
  open,
  onClose,
}: {
  kind: Kind;
  billId: string;
  billNumber: string;
  billDate: string;
  lines: ReturnableLine[];
  open: boolean;
  onClose: () => void;
}) {
  const navigate = useNavigate();
  const [dateStr, setDate] = useState(today());
  const [reason, setReason] = useState("");
  const [qtys, setQtys] = useState<Record<string, number | "">>({});
  const [refundAccount, setRefundAccount] = useState("");
  const [refund, setRefund] = useState<number | "">("");

  useEffect(() => {
    if (open) {
      setQtys({});
      setReason("");
      setRefund("");
      setRefundAccount("");
      setDate(today());
    }
  }, [open]);

  const total = lines.reduce((s, l) => s + Number(qtys[l.id] || 0) * l.rate, 0);
  const action = useAction(
    () =>
      api.post<{ id: string }>(`${cfg[kind].api}/${billId}/returns`, {
        date: dateStr,
        reason: reason || null,
        lines: lines.filter((l) => Number(qtys[l.id]) > 0).map((l) => ({ billLineId: l.id, quantity: Number(qtys[l.id]) })),
        refund: refundAccount && Number(refund) > 0 ? { accountId: refundAccount, amount: Number(refund), reference: null } : null,
      }),
    {
      success: "Return posted",
      onSuccess: (r) => {
        onClose();
        navigate(`${cfg[kind].base}/returns/${r.id}`);
      },
    },
  );

  return (
    <Modal
      open={open}
      onClose={onClose}
      width="max-w-2xl"
      title={`Return against ${billNumber}`}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" loading={action.isPending} disabled={total <= 0} onClick={() => action.mutate(undefined)}>
            Post return of Rs {money(total)}
          </Button>
        </>
      }
    >
      <div className="mb-3 grid grid-cols-[150px_1fr] gap-3">
        <Field label="Return date">
          <Input type="date" value={dateStr} min={billDate} onChange={(e) => setDate(e.target.value)} />
        </Field>
        <Field label="Reason">
          <Input value={reason} onChange={(e) => setReason(e.target.value)} placeholder={kind === "sale" ? "e.g. Damaged in transit" : "e.g. Wrong item delivered"} />
        </Field>
      </div>
      <table className="table-dense w-full rounded-lg border border-rule">
        <thead>
          <tr>
            <th>Product</th>
            <th className="num">Returnable</th>
            <th className="num">Rate</th>
            <th className="num w-[120px]">Return qty</th>
          </tr>
        </thead>
        <tbody>
          {lines.map((l) => (
            <tr key={l.id}>
              <td>
                {l.productName}
                {l.note && <div className="text-[11.5px] text-muted">{l.note}</div>}
              </td>
              <td className="num">
                {qty(l.returnable)} {l.unitSymbol}
              </td>
              <td className="num">{money(l.rate)}</td>
              <td>
                <NumberInput
                  value={qtys[l.id] ?? ""}
                  disabled={l.returnable <= 0}
                  min={0}
                  max={l.returnable}
                  onChange={(v) => setQtys({ ...qtys, [l.id]: v === "" ? "" : Math.min(Number(v), l.returnable) })}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <div className="mt-4 grid grid-cols-2 gap-3 rounded-lg border border-dashed border-rule p-3">
        <Field label={kind === "sale" ? "Refund cash now from" : "Receive refund now into"} hint="Leave empty to keep it as credit on the party's balance.">
          <AccountSelect value={refundAccount} onChange={setRefundAccount} allowEmpty emptyLabel="No refund now" />
        </Field>
        <Field label="Refund amount">
          <div className="flex gap-2">
            <NumberInput value={refund} onChange={setRefund} disabled={!refundAccount} min={0} />
            <Button size="md" variant="ghost" disabled={!refundAccount} onClick={() => setRefund(Math.round(total * 100) / 100)}>
              Full
            </Button>
          </div>
        </Field>
      </div>
    </Modal>
  );
}

// ------------------------------------------------------------------ return detail
export function ReturnDetail({ kind, id }: { kind: Kind; id: string }) {
  const c = cfg[kind];
  const { data, isLoading, error } = useApi<ReturnDoc>(`${c.returnsApi}/${id}`);
  if (isLoading) return <Spinner />;
  if (error || !data) return <ErrorNote error={error} />;
  return (
    <>
      <PageHeader
        title={data.number}
        description={
          <>
            {kind === "sale" ? "Sale" : "Purchase"} return against{" "}
            <Link className="text-accent-strong hover:underline" to={`${c.base}/${data.billId}`}>
              {data.billNumber}
            </Link>{" "}
            for {data.partyName}, {date(data.date)}
          </>
        }
        actions={<Button onClick={() => window.print()}>Print</Button>}
      />
      <div className="grid gap-4 lg:grid-cols-[1fr_300px]">
        <Card bodyClassName="p-0" className="print-area">
          <table className="table-dense w-full">
            <thead>
              <tr>
                <th>Product</th>
                <th className="num">Qty</th>
                <th className="num">Rate</th>
                <th className="num">Amount</th>
                {kind === "sale" && <th className="num">Cost reversed</th>}
              </tr>
            </thead>
            <tbody>
              {data.lines.map((l, i) => (
                <tr key={i}>
                  <td>{l.productName}</td>
                  <td className="num">
                    {qty(l.quantity)} {l.unitSymbol}
                  </td>
                  <td className="num">{money(l.unitPrice ?? l.unitCost ?? 0)}</td>
                  <td className="num">{money(l.amount)}</td>
                  {kind === "sale" && <td className="num text-muted">{money(l.costReversed ?? 0)}</td>}
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan={3} className="px-3 py-2 text-right font-medium">
                  Total
                </td>
                <td className="num px-3 py-2 font-semibold">{money(data.total)}</td>
                {kind === "sale" && <td className="num px-3 py-2 text-muted">{money(data.costTotal ?? 0)}</td>}
              </tr>
            </tfoot>
          </table>
        </Card>
        <div className="no-print space-y-4">
          <Card title="Details">
            <dl className="space-y-1.5 text-[13px]">
              <Row label="Reason">{data.reason ?? "—"}</Row>
              <Row label="Posted by">{data.createdBy ?? "—"}</Row>
              <Row label="Effect">{kind === "sale" ? "Customer credited, stock restored" : "Supplier debited, stock removed"}</Row>
            </dl>
          </Card>
          <LinkedVouchers title="Refunds" vouchers={data.refunds} empty="No cash refund; the amount sits on the party balance." />
        </div>
      </div>
    </>
  );
}

export function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex justify-between gap-3">
      <dt className="text-muted">{label}</dt>
      <dd className="text-right">{children}</dd>
    </div>
  );
}

export function LinkedVouchers({ title, vouchers, empty }: { title: string; vouchers: LinkedVoucher[]; empty: string }) {
  return (
    <Card title={title} bodyClassName={vouchers.length ? "p-0" : undefined}>
      {vouchers.length === 0 ? (
        <p className="text-[12.5px] text-muted">{empty}</p>
      ) : (
        <ul className="divide-y divide-rule">
          {vouchers.map((v) => (
            <li key={v.id} className="flex items-center justify-between px-4 py-2 text-[13px]">
              <div>
                <Link to={`/vouchers?open=${v.id}`} className={cx("font-medium hover:text-accent-strong", v.status === "Voided" && "line-through text-muted")}>
                  {v.number}
                </Link>
                <div className="text-[12px] text-muted">
                  {v.accountName}, {date(v.date)}
                </div>
              </div>
              <div className="text-right">
                <div className="tabular-nums">{money(v.amount)}</div>
                {v.status === "Voided" && <Badge tone="owe">Voided</Badge>}
              </div>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}

/** Company letterhead used on printed documents. */
export function Letterhead({ title, number }: { title: string; number: string }) {
  const { data: company } = useCompany();
  return (
    <div className="mb-6 flex items-start justify-between gap-6 border-b border-rule pb-5">
      <div>
        <div className="text-[18px] font-semibold">{company?.companyName}</div>
        <div className="mt-1 whitespace-pre-line text-[12.5px] leading-5 text-slate-600">
          {[company?.address, company?.phone, company?.email, company?.taxNumber && `NTN ${company.taxNumber}`].filter(Boolean).join("\n")}
        </div>
      </div>
      <div className="text-right">
        <div className="text-[13px] text-muted">{title}</div>
        <div className="text-[20px] font-semibold tracking-[-0.01em]">{number}</div>
      </div>
    </div>
  );
}

export function InvoiceFooter() {
  const { data: company } = useCompany();
  if (!company?.invoiceFooter) return null;
  return <p className="mt-8 border-t border-rule pt-3 text-center text-[12px] text-muted">{company.invoiceFooter}</p>;
}

export function NotesField({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  return (
    <Field label="Notes">
      <Textarea value={value} onChange={(e) => onChange(e.target.value)} rows={2} placeholder="Printed on the bill" />
    </Field>
  );
}
