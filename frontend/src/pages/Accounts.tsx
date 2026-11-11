import { useEffect, useState } from "react";
import { Link, useParams } from "react-router";
import { Banknote, Landmark, Pencil, Plus, Printer, Smartphone } from "lucide-react";
import { api } from "../lib/api";
import { useAccounts, useAction, useApi } from "../lib/hooks";
import { useAuth } from "../lib/auth";
import { cx, money, monthStart, today } from "../lib/format";
import type { Account, AccountType, Statement } from "../lib/types";
import { Badge, Button, Card, Checkbox, DateRange, EmptyState, ErrorNote, Field, Input, Modal, PageHeader, Select, Spinner } from "../components/ui";
import { StatementView } from "../components/StatementView";
import { Letterhead } from "../components/documents";
import { VoucherForm } from "./Vouchers";

const icons: Record<AccountType, typeof Banknote> = { Cash: Banknote, Bank: Landmark, MobileWallet: Smartphone };
const typeLabel: Record<AccountType, string> = { Cash: "Cash", Bank: "Bank", MobileWallet: "Mobile wallet" };

export function AccountsPage() {
  const { can } = useAuth();
  const { data, isLoading, error } = useAccounts(false);
  const [editing, setEditing] = useState<Account | "new" | null>(null);
  const [transfer, setTransfer] = useState(false);
  const total = data?.filter((a) => a.isActive).reduce((s, a) => s + a.balance, 0) ?? 0;

  return (
    <>
      <PageHeader
        title="Cash & bank"
        description={`Real balances, moved only by posted documents. Total across active accounts: Rs ${money(total)}`}
        actions={
          <>
            {can("post") && <Button onClick={() => setTransfer(true)}>Transfer between accounts</Button>}
            {can("master") && (
              <Button variant="primary" icon={<Plus className="size-3.5" />} onClick={() => setEditing("new")}>
                Add account
              </Button>
            )}
          </>
        }
      />
      <ErrorNote error={error} />
      {isLoading ? (
        <Spinner />
      ) : data && data.length ? (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {data.map((a) => {
            const Icon = icons[a.type];
            return (
              <div key={a.id} className={cx("group relative rounded-xl border border-rule bg-surface p-4 transition-colors hover:border-rule-strong", !a.isActive && "opacity-60")}>
                <Link to={`/accounts/${a.id}`} className="absolute inset-0 rounded-xl" aria-label={`Open ${a.name} statement`} />
                <div className="flex items-start justify-between">
                  <div className="flex items-center gap-2.5">
                    <div className="flex size-8 items-center justify-center rounded-lg bg-wash-strong text-ink-soft">
                      <Icon className="size-4" />
                    </div>
                    <div>
                      <div className="font-medium">{a.name}</div>
                      <div className="text-[12px] text-muted">
                        {typeLabel[a.type]}
                        {a.bankName && `, ${a.bankName}`}
                        {a.accountNumber && ` ${a.accountNumber}`}
                      </div>
                    </div>
                  </div>
                  {can("master") && (
                    <button
                      className="relative z-[1] rounded-md p-1 text-muted opacity-0 hover:bg-wash-strong hover:text-ink group-hover:opacity-100 focus:opacity-100 pointer-coarse:opacity-100"
                      onClick={() => setEditing(a)}
                      aria-label={`Edit ${a.name}`}
                    >
                      <Pencil className="size-3.5" />
                    </button>
                  )}
                </div>
                <div className={cx("mt-4 text-[22px] font-semibold tabular-nums tracking-[-0.01em]", a.balance < 0 && "text-owe")}>Rs {money(a.balance)}</div>
                <div className="mt-1 flex gap-1.5">
                  {!a.isActive && <Badge>Inactive</Badge>}
                  {a.allowNegative && <Badge tone="warn">Overdraft allowed</Badge>}
                </div>
              </div>
            );
          })}
        </div>
      ) : (
        <EmptyState title="No accounts yet" action={can("master") && <Button onClick={() => setEditing("new")}>Add account</Button>}>
          Add a cash drawer and each bank account you use. At least one is needed before going live.
        </EmptyState>
      )}
      <AccountForm account={editing} onClose={() => setEditing(null)} />
      <VoucherForm type={transfer ? "Transfer" : null} onClose={() => setTransfer(false)} />
    </>
  );
}

