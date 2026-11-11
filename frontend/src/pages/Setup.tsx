import { useEffect, useState, type ReactNode } from "react";
import { useNavigate } from "react-router";
import { CheckCircle2, Plus, Trash2 } from "lucide-react";
import { api } from "../lib/api";
import { useAccounts, useAction, useApi } from "../lib/hooks";
import { useAuth } from "../lib/auth";
import { cx, date, money, qty } from "../lib/format";
import type { Batch, Paged, Party, Product, SetupStatus } from "../lib/types";
import { Button, Card, ErrorNote, Field, Input, Modal, NumberInput, PageHeader, Spinner, Table, Textarea } from "../components/ui";
import { PartyPicker, ProductPicker } from "../components/pickers";
import { AccountForm } from "./Accounts";
import { PartyForm } from "./Parties";

export default function SetupPage() {
  const { can } = useAuth();
  const { data: status, isLoading, error } = useApi<SetupStatus>("/setup/status");
  if (isLoading) return <Spinner />;
  if (error || !status) return <ErrorNote error={error} />;
  const locked = status.company.isOperational;
  const editable = !locked && can("setup");

  return (
    <>
      <PageHeader
        title="Day-0 setup"
        description={
          locked
            ? `Completed by ${status.company.setupCompletedBy} on ${date(status.company.setupCompletedAt)}. Opening balances are locked.`
            : `Enter what you already have on the day before go-live (${date(status.openingDate)}). Posting opens once you finish.`
        }
      />
      {!can("setup") && !locked && <ErrorNote error={{ message: "Only an Owner or Admin can complete the Day-0 setup." }} />}
      <div className="space-y-4">
        <Step n={1} title="Company and go-live date" done={!!status.company.companyName && status.company.companyName !== "My Company"}>
          <CompanyStep status={status} editable={editable} />
        </Step>
        <Step n={2} title="Cash and bank balances" done={status.accountsWithOpening > 0} summary={`Rs ${money(status.openingCashBank)} across ${status.accountsWithOpening} account(s)`}>
          <AccountsStep editable={editable} />
        </Step>
        <Step
          n={3}
          title="What customers owe and what you owe suppliers"
          done={status.partiesWithOpening > 0}
          summary={`Receivable Rs ${money(status.openingReceivable)}, payable Rs ${money(status.openingPayable)}`}
        >
          <PartiesStep editable={editable} />
        </Step>
        <Step n={4} title="Stock on hand" done={status.openingBatches > 0} summary={`${status.openingBatches} batch(es) worth Rs ${money(status.openingStockValue)}`}>
          <StockStep editable={editable} openingDate={status.openingDate} />
        </Step>
        {!locked && <FinishStep status={status} editable={editable} />}
      </div>
    </>
  );
}

function Step({ n, title, done, summary, children }: { n: number; title: string; done?: boolean; summary?: string; children: ReactNode }) {
  return (
    <section className="rounded-xl border border-rule bg-surface">
      <header className="flex items-center gap-3 border-b border-rule px-4 py-3">
        <span
          className={cx(
            "flex size-6 items-center justify-center rounded-full text-[12px] font-semibold",
            done ? "bg-accent text-on-accent" : "border border-rule-strong text-muted",
          )}
        >
          {done ? <CheckCircle2 className="size-4" /> : n}
        </span>
        <h2 className="flex-1 text-[14px] font-semibold">{title}</h2>
        {summary && <span className="text-[12.5px] tabular-nums text-muted">{summary}</span>}
      </header>
      <div className="p-4">{children}</div>
    </section>
  );
}

function CompanyStep({ status, editable }: { status: SetupStatus; editable: boolean }) {
  const c = status.company;
  const [form, setForm] = useState({ companyName: c.companyName, address: c.address ?? "", phone: c.phone ?? "", email: c.email ?? "", taxNumber: c.taxNumber ?? "", invoiceFooter: c.invoiceFooter ?? "", goLiveDate: c.goLiveDate });
  const set = (p: Partial<typeof form>) => setForm((f) => ({ ...f, ...p }));
  const save = useAction(() => api.put("/setup/company", form), { success: "Company details saved" });
  return (
    <div className="grid gap-3 md:grid-cols-3">
      <Field label="Company name" className="md:col-span-2">
        <Input value={form.companyName} disabled={!editable} onChange={(e) => set({ companyName: e.target.value })} />
      </Field>
      <Field label="Go-live date" hint={`Opening balances are dated ${date(status.openingDate)}`}>
        <Input type="date" value={form.goLiveDate} disabled={!editable} onChange={(e) => set({ goLiveDate: e.target.value })} />
      </Field>
      <Field label="Phone">
        <Input value={form.phone} disabled={!editable} onChange={(e) => set({ phone: e.target.value })} />
      </Field>
      <Field label="Email">
        <Input value={form.email} disabled={!editable} onChange={(e) => set({ email: e.target.value })} />
      </Field>
      <Field label="NTN / STRN">
        <Input value={form.taxNumber} disabled={!editable} onChange={(e) => set({ taxNumber: e.target.value })} />
      </Field>
      <Field label="Address" className="md:col-span-2">
        <Textarea value={form.address} disabled={!editable} rows={2} onChange={(e) => set({ address: e.target.value })} />
      </Field>
      <Field label="Invoice footer">
        <Textarea value={form.invoiceFooter} disabled={!editable} rows={2} onChange={(e) => set({ invoiceFooter: e.target.value })} placeholder="e.g. Goods once sold are returnable within 7 days" />
      </Field>
      {editable && (
        <div className="md:col-span-3">
          <Button variant="primary" loading={save.isPending} onClick={() => save.mutate(undefined)}>
            Save company details
          </Button>
        </div>
      )}
    </div>
  );
}

