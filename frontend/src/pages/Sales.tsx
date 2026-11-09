import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { Layers, Plus, Printer, RotateCcw, Trash2, Ban } from "lucide-react";
import { api } from "../lib/api";
import { useAction, useApi } from "../lib/hooks";
import { useAuth } from "../lib/auth";
import { cx, date, dateTime, money, qty, today } from "../lib/format";
import type { Batch, FifoPreview, Paged, Party, Product, SaleBill } from "../lib/types";
import { Badge, Button, Card, Checkbox, ErrorNote, Field, Input, Modal, NumberInput, PageHeader, Spinner, StatusBadge, VoidDialog } from "../components/ui";
import { AccountSelect, PartyPicker, ProductPicker } from "../components/pickers";
import {
  ChargesEditor,
  DocumentList,
  InvoiceFooter,
  Letterhead,
  LinkedVouchers,
  NotesField,
  ReturnDetail,
  ReturnDialog,
  Row,
  chargesToRequest,
  type ChargeDraft,
} from "../components/documents";

export const SalesListPage = () => <DocumentList kind="sale" />;

// =================================================================== new sale
interface Pick {
  batchId: string;
  quantity: number;
}
interface SaleLineDraft {
  key: number;
  product: Product | null;
  quantity: number | "";
  unitPrice: number | "";
  picks: Pick[] | null; // null = automatic FIFO
}

const blankLine = (): SaleLineDraft => ({ key: Date.now() + Math.random(), product: null, quantity: "", unitPrice: "", picks: null });

