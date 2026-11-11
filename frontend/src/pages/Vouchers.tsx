import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router";
import { Ban, Plus } from "lucide-react";
import { api } from "../lib/api";
import { useAction, useApi, useDebounced } from "../lib/hooks";
import { useAuth } from "../lib/auth";
import { cx, date, dateTime, documentHref, documentLabel, money, today } from "../lib/format";
import type { Paged, Party, Voucher, VoucherType } from "../lib/types";
import {
  Badge,
  Button,
  DateRange,
  EmptyState,
  ErrorNote,
  Field,
  Input,
  Modal,
  NumberInput,
  PageHeader,
  Pagination,
  SearchInput,
  Spinner,
  StatusBadge,
  Table,
  Tabs,
  Textarea,
  VoidDialog,
} from "../components/ui";
import { AccountSelect, ExpenseCategorySelect, PartyPicker } from "../components/pickers";
import { Row } from "../components/documents";

const typeInfo: Record<VoucherType, { label: string; verb: string; help: string }> = {
  Receipt: { label: "Receipt", verb: "Record receipt", help: "Money received from a customer (or a refund from a supplier). Lowers what they owe us." },
  Payment: { label: "Payment", verb: "Record payment", help: "Money paid to a supplier (or a refund to a customer). Lowers what we owe them." },
  Expense: { label: "Expense", verb: "Record expense", help: "Rent, salaries, utilities and other running costs. Charged straight to net profit." },
  Transfer: { label: "Transfer", verb: "Record transfer", help: "Move money between your own cash and bank accounts. No profit effect." },
};

type Filter = "all" | VoucherType;

export default function VouchersPage() {
  const { can } = useAuth();
  const [params, setParams] = useSearchParams();
  const openId = params.get("open");
  const [type, setType] = useState<Filter>("all");
  const [search, setSearch] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState<VoucherType | null>(null);
  const debounced = useDebounced(search);
  useEffect(() => setPage(1), [type, debounced, from, to]);

  const { data, isLoading, error } = useApi<Paged<Voucher>>(
    "/vouchers",
    { type: type === "all" ? undefined : type, search: debounced, from, to, page, pageSize: 25 },
    { keepPrevious: true },
  );

  return (
    <>
      <PageHeader
        title="Payments & expenses"
        description="Receipts and payments settle party balances. Expenses and transfers move cash only."
        actions={
          can("post") && (
            <>
              <Button onClick={() => setCreating("Transfer")}>Transfer</Button>
              <Button onClick={() => setCreating("Expense")}>Expense</Button>
              <Button onClick={() => setCreating("Payment")}>Pay supplier</Button>
              <Button variant="primary" icon={<Plus className="size-3.5" />} onClick={() => setCreating("Receipt")}>
                Receive payment
              </Button>
            </>
          )
        }
      />
      <div className="mb-3 flex flex-wrap items-center gap-2">
        <Tabs<Filter>
          value={type}
          onChange={setType}
          tabs={[
            { value: "all", label: "All" },
            { value: "Receipt", label: "Receipts" },
            { value: "Payment", label: "Payments" },
            { value: "Expense", label: "Expenses" },
            { value: "Transfer", label: "Transfers" },
          ]}
        />
        <SearchInput value={search} onChange={setSearch} placeholder="Number, reference or party" />
        <DateRange from={from} to={to} onFrom={setFrom} onTo={setTo} />
      </div>

      <div className="overflow-hidden rounded-xl border border-rule bg-surface">
        <ErrorNote error={error} />
        {isLoading ? (
          <Spinner />
        ) : data && data.items.length > 0 ? (
          <Table>
            <thead>
              <tr>
                <th>Number</th>
                <th>Date</th>
                <th>Type</th>
                <th>Party / category</th>
                <th>Account</th>
                <th>Reference</th>
                <th>Status</th>
                <th className="num">Amount</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((v) => (
                <tr key={v.id} className={cx("cursor-pointer", v.status === "Voided" && "text-muted")} onClick={() => setParams({ open: v.id })}>
                  <td className="font-medium">{v.number}</td>
                  <td>{date(v.date)}</td>
                  <td>
                    <TypeBadge type={v.type} />
                  </td>
                  <td>{v.partyName ?? v.expenseCategoryName ?? (v.toAccountName ? `To ${v.toAccountName}` : "—")}</td>
                  <td className="text-ink-soft">{v.accountName}</td>
                  <td className="text-muted">{v.reference ?? v.linkedDocumentNumber ?? ""}</td>
                  <td>
                    <StatusBadge status={v.status} />
                  </td>
                  <td className={cx("num font-medium", v.status === "Voided" && "line-through")}>{money(v.amount)}</td>
                </tr>
              ))}
            </tbody>
          </Table>
        ) : (
          <EmptyState title="No vouchers yet">Receipts, payments, expenses and transfers you record will be listed here.</EmptyState>
        )}
        {data && <Pagination page={data.page} pageSize={data.pageSize} total={data.total} onPage={setPage} />}
      </div>

      <VoucherForm type={creating} onClose={() => setCreating(null)} onCreated={(id) => setParams({ open: id })} />
      <VoucherDetail id={openId} onClose={() => setParams({})} />
    </>
  );
}