function AccountsStep({ editable }: { editable: boolean }) {
  const { data } = useAccounts(false);
  const [adding, setAdding] = useState(false);
  return (
    <>
      {data && data.length > 0 ? (
        <Table stack>
          <thead>
            <tr>
              <th>Account</th>
              <th>Type</th>
              <th className="num w-[220px]">Balance on opening day (Rs)</th>
              <th className="w-[90px]" />
            </tr>
          </thead>
          <tbody>
            {data.map((a) => (
              <OpeningRow key={a.id} label={a.name} sub={a.type} current={a.openingBalance} editable={editable} path={`/setup/accounts/${a.id}/opening-balance`} allowNegative={a.allowNegative} />
            ))}
          </tbody>
        </Table>
      ) : (
        <p className="text-[13px] text-muted">Add the cash drawer and every bank account you use.</p>
      )}
      {editable && (
        <Button className="mt-3" size="sm" icon={<Plus className="size-3.5" />} onClick={() => setAdding(true)}>
          Add account
        </Button>
      )}
      <AccountForm account={adding ? "new" : null} onClose={() => setAdding(false)} />
    </>
  );
}

function OpeningRow({ label, sub, current, editable, path, allowNegative = true }: { label: ReactNode; sub?: string; current: number; editable: boolean; path: string; allowNegative?: boolean }) {
  const [value, setValue] = useState<number | "">(current || "");
  useEffect(() => setValue(current || ""), [current]);
  const save = useAction(() => api.put(path, { amount: Number(value || 0) }), { success: "Opening balance saved" });
  const dirty = Number(value || 0) !== current;
  return (
    <tr>
      <td className="font-medium">{label}</td>
      <td className="text-muted">{sub}</td>
      <td data-label="Opening balance (Rs)">
        <NumberInput value={value} disabled={!editable} onChange={setValue} min={allowNegative ? undefined : 0} />
      </td>
      <td className="text-right">
        {editable && dirty && (
          <Button size="sm" variant="primary" loading={save.isPending} onClick={() => save.mutate(undefined)}>
            Save
          </Button>
        )}
      </td>
    </tr>
  );
}

function PartiesStep({ editable }: { editable: boolean }) {
  const { data } = useApi<Paged<Party>>("/parties", { pageSize: 200 });
  const [extra, setExtra] = useState<Party[]>([]);
  const [adding, setAdding] = useState(false);
  const withOpening = (data?.items ?? []).filter((p) => p.openingBalance !== 0 || extra.some((x) => x.id === p.id));
  const rows = [...withOpening, ...extra.filter((x) => !withOpening.some((p) => p.id === x.id))];

  return (
    <>
      <p className="mb-3 text-[13px] text-ink-soft">
        Positive = the party owes you (receivable). Negative = you owe them or hold their advance (payable).
      </p>
      {rows.length > 0 && (
        <Table stack className="mb-3">
          <thead>
            <tr>
              <th>Party</th>
              <th>Type</th>
              <th className="num w-[220px]">Opening balance (Rs)</th>
              <th className="w-[90px]" />
            </tr>
          </thead>
          <tbody>
            {rows.map((p) => (
              <OpeningRow key={p.id} label={p.name} sub={p.kind} current={p.openingBalance} editable={editable} path={`/setup/parties/${p.id}/opening-balance`} />
            ))}
          </tbody>
        </Table>
      )}
      {editable && (
        <div className="flex items-center gap-2">
          <div className="min-w-0 flex-1 sm:w-[320px] sm:flex-none">
            <PartyPicker value={null} onSelect={(p) => setExtra((x) => [...x, p])} placeholder="Find a party to add a balance" />
          </div>
          <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setAdding(true)}>
            New party
          </Button>
        </div>
      )}
      <PartyForm party={adding ? "new" : null} onClose={() => setAdding(false)} />
    </>
  );
}

