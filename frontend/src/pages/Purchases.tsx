import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { Ban, Plus, Printer, RotateCcw, Trash2 } from "lucide-react";
import { api } from "../lib/api";
import { useAction, useApi } from "../lib/hooks";
import { useAuth } from "../lib/auth";
import { date, dateTime, money, qty, today } from "../lib/format";
import type { Party, Product, PurchaseBill } from "../lib/types";
import { Button, Card, ErrorNote, Field, Input, NumberInput, PageHeader, Spinner, StatusBadge, VoidDialog } from "../components/ui";
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

export const PurchasesListPage = () => <DocumentList kind="purchase" />;

interface PurchaseLineDraft {
  key: number;
  product: Product | null;
  quantity: number | "";
  unitCost: number | "";
  batchNumber: string;
  expiryDate: string;
}
const blankLine = (): PurchaseLineDraft => ({ key: Date.now() + Math.random(), product: null, quantity: "", unitCost: "", batchNumber: "", expiryDate: "" });

export function NewPurchasePage() {
  const navigate = useNavigate();
  const [party, setParty] = useState<Party | null>(null);
  const [billDate, setBillDate] = useState(today());
  const [reference, setReference] = useState("");
  const [notes, setNotes] = useState("");
  const [lines, setLines] = useState<PurchaseLineDraft[]>([blankLine()]);
  const [charges, setCharges] = useState<ChargeDraft[]>([]);
  const [payAccount, setPayAccount] = useState("");
  const [paid, setPaid] = useState<number | "">("");

  const update = (key: number, patch: Partial<PurchaseLineDraft>) => setLines((ls) => ls.map((l) => (l.key === key ? { ...l, ...patch } : l)));
  const goods = lines.reduce((s, l) => s + Number(l.quantity || 0) * Number(l.unitCost || 0), 0);
  const onSupplierBill = charges.filter((c) => !c.paidFromAccountId).reduce((s, c) => s + Number(c.amount || 0), 0);
  const paidSeparately = charges.filter((c) => c.paidFromAccountId).reduce((s, c) => s + Number(c.amount || 0), 0);
  const total = goods + onSupplierBill;
  const paidNum = Number(paid || 0);
  const ready = party && lines.some((l) => l.product && Number(l.quantity) > 0);

  const post = useAction(
    () =>
      api.post<{ id: string }>("/purchases", {
        date: billDate,
        partyId: party?.id,
        supplierReference: reference || null,
        notes: notes || null,
        lines: lines
          .filter((l) => l.product && Number(l.quantity) > 0)
          .map((l) => ({
            productId: l.product!.id,
            quantity: Number(l.quantity),
            unitCost: Number(l.unitCost || 0),
            batchNumber: l.batchNumber || null,
            expiryDate: l.expiryDate || null,
          })),
        charges: chargesToRequest(charges),
        payment: payAccount && paidNum > 0 ? { accountId: payAccount, amount: paidNum, reference: null } : null,
      }),
    { success: "Purchase posted; stock received", onSuccess: (r) => navigate(`/purchases/${r.id}`) },
  );

  return (
    <>
      <PageHeader title="New purchase" description="Each line becomes a stock batch at the cost you enter." />
      <div className="grid gap-4 xl:grid-cols-[1fr_300px]">
        <div className="space-y-4">
          <Card>
            <div className="grid gap-3 md:grid-cols-[1.4fr_150px_1fr]">
              <Field label="Supplier">
                <PartyPicker kind="Supplier" value={party} onSelect={setParty} placeholder="Search supplier" autoFocus />
              </Field>
              <Field label="Date received">
                <Input type="date" value={billDate} onChange={(e) => setBillDate(e.target.value)} />
              </Field>
              <Field label="Supplier invoice no.">
                <Input value={reference} onChange={(e) => setReference(e.target.value)} placeholder="Optional" />
              </Field>
            </div>
          </Card>

          <Card title="Items received" bodyClassName="p-0">
            <table className="table-dense w-full">
              <thead>
                <tr>
                  <th>Product</th>
                  <th className="num w-[100px]">Qty</th>
                  <th className="num w-[120px]">Unit cost</th>
                  <th className="w-[130px]">Batch no.</th>
                  <th className="w-[150px]">Expiry</th>
                  <th className="num w-[120px]">Amount</th>
                  <th className="w-8" />
                </tr>
              </thead>
              <tbody>
                {lines.map((l, i) => (
                  <tr key={l.key}>
                    <td>
                      <ProductPicker
                        value={l.product}
                        autoFocus={i === lines.length - 1 && i > 0}
                        onSelect={(p) => update(l.key, { product: p, quantity: l.quantity || 1 })}
                      />
                      {l.product && (
                        <div className="mt-1 text-[11.5px] text-muted">
                          {qty(l.product.stockOnHand)} {l.product.unitSymbol} in stock
                          {l.product.tracksExpiry && ", expiry required"}
                        </div>
                      )}
                    </td>
                    <td>
                      <NumberInput value={l.quantity} onChange={(v) => update(l.key, { quantity: v })} min={0} />
                    </td>
                    <td>
                      <NumberInput value={l.unitCost} onChange={(v) => update(l.key, { unitCost: v })} min={0} />
                    </td>
                    <td>
                      <Input value={l.batchNumber} onChange={(e) => update(l.key, { batchNumber: e.target.value })} placeholder="Auto" />
                    </td>
                    <td>
                      <Input type="date" value={l.expiryDate} onChange={(e) => update(l.key, { expiryDate: e.target.value })} required={l.product?.tracksExpiry} />
                    </td>
                    <td className="num">{money(Number(l.quantity || 0) * Number(l.unitCost || 0))}</td>
                    <td>
                      {lines.length > 1 && (
                        <button onClick={() => setLines(lines.filter((x) => x.key !== l.key))} aria-label="Remove line" className="rounded p-1 text-muted hover:bg-owe-soft hover:text-owe">
                          <Trash2 className="size-3.5" />
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="border-t border-rule px-3 py-2">
              <Button size="sm" variant="ghost" icon={<Plus className="size-3.5" />} onClick={() => setLines([...lines, blankLine()])}>
                Add line
              </Button>
            </div>
          </Card>

          <ChargesEditor charges={charges} setCharges={setCharges} allowOnParty />
          <Card>
            <NotesField value={notes} onChange={setNotes} />
          </Card>
        </div>

        <aside className="space-y-4 xl:sticky xl:top-6 xl:self-start">
          <Card title="Summary">
            <dl className="space-y-1.5 text-[13px]">
              <Row label="Goods">{money(goods)}</Row>
              {onSupplierBill > 0 && <Row label="Charges on supplier bill">{money(onSupplierBill)}</Row>}
              <Row label="Supplier bill total">
                <span className="font-semibold tabular-nums">{money(total)}</span>
              </Row>
              {paidSeparately > 0 && <Row label="Charges paid by us">{money(paidSeparately)}</Row>}
            </dl>
            <div className="my-3 border-t border-rule" />
            <Field label="Paid now from">
              <AccountSelect value={payAccount} onChange={setPayAccount} allowEmpty emptyLabel="Nothing paid (on credit)" />
            </Field>
            <div className="mt-2 flex gap-2">
              <NumberInput value={paid} onChange={setPaid} disabled={!payAccount} placeholder="0.00" min={0} />
              <Button variant="ghost" disabled={!payAccount} onClick={() => setPaid(Math.round(total * 100) / 100)}>
                Full
              </Button>
            </div>
            <dl className="mt-3 text-[13px]">
              <Row label="Added to what we owe">
                <span className="font-semibold tabular-nums text-owe">{money(total - paidNum)}</span>
              </Row>
            </dl>
            <Button variant="primary" className="mt-4 h-9 w-full" disabled={!ready} loading={post.isPending} onClick={() => post.mutate(undefined)}>
              Post purchase of Rs {money(total)}
            </Button>
            <p className="mt-2 text-[12px] text-muted">Posted bills can't be edited. Use a purchase return or a void to correct them.</p>
          </Card>
        </aside>
      </div>
    </>
  );
}

export function PurchaseDetailPage() {
  const { id = "" } = useParams();
  const { can } = useAuth();
  const { data: bill, isLoading, error } = useApi<PurchaseBill>(`/purchases/${id}`);
  const [voidOpen, setVoidOpen] = useState(false);
  const [returnOpen, setReturnOpen] = useState(false);
  const voidAction = useAction((reason: string) => api.post(`/purchases/${id}/void`, { reason }), {
    success: "Purchase voided; stock and balances reversed",
    onSuccess: () => setVoidOpen(false),
  });

  if (isLoading) return <Spinner />;
  if (error || !bill) return <ErrorNote error={error} />;
  const voided = bill.status === "Voided";
  const paid = bill.settlements.filter((s) => s.status === "Posted").reduce((s, v) => s + v.amount, 0);
  const stockTouched = bill.lines.some((l) => l.batchRemaining !== l.quantity);

  return (
    <>
      <PageHeader
        title={
          <span className="flex items-center gap-2">
            {bill.number} <StatusBadge status={bill.status} />
          </span>
        }
        description={`Purchase from ${bill.partyName} on ${date(bill.date)}`}
        actions={
          <>
            <Button icon={<Printer className="size-3.5" />} onClick={() => window.print()}>
              Print
            </Button>
            {!voided && can("post") && (
              <Button icon={<RotateCcw className="size-3.5" />} onClick={() => setReturnOpen(true)}>
                Return to supplier
              </Button>
            )}
            {!voided && can("void") && (
              <Button
                variant="danger"
                icon={<Ban className="size-3.5" />}
                disabled={stockTouched || bill.returns.length > 0}
                title={stockTouched ? "Some of this stock has been sold or written off; use a return instead" : undefined}
                onClick={() => setVoidOpen(true)}
              >
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
          <Letterhead title={voided ? "Purchase bill (VOID)" : "Purchase bill"} number={bill.number} />
          <div className="mb-6 grid grid-cols-2 gap-6 text-[13px]">
            <div>
              <div className="text-muted">Supplier</div>
              <div className="mt-1 font-medium">{bill.partyName}</div>
              <div className="text-slate-600">{bill.partyCode}</div>
            </div>
            <dl className="space-y-1 justify-self-end text-right">
              <div>
                <dt className="inline text-muted">Received </dt>
                <dd className="inline font-medium">{date(bill.date)}</dd>
              </div>
              {bill.supplierReference && (
                <div>
                  <dt className="inline text-muted">Supplier invoice </dt>
                  <dd className="inline">{bill.supplierReference}</dd>
                </div>
              )}
            </dl>
          </div>
          <table className="table-dense w-full">
            <thead>
              <tr>
                <th>Item</th>
                <th>Batch</th>
                <th className="num">Qty</th>
                <th className="num">Unit cost</th>
                <th className="num">Amount</th>
              </tr>
            </thead>
            <tbody>
              {bill.lines.map((l) => (
                <tr key={l.id}>
                  <td>
                    <Link to={`/products/${l.productId}`} className="hover:text-accent-strong">
                      {l.productName}
                    </Link>
                    <div className="text-[11.5px] text-muted">{l.sku}</div>
                  </td>
                  <td>
                    {l.batchNumber}
                    {l.expiryDate && <div className="text-[11.5px] text-muted">Expires {date(l.expiryDate)}</div>}
                    <div className="no-print text-[11.5px] text-muted">{qty(l.batchRemaining)} left</div>
                  </td>
                  <td className="num">
                    {qty(l.quantity)} {l.unitSymbol}
                    {l.returnedQuantity > 0 && <div className="no-print text-[11.5px] text-warn">{qty(l.returnedQuantity)} returned</div>}
                  </td>
                  <td className="num">{money(l.unitCost)}</td>
                  <td className="num">{money(l.lineTotal)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="mt-4 flex justify-end">
            <dl className="w-64 space-y-1.5 text-[13px]">
              <Row label="Goods">{money(bill.subtotal)}</Row>
              {bill.chargesOnParty > 0 && <Row label="Supplier charges">{money(bill.chargesOnParty)}</Row>}
              <Row label="Total">
                <span className="text-[15px] font-semibold tabular-nums">Rs {money(bill.total)}</span>
              </Row>
              <Row label="Paid at posting">{money(paid)}</Row>
            </dl>
          </div>
          {bill.notes && <p className="mt-6 text-[12.5px] text-slate-600">{bill.notes}</p>}
          <InvoiceFooter />
        </article>

        <div className="no-print space-y-4">
          {bill.charges.length > 0 && (
            <Card title="Operational charges">
              <ul className="space-y-1.5 text-[13px]">
                {bill.charges.map((c) => (
                  <li key={c.id} className="flex justify-between gap-2">
                    <span>
                      {c.expenseCategoryName}
                      <span className="block text-[12px] text-muted">
                        {c.paidFromAccountName ? `Paid from ${c.paidFromAccountName}` : "On supplier bill"}
                        {c.description && `, ${c.description}`}
                      </span>
                    </span>
                    <span className="tabular-nums">{money(c.amount)}</span>
                  </li>
                ))}
              </ul>
            </Card>
          )}
          <LinkedVouchers title="Paid at posting" vouchers={bill.settlements} empty="Nothing paid when posted. Later payments are recorded as vouchers against the supplier." />
          {bill.returns.length > 0 && (
            <Card title="Returns">
              <ul className="space-y-1.5 text-[13px]">
                {bill.returns.map((r) => (
                  <li key={r.id} className="flex justify-between">
                    <Link className="hover:text-accent-strong" to={`/purchases/returns/${r.id}`}>
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
        consequence="The received batches are removed from stock, the supplier balance is reversed, and any payment or charges made at posting are reversed."
      />
      <ReturnDialog
        kind="purchase"
        open={returnOpen}
        onClose={() => setReturnOpen(false)}
        billId={bill.id}
        billNumber={bill.number}
        billDate={bill.date}
        lines={bill.lines.map((l) => ({
          id: l.id,
          productName: l.productName,
          unitSymbol: l.unitSymbol,
          returnable: Math.min(l.quantity - l.returnedQuantity, l.batchRemaining),
          rate: l.unitCost,
          note: `Batch ${l.batchNumber}: ${qty(l.batchRemaining)} still in stock`,
        }))}
      />
    </>
  );
}

export function PurchaseReturnDetailPage() {
  const { id = "" } = useParams();
  return <ReturnDetail kind="purchase" id={id} />;
}
