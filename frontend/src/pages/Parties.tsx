import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { Pencil, Plus, Printer } from "lucide-react";
import { api } from "../lib/api";
import { useAction, useApi, useDebounced } from "../lib/hooks";
import { useAuth } from "../lib/auth";
import { balanceLabel, cx, money, monthStart, today } from "../lib/format";
import type { Paged, Party, PartyKind, Statement, VoucherType } from "../lib/types";
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
  PageHeader,
  Pagination,
  SearchInput,
  Select,
  Spinner,
  Tabs,
  Textarea,
} from "../components/ui";
import { StatementView } from "../components/StatementView";
import { Letterhead } from "../components/documents";
import { VoucherForm } from "./Vouchers";

type KindFilter = "all" | "Customer" | "Supplier";
type BalFilter = "Any" | "Receivable" | "Payable" | "Settled";

export function PartiesPage() {
  const { can } = useAuth();
  const navigate = useNavigate();
  const [kind, setKind] = useState<KindFilter>("all");
  const [bal, setBal] = useState<BalFilter>("Any");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<Party | "new" | null>(null);
  const debounced = useDebounced(search);
  useEffect(() => setPage(1), [kind, bal, debounced]);

  const { data, isLoading, error } = useApi<Paged<Party>>(
    "/parties",
    { kind: kind === "all" ? undefined : kind, balance: bal, search: debounced, page, pageSize: 30 },
    { keepPrevious: true },
  );

  return (
    <>
      <PageHeader
        title="Customers & suppliers"
        description="One record per party with a single running balance. Positive means they owe you; negative means you owe them."
        actions={
          can("master") && (
            <Button variant="primary" icon={<Plus className="size-3.5" />} onClick={() => setEditing("new")}>
              Add party
            </Button>
          )
        }
      />
      <div className="mb-3 flex flex-wrap items-center gap-2">
        <Tabs<KindFilter>
          value={kind}
          onChange={setKind}
          tabs={[
            { value: "all", label: "Everyone" },
            { value: "Customer", label: "Customers" },
            { value: "Supplier", label: "Suppliers" },
          ]}
        />
        <SearchInput value={search} onChange={setSearch} placeholder="Name, code or phone" />
        <Select value={bal} onChange={(e) => setBal(e.target.value as BalFilter)} className="w-[170px]" aria-label="Balance filter">
          <option value="Any">Any balance</option>
          <option value="Receivable">They owe us</option>
          <option value="Payable">We owe them</option>
          <option value="Settled">Settled</option>
        </Select>
      </div>

      <div className="overflow-hidden rounded-xl border border-rule bg-surface">
        <ErrorNote error={error} />
        {isLoading ? (
          <Spinner />
        ) : data && data.items.length ? (
          <table className="table-dense w-full">
            <thead>
              <tr>
                <th className="w-[100px]">Code</th>
                <th>Name</th>
                <th>Type</th>
                <th>Phone</th>
                <th className="num">Balance</th>
                <th className="w-10" />
              </tr>
            </thead>
            <tbody>
              {data.items.map((p) => {
                const b = balanceLabel(p.balance);
                return (
                  <tr key={p.id} className={cx("cursor-pointer", !p.isActive && "text-muted")} onClick={() => navigate(`/parties/${p.id}`)}>
                    <td className="text-muted">{p.code}</td>
                    <td className="font-medium">
                      {p.name} {!p.isActive && <Badge>Inactive</Badge>}
                    </td>
                    <td>{p.kind === "Both" ? "Customer & supplier" : p.kind}</td>
                    <td className="text-slate-600">{p.phone ?? ""}</td>
                    <td className={cx("num", b.tone === "owe" && "text-owe", b.tone === "accent" && "text-accent-strong", b.tone === "muted" && "text-muted")}>
                      {b.text}
                    </td>
                    <td>
                      {can("master") && !p.isSystem && (
                        <button
                          aria-label={`Edit ${p.name}`}
                          className="rounded p-1 text-muted hover:bg-slate-100 hover:text-ink"
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
          </table>
        ) : (
          <EmptyState title="No parties match" action={can("master") && <Button onClick={() => setEditing("new")}>Add party</Button>}>
            Customers and suppliers share one list. A party can be both.
          </EmptyState>
        )}
        {data && <Pagination page={data.page} pageSize={data.pageSize} total={data.total} onPage={setPage} />}
      </div>
      <PartyForm party={editing} onClose={() => setEditing(null)} />
    </>
  );
}

export function PartyForm({ party, onClose }: { party: Party | "new" | null; onClose: () => void }) {
  const isNew = party === "new";
  const p = party && party !== "new" ? party : null;
  const [form, setForm] = useState({ code: "", name: "", kind: "Customer" as PartyKind, phone: "", email: "", address: "", taxNumber: "", notes: "", isActive: true });
  useEffect(() => {
    if (party)
      setForm({
        code: p?.code ?? "",
        name: p?.name ?? "",
        kind: p?.kind ?? "Customer",
        phone: p?.phone ?? "",
        email: p?.email ?? "",
        address: p?.address ?? "",
        taxNumber: p?.taxNumber ?? "",
        notes: p?.notes ?? "",
        isActive: p?.isActive ?? true,
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [party]);
  const set = (patch: Partial<typeof form>) => setForm((f) => ({ ...f, ...patch }));
  const save = useAction(() => (isNew ? api.post("/parties", form) : api.put(`/parties/${p!.id}`, form)), {
    success: isNew ? "Party added" : "Party updated",
    onSuccess: onClose,
  });

  return (
    <Modal
      open={!!party}
      onClose={onClose}
      title={isNew ? "Add party" : `Edit ${p?.name}`}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" disabled={!form.name.trim()} loading={save.isPending} onClick={() => save.mutate(undefined)}>
            {isNew ? "Add party" : "Save changes"}
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-2 gap-3">
        <Field label="Name" className="col-span-2">
          <Input autoFocus value={form.name} onChange={(e) => set({ name: e.target.value })} />
        </Field>
        <Field label="Type">
          <Select value={form.kind} onChange={(e) => set({ kind: e.target.value as PartyKind })}>
            <option value="Customer">Customer</option>
            <option value="Supplier">Supplier</option>
            <option value="Both">Customer & supplier</option>
          </Select>
        </Field>
        <Field label="Code" hint={isNew ? "Leave blank to number automatically" : undefined}>
          <Input value={form.code} onChange={(e) => set({ code: e.target.value })} />
        </Field>
        <Field label="Phone">
          <Input value={form.phone} onChange={(e) => set({ phone: e.target.value })} />
        </Field>
        <Field label="Email">
          <Input type="email" value={form.email} onChange={(e) => set({ email: e.target.value })} />
        </Field>
        <Field label="Address" className="col-span-2">
          <Textarea value={form.address} onChange={(e) => set({ address: e.target.value })} rows={2} />
        </Field>
        <Field label="NTN / STRN">
          <Input value={form.taxNumber} onChange={(e) => set({ taxNumber: e.target.value })} />
        </Field>
        <Field label="Notes">
          <Input value={form.notes} onChange={(e) => set({ notes: e.target.value })} />
        </Field>
        {!isNew && (
          <div className="col-span-2">
            <Checkbox label="Active (inactive parties can't be used on new documents)" checked={form.isActive} onChange={(v) => set({ isActive: v })} />
          </div>
        )}
      </div>
      {isNew && <p className="mt-3 text-[12.5px] text-muted">Balances carried over from before go-live are entered on the Day-0 setup page.</p>}
    </Modal>
  );
}

export function PartyStatementPage() {
  const { id = "" } = useParams();
  const { can } = useAuth();
  const [from, setFrom] = useState(monthStart());
  const [to, setTo] = useState(today());
  const [voucher, setVoucher] = useState<VoucherType | null>(null);
  const [editing, setEditing] = useState(false);
  const party = useApi<Party>(`/parties/${id}`);
  const statement = useApi<Statement>(`/parties/${id}/statement`, { from, to }, { keepPrevious: true });

  if (party.isLoading) return <Spinner />;
  if (party.error || !party.data) return <ErrorNote error={party.error} />;
  const p = party.data;
  const b = balanceLabel(p.balance);

  return (
    <>
      <PageHeader
        title={p.name}
        description={
          <>
            <Link to="/parties" className="hover:underline">
              Customers & suppliers
            </Link>{" "}
            / {p.code}
            {p.phone && `, ${p.phone}`}
          </>
        }
        actions={
          <>
            <Button icon={<Printer className="size-3.5" />} onClick={() => window.print()}>
              Print statement
            </Button>
            {can("master") && !p.isSystem && <Button onClick={() => setEditing(true)}>Edit</Button>}
            {can("post") && p.kind !== "Customer" && <Button onClick={() => setVoucher("Payment")}>Pay</Button>}
            {can("post") && p.kind !== "Supplier" && (
              <Button variant="primary" onClick={() => setVoucher("Receipt")}>
                Receive payment
              </Button>
            )}
          </>
        }
      />
      <div className="no-print mb-4 grid gap-px overflow-hidden rounded-xl border border-rule bg-rule sm:grid-cols-3">
        <div className="bg-surface px-4 py-3">
          <div className="text-[12px] text-muted">Current balance</div>
          <div className={cx("mt-0.5 text-[20px] font-semibold tabular-nums", b.tone === "owe" && "text-owe", b.tone === "accent" && "text-accent-strong")}>
            {b.text}
          </div>
        </div>
        <div className="bg-surface px-4 py-3">
          <div className="text-[12px] text-muted">Opening balance (Day-0)</div>
          <div className="mt-0.5 text-[20px] font-semibold tabular-nums">{money(p.openingBalance)}</div>
        </div>
        <div className="bg-surface px-4 py-3">
          <div className="text-[12px] text-muted">Type</div>
          <div className="mt-0.5 text-[15px] font-medium">{p.kind === "Both" ? "Customer & supplier" : p.kind}</div>
        </div>
      </div>
      <div className="no-print mb-3 flex items-center gap-2">
        <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="w-[150px]" aria-label="From" />
        <span className="text-muted">to</span>
        <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="w-[150px]" aria-label="To" />
        <Button size="sm" variant="ghost" onClick={() => setFrom("")}>
          From the start
        </Button>
      </div>
      <Card bodyClassName="p-0" className="print-area">
        <div className="print-only p-6">
          <Letterhead title="Statement of account" number={p.name} />
        </div>
        {statement.isLoading || !statement.data ? (
          <Spinner />
        ) : (
          <StatementView
            statement={statement.data}
            debitLabel="Debit"
            creditLabel="Credit"
            balanceFormatter={(n) => (n < 0 ? `${money(-n)} Cr` : `${money(n)} Dr`)}
          />
        )}
      </Card>
      <p className="no-print mt-2 text-[12px] text-muted">Dr = they owe us. Cr = we owe them or hold their advance.</p>

      <VoucherForm type={voucher} presetParty={p} onClose={() => setVoucher(null)} />
      <PartyForm party={editing ? p : null} onClose={() => setEditing(false)} />
    </>
  );
}