function StockStep({ editable, openingDate }: { editable: boolean; openingDate: string }) {
  const { data } = useApi<Paged<Batch>>("/inventory/batches", { pageSize: 200, includeDepleted: true });
  const opening = (data?.items ?? []).filter((b) => b.source === "Opening");
  const [product, setProduct] = useState<Product | null>(null);
  const [form, setForm] = useState({ quantity: "" as number | "", unitCost: "" as number | "", batchNumber: "", expiryDate: "" });
  const add = useAction(
    () =>
      api.post("/setup/opening-stock", {
        productId: product!.id,
        quantity: Number(form.quantity),
        unitCost: Number(form.unitCost || 0),
        batchNumber: form.batchNumber || null,
        expiryDate: form.expiryDate || null,
      }),
    {
      success: "Opening stock added",
      onSuccess: () => {
        setProduct(null);
        setForm({ quantity: "", unitCost: "", batchNumber: "", expiryDate: "" });
      },
    },
  );
  const remove = useAction((id: string) => api.del(`/setup/opening-stock/${id}`), { success: "Opening batch removed" });

  return (
    <>
      <p className="mb-3 text-[13px] text-ink-soft">
        Count what is on the shelf and enter it at what it cost you. Each line becomes a batch received on {date(openingDate)}; add several lines for the same product if parts were bought at different costs or expire on different dates.
      </p>
      {opening.length > 0 && (
        <Table className="mb-3">
          <thead>
            <tr>
              <th>Product</th>
              <th>Batch</th>
              <th>Expiry</th>
              <th className="num">Qty</th>
              <th className="num">Unit cost</th>
              <th className="num">Value</th>
              <th className="w-10" />
            </tr>
          </thead>
          <tbody>
            {opening.map((b) => (
              <tr key={b.id}>
                <td className="font-medium">{b.productName}</td>
                <td>{b.batchNumber}</td>
                <td>{b.expiryDate ? date(b.expiryDate) : "—"}</td>
                <td className="num">
                  {qty(b.originalQuantity)} {b.unitSymbol}
                </td>
                <td className="num">{money(b.unitCost)}</td>
                <td className="num">{money(b.originalQuantity * b.unitCost)}</td>
                <td>
                  {editable && (
                    <button aria-label="Remove batch" className="rounded-md p-1 text-muted hover:bg-owe-soft hover:text-owe" onClick={() => remove.mutate(b.id)}>
                      <Trash2 className="size-3.5" />
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
      {editable && (
        <div className="grid items-end gap-2 rounded-lg border border-dashed border-rule p-3 md:grid-cols-[1.6fr_100px_120px_130px_150px_auto]">
          <Field label="Product">
            <ProductPicker value={product} onSelect={setProduct} />
          </Field>
          <Field label="Quantity">
            <NumberInput value={form.quantity} onChange={(v) => setForm({ ...form, quantity: v })} min={0} />
          </Field>
          <Field label="Unit cost">
            <NumberInput value={form.unitCost} onChange={(v) => setForm({ ...form, unitCost: v })} min={0} />
          </Field>
          <Field label="Batch no.">
            <Input value={form.batchNumber} onChange={(e) => setForm({ ...form, batchNumber: e.target.value })} placeholder="Auto" />
          </Field>
          <Field label={product?.tracksExpiry ? "Expiry (required)" : "Expiry"}>
            <Input type="date" value={form.expiryDate} onChange={(e) => setForm({ ...form, expiryDate: e.target.value })} />
          </Field>
          <Button variant="primary" disabled={!product || !(Number(form.quantity) > 0)} loading={add.isPending} onClick={() => add.mutate(undefined)}>
            Add
          </Button>
        </div>
      )}
    </>
  );
}

function FinishStep({ status, editable }: { status: SetupStatus; editable: boolean }) {
  const navigate = useNavigate();
  const [confirm, setConfirm] = useState(false);
  const { data: accounts } = useAccounts();
  const complete = useAction(() => api.post("/setup/complete"), {
    success: "Setup complete. Posting is now open.",
    onSuccess: () => navigate("/"),
  });
  const ready = (accounts?.length ?? 0) > 0;

  return (
    <Card>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <div className="text-[14px] font-semibold">Go live on {date(status.company.goLiveDate)}</div>
          <p className="text-[13px] text-muted">
            {ready ? "Opening balances lock once you go live. Later corrections are made with documents." : "Add at least one cash or bank account first."}
          </p>
        </div>
        <Button variant="primary" disabled={!editable || !ready} onClick={() => setConfirm(true)}>
          Finish setup and go live
        </Button>
      </div>
      <Modal
        open={confirm}
        onClose={() => setConfirm(false)}
        title="Lock opening balances?"
        footer={
          <>
            <Button variant="ghost" onClick={() => setConfirm(false)}>
              Keep editing
            </Button>
            <Button variant="primary" loading={complete.isPending} onClick={() => complete.mutate(undefined)}>
              Go live
            </Button>
          </>
        }
      >
        <dl className="space-y-1.5 text-[13px]">
          <SummaryRow label="Cash and bank" value={status.openingCashBank} />
          <SummaryRow label="Receivable from customers" value={status.openingReceivable} />
          <SummaryRow label="Payable to suppliers" value={status.openingPayable} />
          <SummaryRow label="Stock at cost" value={status.openingStockValue} />
        </dl>
        <p className="mt-4 text-[13px] text-ink-soft">After this, opening balances can't change and documents can be posted from {date(status.company.goLiveDate)} onwards.</p>
      </Modal>
    </Card>
  );
}

function SummaryRow({ label, value }: { label: string; value: number }) {
  return (
    <div className="flex justify-between">
      <dt className="text-muted">{label}</dt>
      <dd className="tabular-nums">Rs {money(value)}</dd>
    </div>
  );
}

