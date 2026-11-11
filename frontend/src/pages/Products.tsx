import { useEffect, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router";
import { Pencil, Plus } from "lucide-react";
import { api } from "../lib/api";
import { useAction, useApi, useCategories, useDebounced, useUnits } from "../lib/hooks";
import { useAuth } from "../lib/auth";
import { cx, date, daysUntil, documentHref, documentLabel, money, qty } from "../lib/format";
import type { Batch, Movement, Paged, Product } from "../lib/types";
import {
  Badge,
  Button,
  Card,
  Checkbox,
  EmptyState,
  ErrorNote,
  Field,
  Input,
  Modal,
  NumberInput,
  PageHeader,
  Pagination,
  SearchInput,
  Select,
  Spinner,
  Table,
  Textarea,
} from "../components/ui";

export function ProductsPage() {
  const { can } = useAuth();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const lowStock = params.get("lowStock") === "1";
  const [search, setSearch] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<Product | "new" | null>(null);
  const debounced = useDebounced(search);
  const categories = useCategories();
  useEffect(() => setPage(1), [debounced, categoryId, lowStock]);

  const { data, isLoading, error } = useApi<Paged<Product>>(
    "/products",
    { search: debounced, categoryId, lowStockOnly: lowStock, page, pageSize: 30 },
    { keepPrevious: true },
  );

  return (
    <>
      <PageHeader
        title="Products"
        description="Stock on hand and value come from open FIFO batches."
        actions={
          can("master") && (
            <Button variant="primary" icon={<Plus className="size-3.5" />} onClick={() => setEditing("new")}>
              Add product
            </Button>
          )
        }
      />
      <div className="mb-3 flex flex-wrap items-center gap-3">
        <SearchInput value={search} onChange={setSearch} placeholder="Name or SKU" />
        <Select value={categoryId} onChange={(e) => setCategoryId(e.target.value)} className="w-full sm:w-[220px]" aria-label="Category">
          <option value="">All categories</option>
          {categories.data?.map((c) => (
            <option key={c.id} value={c.id}>
              {c.parentName ? `  ${c.parentName} / ${c.name}` : c.name}
            </option>
          ))}
        </Select>
        <Checkbox label="At or below reorder level" checked={lowStock} onChange={(v) => setParams(v ? { lowStock: "1" } : {})} />
      </div>

      <div className="overflow-hidden rounded-xl border border-rule bg-surface">
        <ErrorNote error={error} />
        {isLoading ? (
          <Spinner />
        ) : data && data.items.length ? (
          <Table>
            <thead>
              <tr>
                <th className="w-[120px]">SKU</th>
                <th>Name</th>
                <th>Category</th>
                <th className="num">Sale price</th>
                <th className="num">On hand</th>
                <th className="num">Stock value</th>
                <th className="w-10" />
              </tr>
            </thead>
            <tbody>
              {data.items.map((p) => {
                const low = p.isActive && p.reorderLevel > 0 && p.stockOnHand <= p.reorderLevel;
                return (
                  <tr key={p.id} className={cx("cursor-pointer", !p.isActive && "text-muted")} onClick={() => navigate(`/products/${p.id}`)}>
                    <td className="text-muted">{p.sku}</td>
                    <td className="font-medium">
                      {p.name} {!p.isActive && <Badge>Inactive</Badge>}
                    </td>
                    <td className="text-ink-soft">{p.categoryName ?? ""}</td>
                    <td className="num">{money(p.defaultSalePrice)}</td>
                    <td className={cx("num", low && "font-medium text-warn")}>
                      {qty(p.stockOnHand)} {p.unitSymbol}
                    </td>
                    <td className="num">{money(p.stockValue)}</td>
                    <td>
                      {can("master") && (
                        <button
                          aria-label={`Edit ${p.name}`}
                          className="rounded-md p-1 text-muted hover:bg-wash-strong hover:text-ink"
                          onClick={(e) => {
                            e.stopPropagation();
                            setEditing(p);
                          }}
                        >
                          <Pencil className="size-3.5" />
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </Table>
        ) : (
          <EmptyState title={lowStock ? "Nothing is below its reorder level" : "No products yet"} action={!lowStock && can("master") && <Button onClick={() => setEditing("new")}>Add product</Button>}>
            {lowStock ? "Products with a reorder level appear here when stock falls to it." : "Add products, then receive stock with a purchase or as Day-0 opening stock."}
          </EmptyState>
        )}
        {data && <Pagination page={data.page} pageSize={data.pageSize} total={data.total} onPage={setPage} />}
      </div>
      <ProductForm product={editing} onClose={() => setEditing(null)} />
    </>
  );
}

export function ProductForm({ product, onClose }: { product: Product | "new" | null; onClose: () => void }) {
  const isNew = product === "new";
  const p = product && product !== "new" ? product : null;
  const units = useUnits();
  const categories = useCategories();
  const empty = { sku: "", name: "", description: "", categoryId: "", unitId: "", defaultSalePrice: "" as number | "", reorderLevel: "" as number | "", tracksExpiry: false, isActive: true };
  const [form, setForm] = useState(empty);
  useEffect(() => {
    if (product)
      setForm(
        p
          ? {
              sku: p.sku,
              name: p.name,
              description: p.description ?? "",
              categoryId: p.categoryId ?? "",
              unitId: p.unitId,
              defaultSalePrice: p.defaultSalePrice,
              reorderLevel: p.reorderLevel,
              tracksExpiry: p.tracksExpiry,
              isActive: p.isActive,
            }
          : { ...empty, unitId: units.data?.[0]?.id ?? "" },
      );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [product, units.data]);
  const set = (patch: Partial<typeof form>) => setForm((f) => ({ ...f, ...patch }));

  const body = () => ({
    ...form,
    categoryId: form.categoryId || null,
    defaultSalePrice: Number(form.defaultSalePrice || 0),
    reorderLevel: Number(form.reorderLevel || 0),
  });
  const save = useAction(() => (isNew ? api.post("/products", body()) : api.put(`/products/${p!.id}`, body())), {
    success: isNew ? "Product added" : "Product updated",
    onSuccess: onClose,
  });

  return (
    <Modal
      open={!!product}
      onClose={onClose}
      title={isNew ? "Add product" : `Edit ${p?.name}`}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" disabled={!form.name.trim() || !form.unitId} loading={save.isPending} onClick={() => save.mutate(undefined)}>
            {isNew ? "Add product" : "Save changes"}
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-2 gap-3">
        <Field label="Name" className="col-span-2">
          <Input autoFocus value={form.name} onChange={(e) => set({ name: e.target.value })} />
        </Field>
        <Field label="SKU" hint={isNew ? "Leave blank to number automatically" : undefined}>
          <Input value={form.sku} onChange={(e) => set({ sku: e.target.value })} />
        </Field>
        <Field label="Unit of measure">
          <Select value={form.unitId} onChange={(e) => set({ unitId: e.target.value })}>
            {units.data?.map((u) => (
              <option key={u.id} value={u.id}>
                {u.name} ({u.symbol}){u.allowsFractions ? ", fractions allowed" : ""}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Category" className="col-span-2">
          <Select value={form.categoryId} onChange={(e) => set({ categoryId: e.target.value })}>
            <option value="">Uncategorised</option>
            {categories.data?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.parentName ? `${c.parentName} / ${c.name}` : c.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Default sale price (Rs)">
          <NumberInput value={form.defaultSalePrice} onChange={(v) => set({ defaultSalePrice: v })} min={0} />
        </Field>
        <Field label="Reorder level" hint="Flag the product when stock falls to this">
          <NumberInput value={form.reorderLevel} onChange={(v) => set({ reorderLevel: v })} min={0} />
        </Field>
        <Field label="Description" className="col-span-2">
          <Textarea value={form.description} onChange={(e) => set({ description: e.target.value })} rows={2} />
        </Field>
        <div className="col-span-2 flex flex-wrap gap-5">
          <Checkbox label="Track expiry dates per batch" checked={form.tracksExpiry} onChange={(v) => set({ tracksExpiry: v })} />
          {!isNew && <Checkbox label="Active" checked={form.isActive} onChange={(v) => set({ isActive: v })} />}
        </div>
      </div>
    </Modal>
  );
}

export function ProductDetailPage() {
  const { id = "" } = useParams();
  const { can } = useAuth();
  const [editing, setEditing] = useState(false);
  const [showDepleted, setShowDepleted] = useState(false);
  const [page, setPage] = useState(1);
  const product = useApi<Product>(`/products/${id}`);
  const batches = useApi<Paged<Batch>>("/inventory/batches", { productId: id, includeDepleted: showDepleted, pageSize: 100 });
  const movements = useApi<Paged<Movement>>(`/products/${id}/movements`, { page, pageSize: 25 }, { keepPrevious: true });

  if (product.isLoading) return <Spinner />;
  if (product.error || !product.data) return <ErrorNote error={product.error} />;
  const p = product.data;
  const avg = p.stockOnHand ? p.stockValue / p.stockOnHand : 0;

  return (
    <>
      <PageHeader
        title={p.name}
        description={
          <>
            <Link to="/products" className="hover:underline">
              Products
            </Link>{" "}
            / {p.sku}
            {p.categoryName && `, ${p.categoryName}`}
          </>
        }
        actions={can("master") && <Button onClick={() => setEditing(true)}>Edit product</Button>}
      />
      <div className="mb-4 grid gap-px overflow-hidden rounded-xl border border-rule bg-rule sm:grid-cols-4">
        {[
          ["On hand", `${qty(p.stockOnHand)} ${p.unitSymbol}`],
          ["Value at FIFO cost", `Rs ${money(p.stockValue)}`],
          ["Average cost of stock", `Rs ${money(avg)}`],
          ["Default sale price", `Rs ${money(p.defaultSalePrice)}`],
        ].map(([label, value]) => (
          <div key={label} className="bg-surface px-4 py-3">
            <div className="text-[12px] text-muted">{label}</div>
            <div className="mt-0.5 text-[17px] font-semibold tabular-nums">{value}</div>
          </div>
        ))}
      </div>

      <Card
        title="Batches, oldest first"
        className="mb-4"
        bodyClassName="p-0"
        actions={<Checkbox label="Show depleted" checked={showDepleted} onChange={setShowDepleted} />}
      >
        {batches.data && batches.data.items.length ? (
          <Table>
            <thead>
              <tr>
                <th>Batch</th>
                <th>Source</th>
                <th>Received</th>
                <th>Expiry</th>
                <th className="num">Unit cost</th>
                <th className="num">Received qty</th>
                <th className="num">Remaining</th>
                <th className="num">Value</th>
              </tr>
            </thead>
            <tbody>
              {[...batches.data.items]
                .sort((a, b) => a.receivedOn.localeCompare(b.receivedOn))
                .map((b) => (
                  <tr key={b.id} className={cx(b.remainingQuantity === 0 && "text-muted")}>
                    <td className="font-medium">{b.batchNumber}</td>
                    <td>{b.source === "Opening" ? "Opening stock" : "Purchase"}</td>
                    <td>{date(b.receivedOn)}</td>
                    <td>
                      <ExpiryCell date={b.expiryDate} />
                    </td>
                    <td className="num">{money(b.unitCost)}</td>
                    <td className="num">{qty(b.originalQuantity)}</td>
                    <td className="num">{qty(b.remainingQuantity)}</td>
                    <td className="num">{money(b.remainingValue)}</td>
                  </tr>
                ))}
            </tbody>
          </Table>
        ) : (
          <p className="p-4 text-[13px] text-muted">No stock. Receive it with a purchase bill.</p>
        )}
      </Card>

      <Card title="Stock card" bodyClassName="p-0">
        {movements.data && movements.data.items.length ? (
          <>
            <Table>
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Document</th>
                  <th>Batch</th>
                  <th>Note</th>
                  <th className="num">In</th>
                  <th className="num">Out</th>
                  <th className="num">Unit cost</th>
                </tr>
              </thead>
              <tbody>
                {movements.data.items.map((m) => {
                  const href = documentHref(m.documentType, m.documentId);
                  return (
                    <tr key={m.id} className={cx(m.isReversal && "text-muted")}>
                      <td>{date(m.date)}</td>
                      <td>
                        {href ? (
                          <Link to={href} className="font-medium hover:text-accent-strong">
                            {m.documentNumber}
                          </Link>
                        ) : (
                          m.documentNumber
                        )}
                        <span className="ml-1.5 text-[11.5px] text-muted">{documentLabel[m.documentType]}</span>
                      </td>
                      <td>{m.batchNumber}</td>
                      <td className="text-muted">{m.note ?? ""}</td>
                      <td className="num text-accent-strong">{m.quantityIn ? qty(m.quantityIn) : ""}</td>
                      <td className="num text-owe">{m.quantityOut ? qty(m.quantityOut) : ""}</td>
                      <td className="num">{money(m.unitCost)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </Table>
            <Pagination page={movements.data.page} pageSize={movements.data.pageSize} total={movements.data.total} onPage={setPage} />
          </>
        ) : (
          <p className="p-4 text-[13px] text-muted">No stock movements yet.</p>
        )}
      </Card>
      <ProductForm product={editing ? p : null} onClose={() => setEditing(false)} />
    </>
  );
}

export function ExpiryCell({ date: d }: { date: string | null }) {
  if (!d) return <span className="text-muted">—</span>;
  const days = daysUntil(d) ?? 0;
  return (
    <span className="inline-flex items-center gap-1.5">
      {date(d)}
      {days < 0 ? <Badge tone="owe">Expired</Badge> : days <= 30 ? <Badge tone="warn">{days}d left</Badge> : null}
    </span>
  );
}