function TypeBadge({ type }: { type: VoucherType }) {
  const tone = type === "Receipt" ? "accent" : type === "Payment" ? "owe" : type === "Expense" ? "warn" : "neutral";
  return <Badge tone={tone}>{typeInfo[type].label}</Badge>;
}

export function VoucherForm({
  type,
  onClose,
  onCreated,
  presetParty,
}: {
  type: VoucherType | null;
  onClose: () => void;
  onCreated?: (id: string) => void;
  presetParty?: Party | null;
}) {
  const [voucherDate, setDate] = useState(today());
  const [party, setParty] = useState<Party | null>(presetParty ?? null);
  const [account, setAccount] = useState("");
  const [toAccount, setToAccount] = useState("");
  const [category, setCategory] = useState("");
  const [amount, setAmount] = useState<number | "">("");
  const [reference, setReference] = useState("");
  const [notes, setNotes] = useState("");

  useEffect(() => {
    if (type) {
      setDate(today());
      setParty(presetParty ?? null);
      setAccount("");
      setToAccount("");
      setCategory("");
      setAmount("");
      setReference("");
      setNotes("");
    }
  }, [type, presetParty]);

  const create = useAction(
    () =>
      api.post<{ id: string }>("/vouchers", {
        type,
        date: voucherDate,
        partyId: party?.id ?? null,
        accountId: account,
        toAccountId: toAccount || null,
        expenseCategoryId: category || null,
        amount: Number(amount),
        reference: reference || null,
        notes: notes || null,
      }),
    {
      success: `${type} recorded`,
      onSuccess: (r) => {
        onClose();
        onCreated?.(r.id);
      },
    },
  );

  if (!type) return <Modal open={false} onClose={onClose} title="">{null}</Modal>;
  const info = typeInfo[type];
  const needsParty = type === "Receipt" || type === "Payment";
  const valid =
    account && Number(amount) > 0 && (!needsParty || party) && (type !== "Expense" || category) && (type !== "Transfer" || toAccount);

  return (
    <Modal
      open={!!type}
      onClose={onClose}
      title={info.verb}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" disabled={!valid} loading={create.isPending} onClick={() => create.mutate(undefined)}>
            {info.verb}
          </Button>
        </>
      }
    >
      <p className="mb-4 text-[13px] text-ink-soft">{info.help}</p>
      <div className="grid grid-cols-2 gap-3">
        {needsParty && (
          <Field label={type === "Receipt" ? "Received from" : "Paid to"} className="col-span-2">
            <PartyPicker value={party} onSelect={setParty} autoFocus={!presetParty} />
          </Field>
        )}
        {needsParty && party && (
          <div className="col-span-2 -mt-1 text-[12.5px] text-muted">
            Current balance:{" "}
            <span className={party.balance < 0 ? "text-owe" : party.balance > 0 ? "text-accent-strong" : ""}>
              {party.balance > 0 ? `${money(party.balance)} receivable` : party.balance < 0 ? `${money(-party.balance)} payable` : "settled"}
            </span>
            {party.balance !== 0 && (
              <button className="ml-2 text-accent-strong hover:underline" onClick={() => setAmount(Math.abs(party.balance))}>
                Use full amount
              </button>
            )}
          </div>
        )}
        {type === "Expense" && (
          <Field label="Expense category" className="col-span-2">
            <ExpenseCategorySelect value={category} onChange={setCategory} />
          </Field>
        )}
        <Field label={type === "Receipt" ? "Into account" : "From account"}>
          <AccountSelect value={account} onChange={setAccount} />
        </Field>
        {type === "Transfer" ? (
          <Field label="To account">
            <AccountSelect value={toAccount} onChange={setToAccount} exclude={account} />
          </Field>
        ) : (
          <Field label="Date">
            <Input type="date" value={voucherDate} onChange={(e) => setDate(e.target.value)} />
          </Field>
        )}
        <Field label="Amount (Rs)">
          <NumberInput value={amount} onChange={setAmount} min={0} placeholder="0.00" />
        </Field>
        {type === "Transfer" ? (
          <Field label="Date">
            <Input type="date" value={voucherDate} onChange={(e) => setDate(e.target.value)} />
          </Field>
        ) : (
          <Field label="Reference">
            <Input value={reference} onChange={(e) => setReference(e.target.value)} placeholder="Cheque or transaction no." />
          </Field>
        )}
        <Field label="Notes" className="col-span-2">
          <Textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={2} />
        </Field>
      </div>
    </Modal>
  );
}