export function NewSalePage() {
  const navigate = useNavigate();
  const [walkIn, setWalkIn] = useState(false);
  const [party, setParty] = useState<Party | null>(null);
  const [billDate, setBillDate] = useState(today());
  const [reference, setReference] = useState("");
  const [notes, setNotes] = useState("");
  const [lines, setLines] = useState<SaleLineDraft[]>([blankLine()]);
  const [charges, setCharges] = useState<ChargeDraft[]>([]);
  const [payAccount, setPayAccount] = useState("");
  const [paid, setPaid] = useState<number | "">("");
  const [batchFor, setBatchFor] = useState<SaleLineDraft | null>(null);
  const [costs, setCosts] = useState<Record<number, number>>({});

  const update = (key: number, patch: Partial<SaleLineDraft>) => setLines((ls) => ls.map((l) => (l.key === key ? { ...l, ...patch } : l)));

  const subtotal = lines.reduce((s, l) => s + Number(l.quantity || 0) * Number(l.unitPrice || 0), 0);
  const cogs = lines.reduce((s, l) => s + (costs[l.key] ?? 0), 0);
  const chargesTotal = charges.reduce((s, c) => s + Number(c.amount || 0), 0);
  const paidNum = Number(paid || 0);
  const ready = (walkIn || party) && lines.some((l) => l.product && Number(l.quantity) > 0);

  const post = useAction(
    () =>
      api.post<{ id: string }>("/sales", {
        date: billDate,
        partyId: walkIn ? null : party?.id,
        customerReference: reference || null,
        notes: notes || null,
        lines: lines
          .filter((l) => l.product && Number(l.quantity) > 0)
          .map((l) => ({
            productId: l.product!.id,
            quantity: Number(l.quantity),
            unitPrice: l.unitPrice === "" ? null : Number(l.unitPrice),
            batches: l.picks,
          })),
        charges: chargesToRequest(charges),
        payment: payAccount && paidNum > 0 ? { accountId: payAccount, amount: paidNum, reference: null } : null,
      }),
    { success: "Sale posted", onSuccess: (r) => navigate(`/sales/${r.id}`) },
  );

  return (
    <>
      <PageHeader title="New sale" description="Stock is taken from the oldest batches first unless you pick batches yourself." />
      <div className="grid gap-4 xl:grid-cols-[1fr_300px]">
        <div className="space-y-4">
          <Card>
            <div className="grid gap-3 md:grid-cols-[1.4fr_150px_1fr]">
              <Field label="Customer">
                {walkIn ? (
                  <div className="input flex items-center text-muted">Walk-in customer, paid in full</div>
                ) : (
                  <PartyPicker kind="Customer" value={party} onSelect={setParty} placeholder="Search customer" autoFocus />
                )}
              </Field>
              <Field label="Date">
                <Input type="date" value={billDate} onChange={(e) => setBillDate(e.target.value)} />
              </Field>
              <Field label="Customer reference">
                <Input value={reference} onChange={(e) => setReference(e.target.value)} placeholder="PO number, optional" />
              </Field>
            </div>
            <div className="mt-3">
              <Checkbox
                label="Walk-in cash sale"
                checked={walkIn}
                onChange={(v) => {
                  setWalkIn(v);
                  if (v) setPaid(Math.round(subtotal * 100) / 100);
                }}
              />
            </div>
          </Card>

          <Card title="Items" bodyClassName="p-0" actions={<span className="text-[12px] text-muted">{lines.length} line{lines.length > 1 ? "s" : ""}</span>}>
            <table className="table-dense w-full">
              <thead>
                <tr>
                  <th>Product</th>
                  <th className="num w-[110px]">Qty</th>
                  <th className="num w-[130px]">Unit price</th>
                  <th className="num w-[130px]">Amount</th>
                  <th className="num w-[120px]">FIFO cost</th>
                  <th className="w-[70px]" />
                </tr>
              </thead>
              <tbody>
                {lines.map((l, i) => (
                  <SaleLineRow
                    key={l.key}
                    line={l}
                    date={billDate}
                    autoFocus={i === lines.length - 1 && i > 0}
                    onChange={(p) => update(l.key, p)}
                    onCost={(c) => setCosts((x) => (x[l.key] === c ? x : { ...x, [l.key]: c }))}
                    onPickBatches={() => setBatchFor(l)}
                    onRemove={lines.length > 1 ? () => setLines(lines.filter((x) => x.key !== l.key)) : undefined}
                  />
                ))}
              </tbody>
            </table>
            <div className="border-t border-rule px-3 py-2">
              <Button size="sm" variant="ghost" icon={<Plus className="size-3.5" />} onClick={() => setLines([...lines, blankLine()])}>
                Add line
              </Button>
            </div>
          </Card>

          <ChargesEditor charges={charges} setCharges={setCharges} allowOnParty={false} />
          <Card>
            <NotesField value={notes} onChange={setNotes} />
          </Card>
        </div>

        <aside className="space-y-4 xl:sticky xl:top-6 xl:self-start">
          <Card title="Summary">
            <dl className="space-y-1.5 text-[13px]">
              <Row label="Sale amount">
                <span className="tabular-nums">{money(subtotal)}</span>
              </Row>
              <Row label="FIFO cost">
                <span className="tabular-nums text-muted">{money(cogs)}</span>
              </Row>
              <Row label="Gross profit">
                <span className={cx("tabular-nums", subtotal - cogs < 0 ? "text-owe" : "text-accent-strong")}>{money(subtotal - cogs)}</span>
              </Row>
              {chargesTotal > 0 && (
                <Row label="Charges we pay">
                  <span className="tabular-nums text-muted">{money(chargesTotal)}</span>
                </Row>
              )}
            </dl>
            <div className="my-3 border-t border-rule" />
            <Field label="Received now into">
              <AccountSelect value={payAccount} onChange={setPayAccount} allowEmpty emptyLabel="Nothing received (credit sale)" />
            </Field>
            <div className="mt-2 flex gap-2">
              <NumberInput value={paid} onChange={setPaid} disabled={!payAccount} placeholder="0.00" min={0} />
              <Button variant="ghost" disabled={!payAccount} onClick={() => setPaid(Math.round(subtotal * 100) / 100)}>
                Full
              </Button>
            </div>
            <dl className="mt-3 space-y-1.5 text-[13px]">
              <Row label={walkIn ? "Must be paid" : "Added to customer balance"}>
                <span className="font-semibold tabular-nums">{money(subtotal - paidNum)}</span>
              </Row>
            </dl>
            <Button variant="primary" className="mt-4 h-9 w-full" disabled={!ready} loading={post.isPending} onClick={() => post.mutate(undefined)}>
              Post sale of Rs {money(subtotal)}
            </Button>
            <p className="mt-2 text-[12px] text-muted">Posted bills can't be edited. Mistakes are fixed with a return or a void.</p>
          </Card>
        </aside>
      </div>

      <BatchPicker
        line={batchFor}
        date={billDate}
        onClose={() => setBatchFor(null)}
        onSave={(picks) => {
          if (batchFor) update(batchFor.key, { picks, quantity: picks ? picks.reduce((s, p) => s + p.quantity, 0) : batchFor.quantity });
          setBatchFor(null);
        }}
      />
    </>
  );
}