export function AccountForm({ account, onClose }: { account: Account | "new" | null; onClose: () => void }) {
  const isNew = account === "new";
  const a = account && account !== "new" ? account : null;
  const [form, setForm] = useState({ name: "", type: "Cash" as AccountType, bankName: "", accountNumber: "", allowNegative: false, isActive: true });
  useEffect(() => {
    if (account)
      setForm({
        name: a?.name ?? "",
        type: a?.type ?? "Cash",
        bankName: a?.bankName ?? "",
        accountNumber: a?.accountNumber ?? "",
        allowNegative: a?.allowNegative ?? false,
        isActive: a?.isActive ?? true,
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [account]);
  const set = (patch: Partial<typeof form>) => setForm((f) => ({ ...f, ...patch }));
  const save = useAction(() => (isNew ? api.post("/accounts", form) : api.put(`/accounts/${a!.id}`, form)), {
    success: isNew ? "Account added" : "Account updated",
    onSuccess: onClose,
  });

  return (
    <Modal
      open={!!account}
      onClose={onClose}
      title={isNew ? "Add account" : `Edit ${a?.name}`}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" disabled={!form.name.trim()} loading={save.isPending} onClick={() => save.mutate(undefined)}>
            {isNew ? "Add account" : "Save changes"}
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-2 gap-3">
        <Field label="Name" className="col-span-2">
          <Input autoFocus value={form.name} onChange={(e) => set({ name: e.target.value })} placeholder="e.g. Shop cash, Meezan current" />
        </Field>
        <Field label="Type">
          <Select value={form.type} onChange={(e) => set({ type: e.target.value as AccountType, allowNegative: e.target.value === "Cash" ? false : form.allowNegative })}>
            <option value="Cash">Cash</option>
            <option value="Bank">Bank</option>
            <option value="MobileWallet">Mobile wallet</option>
          </Select>
        </Field>
        <Field label="Bank / provider">
          <Input value={form.bankName} disabled={form.type === "Cash"} onChange={(e) => set({ bankName: e.target.value })} />
        </Field>
        <Field label="Account number / IBAN" className="col-span-2">
          <Input value={form.accountNumber} disabled={form.type === "Cash"} onChange={(e) => set({ accountNumber: e.target.value })} />
        </Field>
        <div className="col-span-2 flex flex-wrap gap-5">
          {form.type !== "Cash" && (
            <Checkbox label="Allow overdraft (balance may go below zero)" checked={form.allowNegative} onChange={(v) => set({ allowNegative: v })} />
          )}
          {!isNew && <Checkbox label="Active" checked={form.isActive} onChange={(v) => set({ isActive: v })} />}
        </div>
      </div>
      {isNew && <p className="mt-3 text-[12.5px] text-muted">The balance on go-live day is entered on the Day-0 setup page.</p>}
    </Modal>
  );
}

export function AccountStatementPage() {
  const { id = "" } = useParams();
  const [from, setFrom] = useState(monthStart());
  const [to, setTo] = useState(today());
  const account = useApi<Account>(`/accounts/${id}`);
  const statement = useApi<Statement>(`/accounts/${id}/statement`, { from, to }, { keepPrevious: true });
  if (account.isLoading) return <Spinner />;
  if (account.error || !account.data) return <ErrorNote error={account.error} />;
  const a = account.data;

  return (
    <>
      <PageHeader
        title={a.name}
        description={
          <>
            <Link to="/accounts" className="hover:underline">
              Cash & bank
            </Link>{" "}
            / balance <span className={cx("font-medium tabular-nums", a.balance < 0 ? "text-owe" : "text-ink")}>Rs {money(a.balance)}</span>
          </>
        }
        actions={
          <Button icon={<Printer className="size-3.5" />} onClick={() => window.print()}>
            Print
          </Button>
        }
      />
      <div className="no-print mb-3 flex flex-wrap items-center gap-2">
        <DateRange from={from} to={to} onFrom={setFrom} onTo={setTo} />
        <Button size="sm" variant="ghost" onClick={() => setFrom("")}>
          From the start
        </Button>
      </div>
      <Card bodyClassName="p-0" className="print-area">
        <div className="print-only p-6">
          <Letterhead title="Account statement" number={a.name} />
        </div>
        {statement.isLoading || !statement.data ? <Spinner /> : <StatementView statement={statement.data} debitLabel="Money in" creditLabel="Money out" />}
      </Card>
    </>
  );
}