function VoucherDetail({ id, onClose }: { id: string | null; onClose: () => void }) {
  const { can } = useAuth();
  const { data: v } = useApi<Voucher>(id ? `/vouchers/${id}` : null);
  const [voidOpen, setVoidOpen] = useState(false);
  const voidAction = useAction((reason: string) => api.post(`/vouchers/${id}/void`, { reason }), {
    success: "Voucher voided",
    onSuccess: () => setVoidOpen(false),
  });
  const linkHref = v?.linkedDocumentType && v.linkedDocumentId ? documentHref(v.linkedDocumentType, v.linkedDocumentId) : null;

  return (
    <>
      <Modal
        open={!!id && !voidOpen}
        onClose={onClose}
        title={v ? `${v.number}` : "Voucher"}
        footer={
          v &&
          v.status === "Posted" &&
          can("void") && (
            <Button variant="danger" icon={<Ban className="size-3.5" />} onClick={() => setVoidOpen(true)}>
              Void voucher
            </Button>
          )
        }
      >
        {!v ? (
          <Spinner />
        ) : (
          <>
            <div className="mb-4 flex items-center gap-2">
              <TypeBadge type={v.type} />
              <StatusBadge status={v.status} />
            </div>
            <div className="mb-4 text-[28px] font-semibold tabular-nums tracking-[-0.02em]">Rs {money(v.amount)}</div>
            <dl className="space-y-1.5 text-[13px]">
              <Row label="Date">{date(v.date)}</Row>
              {v.partyName && (
                <Row label="Party">
                  <Link className="text-accent-strong hover:underline" to={`/parties/${v.partyId}`}>
                    {v.partyName}
                  </Link>
                </Row>
              )}
              {v.expenseCategoryName && <Row label="Category">{v.expenseCategoryName}</Row>}
              <Row label={v.type === "Receipt" ? "Into" : "From"}>{v.accountName}</Row>
              {v.toAccountName && <Row label="To">{v.toAccountName}</Row>}
              {v.reference && <Row label="Reference">{v.reference}</Row>}
              {v.linkedDocumentNumber && (
                <Row label={`Linked ${documentLabel[v.linkedDocumentType ?? ""]?.toLowerCase() ?? "document"}`}>
                  {linkHref ? (
                    <Link className="text-accent-strong hover:underline" to={linkHref}>
                      {v.linkedDocumentNumber}
                    </Link>
                  ) : (
                    v.linkedDocumentNumber
                  )}
                </Row>
              )}
              {v.notes && <Row label="Notes">{v.notes}</Row>}
              <Row label="Recorded by">
                {v.createdBy}, {dateTime(v.createdAt)}
              </Row>
              {v.status === "Voided" && (
                <Row label="Voided">
                  <span className="text-owe">
                    {v.voidedBy}, {dateTime(v.voidedAt)}: {v.voidReason}
                  </span>
                </Row>
              )}
            </dl>
          </>
        )}
      </Modal>
      {v && (
        <VoidDialog
          open={voidOpen}
          onClose={() => setVoidOpen(false)}
          number={v.number}
          loading={voidAction.isPending}
          onConfirm={(reason) => voidAction.mutate(reason)}
          consequence="The cash movement and any party balance change are reversed with equal and opposite ledger lines."
        />
      )}
    </>
  );
}
