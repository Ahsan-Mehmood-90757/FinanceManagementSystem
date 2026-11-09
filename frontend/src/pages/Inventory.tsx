import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router";
import { api } from "../lib/api";
import { useAction, useApi, useDebounced } from "../lib/hooks";
import { useAuth } from "../lib/auth";
import { cx, date, money, qty, today } from "../lib/format";
import type { Batch, Paged, StockAdjustment } from "../lib/types";
import { Button, Checkbox, EmptyState, ErrorNote, Field, Input, Modal, NumberInput, PageHeader, Pagination, SearchInput, Select, Spinner, Tabs } from "../components/ui";
import { ExpiryCell } from "./Products";

type Tab = "batches" | "writeoffs";

export default function InventoryPage() {
  const { can } = useAuth();
  const [params, setParams] = useSearchParams();
  const expiring = params.get("expiring");
  const [tab, setTab] = useState<Tab>("batches");
  const [search, setSearch] = useState("");
  const [includeDepleted, setIncludeDepleted] = useState(false);
  const [page, setPage] = useState(1);
  const [writeOff, setWriteOff] = useState<Batch | null>(null);
  const debounced = useDebounced(search);
  useEffect(() => setPage(1), [debounced, includeDepleted, expiring, tab]);

  const batches = useApi<Paged<Batch>>(
    tab === "batches" ? "/inventory/batches" : null,
    { search: debounced, includeDepleted, expiringWithinDays: expiring ?? undefined, page, pageSize: 30 },
    { keepPrevious: true },
  );
  const adjustments = useApi<Paged<StockAdjustment>>(tab === "writeoffs" ? "/inventory/adjustments" : null, { search: debounced, page, pageSize: 30 }, { keepPrevious: true });

  return (
    <>
      <PageHeader title="Stock batches" description="Every receipt of stock is a batch with its own cost and expiry. Sales drain the oldest batch first." />
      <div className="mb-3 flex flex-wrap items-center gap-3">
        <Tabs<Tab>
          value={tab}
          onChange={setTab}
          tabs={[
            { value: "batches", label: "Batches" },
            { value: "writeoffs", label: "Write-offs" },
          ]}
        />
        <SearchInput value={search} onChange={setSearch} placeholder={tab === "batches" ? "Product, SKU or batch" : "Number or product"} />
        {tab === "batches" && (
          <>
            <Select value={expiring ?? ""} onChange={(e) => setParams(e.target.value ? { expiring: e.target.value } : {})} className="w-[200px]" aria-label="Expiry filter">
              <option value="">Any expiry</option>
              <option value="0">Already expired</option>
              <option value="30">Expiring within 30 days</option>
              <option value="90">Expiring within 90 days</option>
            </Select>
            <Checkbox label="Include depleted" checked={includeDepleted} onChange={setIncludeDepleted} />
          </>
        )}
      </div>

      <div className="overflow-hidden rounded-xl border border-rule bg-surface">
        {tab === "batches" ? (
          <>
            <ErrorNote error={batches.error} />
            {batches.isLoading ? (
              <Spinner />
            ) : batches.data && batches.data.items.length ? (
              <table className="table-dense w-full">
                <thead>
                  <tr>
                    <th>Product</th>
                    <th>Batch</th>
                    <th>Received</th>
                    <th>Expiry</th>
                    <th className="num">Unit cost</th>
                    <th className="num">Remaining</th>
                    <th className="num">Value</th>
                    <th className="w-[90px]" />
                  </tr>
                </thead>
                <tbody>
                  {batches.data.items.map((b) => (
                    <tr key={b.id} className={cx(b.remainingQuantity === 0 && "text-muted")}>
                      <td>
                        <Link to={`/products/${b.productId}`} className="font-medium hover:text-accent-strong">
                          {b.productName}
                        </Link>
                        <div className="text-[11.5px] text-muted">{b.sku}</div>
                      </td>
                      <td>{b.batchNumber}</td>
                      <td>{date(b.receivedOn)}</td>
                      <td>
                        <ExpiryCell date={b.expiryDate} />
                      </td>
                      <td className="num">{money(b.unitCost)}</td>
                      <td className="num">
                        {qty(b.remainingQuantity)} {b.unitSymbol}
                      </td>
                      <td className="num">{money(b.remainingValue)}</td>
                      <td className="text-right">
                        {can("post") && b.remainingQuantity > 0 && (
                          <Button size="sm" variant="ghost" onClick={() => setWriteOff(b)}>
                            Write off
                          </Button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <EmptyState title="No batches match">Stock arrives through purchase bills or Day-0 opening stock.</EmptyState>
            )}
            {batches.data && <Pagination page={batches.data.page} pageSize={batches.data.pageSize} total={batches.data.total} onPage={setPage} />}
          </>
        ) : (
          <>
            <ErrorNote error={adjustments.error} />
            {adjustments.isLoading ? (
              <Spinner />
            ) : adjustments.data && adjustments.data.items.length ? (
              <table className="table-dense w-full">
                <thead>
                  <tr>
                    <th>Number</th>
                    <th>Date</th>
                    <th>Product</th>
                    <th>Batch</th>
                    <th>Reason</th>
                    <th className="num">Qty</th>
                    <th className="num">Value written off</th>
                  </tr>
                </thead>
                <tbody>
                  {adjustments.data.items.map((a) => (
                    <tr key={a.id}>
                      <td className="font-medium">{a.number}</td>
                      <td>{date(a.date)}</td>
                      <td>{a.productName}</td>
                      <td>{a.batchNumber}</td>
                      <td className="text-muted">{a.reason}</td>
                      <td className="num">{qty(a.quantity)}</td>
                      <td className="num text-owe">{money(a.value)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <EmptyState title="No write-offs">Expired, damaged or missing stock written off from a batch shows here.</EmptyState>
            )}
            {adjustments.data && <Pagination page={adjustments.data.page} pageSize={adjustments.data.pageSize} total={adjustments.data.total} onPage={setPage} />}
          </>
        )}
      </div>
      <WriteOffDialog batch={writeOff} onClose={() => setWriteOff(null)} />
    </>
  );
}

function WriteOffDialog({ batch, onClose }: { batch: Batch | null; onClose: () => void }) {
  const [quantity, setQuantity] = useState<number | "">("");
  const [reason, setReason] = useState("");
  const [d, setD] = useState(today());
  useEffect(() => {
    if (batch) {
      setQuantity(batch.remainingQuantity);
      setReason("");
      setD(today());
    }
  }, [batch]);
  const action = useAction(() => api.post("/inventory/adjustments", { date: d, batchId: batch!.id, quantity: Number(quantity), reason }), {
    success: "Stock written off",
    onSuccess: onClose,
  });
  const value = batch ? Number(quantity || 0) * batch.unitCost : 0;

  return (
    <Modal
      open={!!batch}
      onClose={onClose}
      title={`Write off from ${batch?.batchNumber ?? ""}`}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="danger" disabled={!reason.trim() || !(Number(quantity) > 0)} loading={action.isPending} onClick={() => action.mutate(undefined)}>
            Write off Rs {money(value)}
          </Button>
        </>
      }
    >
      <p className="mb-3 text-[13px] text-slate-600">
        Removes stock of <span className="font-medium text-ink">{batch?.productName}</span> at its batch cost. The value is charged to net profit as a loss.
      </p>
      <div className="grid grid-cols-2 gap-3">
        <Field label={`Quantity (max ${qty(batch?.remainingQuantity)})`}>
          <NumberInput value={quantity} onChange={setQuantity} min={0} max={batch?.remainingQuantity} />
        </Field>
        <Field label="Date">
          <Input type="date" value={d} onChange={(e) => setD(e.target.value)} />
        </Field>
        <Field label="Reason" className="col-span-2">
          <Input autoFocus value={reason} onChange={(e) => setReason(e.target.value)} placeholder="e.g. Expired on shelf" />
        </Field>
      </div>
    </Modal>
  );
}
