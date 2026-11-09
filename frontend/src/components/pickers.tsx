import { useState } from "react";
import { Combobox, Select } from "./ui";
import { useAccounts, useDebounced, useExpenseCategories, usePartiesLookup, useProductsLookup } from "../lib/hooks";
import { balanceLabel, money, qty } from "../lib/format";
import type { Account, Party, PartyKind, Product } from "../lib/types";

export function ProductPicker({
  value,
  onSelect,
  autoFocus,
}: {
  value?: { name: string; sku: string } | null;
  onSelect: (p: Product) => void;
  autoFocus?: boolean;
}) {
  const [search, setSearch] = useState("");
  const debounced = useDebounced(search);
  const { data, isFetching } = useProductsLookup(debounced);
  return (
    <Combobox<Product>
      items={data?.items ?? []}
      loading={isFetching}
      getKey={(p) => p.id}
      getLabel={(p) => `${p.name}`}
      selectedLabel={value ? value.name : ""}
      onSearch={setSearch}
      onSelect={onSelect}
      placeholder="Product name or SKU"
      autoFocus={autoFocus}
      renderItem={(p) => (
        <div className="flex items-center justify-between gap-3">
          <div className="min-w-0">
            <div className="truncate">{p.name}</div>
            <div className="text-[11.5px] text-muted">{p.sku}</div>
          </div>
          <div className="text-right text-[12px] tabular-nums">
            <div className={p.stockOnHand <= 0 ? "text-owe" : "text-slate-600"}>
              {qty(p.stockOnHand)} {p.unitSymbol}
            </div>
            <div className="text-muted">Rs {money(p.defaultSalePrice)}</div>
          </div>
        </div>
      )}
    />
  );
}

export function PartyPicker({
  value,
  onSelect,
  kind,
  placeholder = "Search party",
  autoFocus,
}: {
  value?: { name: string } | null;
  onSelect: (p: Party) => void;
  kind?: Exclude<PartyKind, "Both">;
  placeholder?: string;
  autoFocus?: boolean;
}) {
  const [search, setSearch] = useState("");
  const debounced = useDebounced(search);
  const { data, isFetching } = usePartiesLookup(debounced, kind);
  return (
    <Combobox<Party>
      items={data?.items ?? []}
      loading={isFetching}
      getKey={(p) => p.id}
      getLabel={(p) => p.name}
      selectedLabel={value?.name ?? ""}
      onSearch={setSearch}
      onSelect={onSelect}
      placeholder={placeholder}
      autoFocus={autoFocus}
      renderItem={(p) => {
        const b = balanceLabel(p.balance);
        return (
          <div className="flex items-center justify-between gap-3">
            <div className="min-w-0">
              <div className="truncate">{p.name}</div>
              <div className="text-[11.5px] text-muted">{p.code}{p.phone ? `, ${p.phone}` : ""}</div>
            </div>
            <span className={`text-[12px] tabular-nums ${b.tone === "owe" ? "text-owe" : b.tone === "accent" ? "text-accent-strong" : "text-muted"}`}>
              {b.text}
            </span>
          </div>
        );
      }}
    />
  );
}

export function AccountSelect({
  value,
  onChange,
  allowEmpty,
  emptyLabel = "Select account",
  exclude,
}: {
  value: string;
  onChange: (id: string, account?: Account) => void;
  allowEmpty?: boolean;
  emptyLabel?: string;
  exclude?: string;
}) {
  const { data } = useAccounts();
  return (
    <Select value={value} onChange={(e) => onChange(e.target.value, data?.find((a) => a.id === e.target.value))}>
      {(allowEmpty || !value) && <option value="">{emptyLabel}</option>}
      {data
        ?.filter((a) => a.id !== exclude)
        .map((a) => (
          <option key={a.id} value={a.id}>
            {a.name} (Rs {money(a.balance)})
          </option>
        ))}
    </Select>
  );
}

export function ExpenseCategorySelect({ value, onChange }: { value: string; onChange: (id: string) => void }) {
  const { data } = useExpenseCategories();
  return (
    <Select value={value} onChange={(e) => onChange(e.target.value)}>
      {!value && <option value="">Select category</option>}
      {data?.map((c) => (
        <option key={c.id} value={c.id}>
          {c.name}
        </option>
      ))}
    </Select>
  );
}