function SaleLineRow({
  line,
  date: billDate,
  autoFocus,
  onChange,
  onCost,
  onPickBatches,
  onRemove,
}: {
  line: SaleLineDraft;
  date: string;
  autoFocus?: boolean;
  onChange: (p: Partial<SaleLineDraft>) => void;
  onCost: (c: number) => void;
  onPickBatches: () => void;
  onRemove?: () => void;
}) {
  const q = Number(line.quantity || 0);
  const preview = useApi<FifoPreview>(line.product && q > 0 && !line.picks ? `/products/${line.product.id}/fifo-preview` : null, { quantity: q, date: billDate });
  const cost = line.picks ? null : preview.data?.sufficient ? preview.data.totalCost : 0;
  useEffect(() => {
    onCost(cost ?? 0);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [cost]);
  const short = preview.data && !preview.data.sufficient;

  return (
    <tr>
      <td>
        <ProductPicker
          value={line.product}
          autoFocus={autoFocus}
          onSelect={(p) => onChange({ product: p, unitPrice: p.defaultSalePrice, picks: null, quantity: line.quantity || 1 })}
        />
        {line.product && (
          <div className={cx("mt-1 text-[11.5px]", short ? "text-owe" : "text-muted")}>
            {short
              ? `Only ${qty(preview.data!.available)} ${line.product.unitSymbol} available on ${date(billDate)}`
              : `${qty(line.product.stockOnHand)} ${line.product.unitSymbol} in stock`}
            {line.picks && <Badge tone="warn">Manual batches</Badge>}
          </div>
        )}
      </td>
      <td>
        <NumberInput value={line.quantity} onChange={(v) => onChange({ quantity: v, picks: null })} min={0} />
      </td>
      <td>
        <NumberInput value={line.unitPrice} onChange={(v) => onChange({ unitPrice: v })} min={0} />
      </td>
      <td className="num">{money(q * Number(line.unitPrice || 0))}</td>
      <td className="num text-muted">{line.picks ? "manual" : money(cost ?? 0)}</td>
      <td>
        <div className="flex justify-end gap-0.5">
          <button
            disabled={!line.product}
            title="Choose batches (e.g. sell stock close to expiry first)"
            aria-label="Choose batches"
            onClick={onPickBatches}
            className="rounded p-1 text-muted hover:bg-slate-100 hover:text-ink disabled:opacity-40"
          >
            <Layers className="size-3.5" />
          </button>
          {onRemove && (
            <button onClick={onRemove} aria-label="Remove line" className="rounded p-1 text-muted hover:bg-owe-soft hover:text-owe">
              <Trash2 className="size-3.5" />
            </button>
          )}
        </div>
      </td>
    </tr>
  );
}

function BatchPicker({
  line,
  date: billDate,
  onClose,
  onSave,
}: {
  line: SaleLineDraft | null;
  date: string;
  onClose: () => void;
  onSave: (picks: Pick[] | null) => void;
}) {
  const product = line?.product;
  const { data } = useApi<Paged<Batch>>(product ? "/inventory/batches" : null, { productId: product?.id, pageSize: 100 });
  const [values, setValues] = useState<Record<string, number | "">>({});
  const batches = useMemo(
    () => (data?.items ?? []).filter((b) => b.receivedOn <= billDate && (!b.expiryDate || b.expiryDate >= billDate)),
    [data, billDate],
  );
  const total = Object.values(values).reduce<number>((s, v) => s + Number(v || 0), 0);

  return (
    <Modal
      open={!!line}
      onClose={() => {
        setValues({});
        onClose();
      }}
      width="max-w-2xl"
      title={`Choose batches for ${product?.name ?? ""}`}
      footer={
        <>
          <Button variant="ghost" onClick={() => { setValues({}); onSave(null); }}>
            Use automatic FIFO
          </Button>
          <Button
            variant="primary"
            disabled={total <= 0}
            onClick={() => {
              onSave(Object.entries(values).filter(([, v]) => Number(v) > 0).map(([batchId, v]) => ({ batchId, quantity: Number(v) })));
              setValues({});
            }}
          >
            Use {qty(total)} {product?.unitSymbol} from these batches
          </Button>
        </>
      }
    >
      <p className="mb-3 text-[13px] text-slate-600">
        Batches are listed by expiry. Expired batches and stock received after {date(billDate)} cannot be sold.
      </p>
      <table className="table-dense w-full">
        <thead>
          <tr>
            <th>Batch</th>
            <th>Received</th>
            <th>Expiry</th>
            <th className="num">Cost</th>
            <th className="num">Available</th>
            <th className="num w-[110px]">Take</th>
          </tr>
        </thead>
        <tbody>
          {batches.map((b) => (
            <tr key={b.id}>
              <td className="font-medium">{b.batchNumber}</td>
              <td>{date(b.receivedOn)}</td>
              <td>{b.expiryDate ? date(b.expiryDate) : "—"}</td>
              <td className="num">{money(b.unitCost)}</td>
              <td className="num">{qty(b.remainingQuantity)}</td>
              <td>
                <NumberInput
                  value={values[b.id] ?? ""}
                  min={0}
                  max={b.remainingQuantity}
                  onChange={(v) => setValues({ ...values, [b.id]: v === "" ? "" : Math.min(Number(v), b.remainingQuantity) })}
                />
              </td>
            </tr>
          ))}
          {batches.length === 0 && (
            <tr>
              <td colSpan={6} className="py-6 text-center text-muted">
                No sellable batches for this product.
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </Modal>
  );
}

// =================================================================== sale detail (printable invoice)
export function SaleDetailPage() {
  const { id = "" } = useParams();
  const { can } = useAuth();
  const { data: bill, isLoading, error } = useApi<SaleBill>(`/sales/${id}`);
  const [voidOpen, setVoidOpen] = useState(false);
  const [returnOpen, setReturnOpen] = useState(false);
  const [showBatches, setShowBatches] = useState(false);
  const voidAction = useAction((reason: string) => api.post(`/sales/${id}/void`, { reason }), {
    success: "Sale voided; stock and balances reversed",
    onSuccess: () => setVoidOpen(false),
  });

  if (isLoading) return <Spinner />;
  if (error || !bill) return <ErrorNote error={error} />;
  const voided = bill.status === "Voided";
  const received = bill.settlements.filter((s) => s.status === "Posted").reduce((s, v) => s + v.amount, 0);

  return (
    <>
      <PageHeader
        title={
          <span className="flex items-center gap-2">
            {bill.number} <StatusBadge status={bill.status} />
          </span>
        }
        description={`Sale to ${bill.partyName} on ${date(bill.date)}`}
        actions={
          <>
            <Button icon={<Printer className="size-3.5" />} onClick={() => window.print()}>
              Print invoice
            </Button>
            {!voided && can("post") && (
              <Button icon={<RotateCcw className="size-3.5" />} onClick={() => setReturnOpen(true)}>
                Return items
              </Button>
            )}
            {!voided && can("void") && bill.returns.length === 0 && (
              <Button variant="danger" icon={<Ban className="size-3.5" />} onClick={() => setVoidOpen(true)}>
                Void
              </Button>
            )}
          </>
        }
      />

      {voided && (
        <div className="no-print mb-4 rounded-lg border border-owe/25 bg-owe-soft px-4 py-2.5 text-[13px] text-owe">
          Voided by {bill.voidedBy} on {dateTime(bill.voidedAt)}: {bill.voidReason}
        </div>
      )}

      <div className="grid gap-4 xl:grid-cols-[1fr_300px]">
        <article className="print-area rounded-xl border border-rule bg-surface p-8">
          <Letterhead title={voided ? "Sales invoice (VOID)" : "Sales invoice"} number={bill.number} />
          <div className="mb-6 grid grid-cols-2 gap-6 text-[13px]">
            <div>
              <div className="text-muted">Bill to</div>
              <div className="mt-1 font-medium">{bill.partyName}</div>
              <div className="whitespace-pre-line text-slate-600">{[bill.partyAddress, bill.partyPhone].filter(Boolean).join("\n")}</div>
            </div>
            <dl className="space-y-1 justify-self-end text-right">
              <div>
                <dt className="inline text-muted">Date </dt>
                <dd className="inline font-medium">{date(bill.date)}</dd>
              </div>
              {bill.customerReference && (
                <div>
                  <dt className="inline text-muted">Your ref. </dt>
                  <dd className="inline">{bill.customerReference}</dd>
                </div>
              )}
              <div>
                <dt className="inline text-muted">Customer code </dt>
                <dd className="inline">{bill.partyCode}</dd>
              </div>
            </dl>
          </div>

          <table className="table-dense w-full">
            <thead>
              <tr>
                <th className="w-8">#</th>
                <th>Item</th>
                <th className="num">Qty</th>
                <th className="num">Rate</th>
                <th className="num">Amount</th>
              </tr>
            </thead>
            <tbody>
              {bill.lines.map((l, i) => (
                <tr key={l.id}>
                  <td className="text-muted">{i + 1}</td>
                  <td>
                    {l.productName}
                    <div className="text-[11.5px] text-muted">{l.sku}</div>
                    {l.returnedQuantity > 0 && <div className="no-print text-[11.5px] text-warn">{qty(l.returnedQuantity)} returned</div>}
                    {showBatches && (
                      <div className="no-print mt-1 space-y-0.5 text-[11.5px] text-muted">
                        {l.allocations.map((a) => (
                          <div key={a.batchId}>
                            {qty(a.quantity)} from {a.batchNumber} at {money(a.unitCost)}
                            {a.expiryDate && `, expires ${date(a.expiryDate)}`}
                          </div>
                        ))}
                      </div>
                    )}
                  </td>
                  <td className="num">
                    {qty(l.quantity)} {l.unitSymbol}
                  </td>
                  <td className="num">{money(l.unitPrice)}</td>
                  <td className="num">{money(l.lineTotal)}</td>
                </tr>
              ))}
            </tbody>
          </table>

          <div className="mt-4 flex justify-end">
            <dl className="w-64 space-y-1.5 text-[13px]">
              <Row label="Total">
                <span className="text-[15px] font-semibold tabular-nums">Rs {money(bill.total)}</span>
              </Row>
              <Row label="Received">
                <span className="tabular-nums">{money(received)}</span>
              </Row>
              <Row label="Balance on this bill">
                <span className="tabular-nums">{money(bill.total - received)}</span>
              </Row>
            </dl>
          </div>
          {bill.notes && <p className="mt-6 text-[12.5px] text-slate-600">{bill.notes}</p>}
          <InvoiceFooter />
        </article>

        <div className="no-print space-y-4">
          <Card title="Profit on this bill">
            <dl className="space-y-1.5 text-[13px]">
              <Row label="Revenue">{money(bill.subtotal)}</Row>
              <Row label="FIFO cost">{money(bill.costOfGoods)}</Row>
              <Row label="Gross profit">
                <span className={bill.grossProfit < 0 ? "text-owe" : "text-accent-strong"}>{money(bill.grossProfit)}</span>
              </Row>
              {bill.chargesTotal > 0 && <Row label="Bill charges">{money(bill.chargesTotal)}</Row>}
            </dl>
            <button className="mt-3 text-[12.5px] text-accent-strong hover:underline" onClick={() => setShowBatches((s) => !s)}>
              {showBatches ? "Hide" : "Show"} batches used
            </button>
          </Card>
          {bill.charges.length > 0 && (
            <Card title="Charges">
              <ul className="space-y-1.5 text-[13px]">
                {bill.charges.map((c) => (
                  <li key={c.id} className="flex justify-between gap-2">
                    <span>
                      {c.expenseCategoryName}
                      <span className="block text-[12px] text-muted">{c.description ?? `Paid from ${c.paidFromAccountName}`}</span>
                    </span>
                    <span className="tabular-nums">{money(c.amount)}</span>
                  </li>
                ))}
              </ul>
            </Card>
          )}
          <LinkedVouchers title="Received at posting" vouchers={bill.settlements} empty="Nothing received when posted. Later receipts are recorded as vouchers against the customer." />
          {bill.returns.length > 0 && (
            <Card title="Returns">
              <ul className="space-y-1.5 text-[13px]">
                {bill.returns.map((r) => (
                  <li key={r.id} className="flex justify-between">
                    <Link className="hover:text-accent-strong" to={`/sales/returns/${r.id}`}>
                      {r.number}, {date(r.date)}
                    </Link>
                    <span className="tabular-nums">{money(r.total)}</span>
                  </li>
                ))}
              </ul>
            </Card>
          )}
          <Card title="Audit">
            <dl className="space-y-1.5 text-[13px]">
              <Row label="Posted by">{bill.createdBy}</Row>
              <Row label="Posted at">{dateTime(bill.createdAt)}</Row>
            </dl>
          </Card>
        </div>
      </div>

      <VoidDialog
        open={voidOpen}
        onClose={() => setVoidOpen(false)}
        number={bill.number}
        loading={voidAction.isPending}
        onConfirm={(reason) => voidAction.mutate(reason)}
        consequence="Stock goes back to the batches it came from, the customer's balance is reversed, and any cash received or charges paid at posting are reversed too."
      />
      <ReturnDialog
        kind="sale"
        open={returnOpen}
        onClose={() => setReturnOpen(false)}
        billId={bill.id}
        billNumber={bill.number}
        billDate={bill.date}
        lines={bill.lines.map((l) => ({
          id: l.id,
          productName: l.productName,
          unitSymbol: l.unitSymbol,
          returnable: l.quantity - l.returnedQuantity,
          rate: l.unitPrice,
        }))}
      />
    </>
  );
}

export function SaleReturnDetailPage() {
  const { id = "" } = useParams();
  return <ReturnDetail kind="sale" id={id} />;
}
