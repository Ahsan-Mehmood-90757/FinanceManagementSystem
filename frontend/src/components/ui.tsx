import {
  useEffect,
  useId,
  useRef,
  useState,
  type ButtonHTMLAttributes,
  type InputHTMLAttributes,
  type ReactNode,
  type SelectHTMLAttributes,
  type TextareaHTMLAttributes,
} from "react";
import { AnimatePresence, motion } from "motion/react";
import { ChevronLeft, ChevronRight, Loader2, Search, X } from "lucide-react";
import { cx, money } from "../lib/format";
import type { DocumentStatus } from "../lib/types";

// ------------------------------------------------------------------ Button
type Variant = "primary" | "secondary" | "ghost" | "danger";
const variants: Record<Variant, string> = {
  primary: "bg-accent text-white hover:bg-accent-strong border border-accent-strong/40",
  secondary: "bg-surface text-ink border border-rule hover:border-rule-strong hover:bg-slate-50",
  ghost: "text-muted hover:text-ink hover:bg-slate-100 border border-transparent",
  danger: "bg-surface text-owe border border-owe/30 hover:bg-owe-soft",
};

export function Button({
  variant = "secondary",
  size = "md",
  loading,
  icon,
  className,
  children,
  disabled,
  ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant;
  size?: "sm" | "md";
  loading?: boolean;
  icon?: ReactNode;
}) {
  return (
    <button
      {...rest}
      disabled={disabled || loading}
      className={cx(
        "inline-flex items-center justify-center gap-1.5 whitespace-nowrap rounded-md font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-55",
        size === "sm" ? "h-7 px-2.5 text-[12.5px]" : "h-8 px-3 text-[13px]",
        variants[variant],
        className,
      )}
    >
      {loading ? <Loader2 className="size-3.5 animate-spin" /> : icon}
      {children}
    </button>
  );
}

// ------------------------------------------------------------------ Form controls
export function Field({
  label,
  hint,
  error,
  children,
  className,
}: {
  label: string;
  hint?: ReactNode;
  error?: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <label className={cx("flex flex-col gap-1", className)}>
      <span className="text-[12px] font-medium text-slate-600">{label}</span>
      {children}
      {error ? <span className="text-[12px] text-owe">{error}</span> : hint ? <span className="text-[12px] text-muted">{hint}</span> : null}
    </label>
  );
}

export function Input({ className, ...rest }: InputHTMLAttributes<HTMLInputElement>) {
  return <input {...rest} className={cx("input", className)} />;
}

export function NumberInput({
  value,
  onChange,
  className,
  ...rest
}: Omit<InputHTMLAttributes<HTMLInputElement>, "value" | "onChange" | "type"> & {
  value: number | "";
  onChange: (v: number | "") => void;
}) {
  return (
    <input
      {...rest}
      type="number"
      inputMode="decimal"
      step="any"
      value={value}
      onChange={(e) => onChange(e.target.value === "" ? "" : Number(e.target.value))}
      onWheel={(e) => (e.target as HTMLInputElement).blur()}
      className={cx("input num", className)}
    />
  );
}

export function Select({ className, children, ...rest }: SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select {...rest} className={cx("input pr-7", className)}>
      {children}
    </select>
  );
}

export function Textarea({ className, ...rest }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea {...rest} className={cx("input h-auto min-h-16 py-1.5", className)} />;
}

export function Checkbox({ label, checked, onChange }: { label: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="inline-flex cursor-pointer items-center gap-2 text-[13px]">
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="size-3.5 rounded border-rule-strong accent-[#0f766e]"
      />
      {label}
    </label>
  );
}

export function SearchInput({ value, onChange, placeholder = "Search" }: { value: string; onChange: (v: string) => void; placeholder?: string }) {
  return (
    <div className="relative w-64">
      <Search className="pointer-events-none absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-slate-400" />
      <input className="input pl-8" value={value} placeholder={placeholder} onChange={(e) => onChange(e.target.value)} />
    </div>
  );
}

// ------------------------------------------------------------------ Layout bits
export function PageHeader({ title, description, actions }: { title: ReactNode; description?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="no-print mb-5 flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 className="text-[20px] font-semibold tracking-[-0.01em]">{title}</h1>
        {description && <p className="mt-0.5 text-[13px] text-muted">{description}</p>}
      </div>
      {actions && <div className="flex items-center gap-2">{actions}</div>}
    </div>
  );
}

export function Card({ title, actions, children, className, bodyClassName }: {
  title?: ReactNode;
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
  bodyClassName?: string;
}) {
  return (
    <section className={cx("rounded-xl border border-rule bg-surface", className)}>
      {(title || actions) && (
        <header className="flex items-center justify-between gap-2 border-b border-rule px-4 py-2.5">
          <h2 className="text-[13px] font-semibold">{title}</h2>
          {actions}
        </header>
      )}
      <div className={cx("p-4", bodyClassName)}>{children}</div>
    </section>
  );
}

type Tone = "neutral" | "accent" | "owe" | "warn";
const tones: Record<Tone, string> = {
  neutral: "bg-slate-100 text-slate-600",
  accent: "bg-accent-soft text-accent-strong",
  owe: "bg-owe-soft text-owe",
  warn: "bg-warn-soft text-warn",
};
export function Badge({ tone = "neutral", children }: { tone?: Tone; children: ReactNode }) {
  return (
    <span className={cx("inline-flex items-center rounded-full px-2 py-0.5 text-[11.5px] font-medium", tones[tone])}>
      {children}
    </span>
  );
}

export function StatusBadge({ status }: { status: DocumentStatus }) {
  return status === "Voided" ? <Badge tone="owe">Voided</Badge> : <Badge tone="accent">Posted</Badge>;
}

/** A signed amount: rust when negative (or when `owe`), ledger teal when `positive`. */
export function Amount({ value, tone, className }: { value: number; tone?: "auto" | "plain"; className?: string }) {
  const colored = tone === "auto";
  return (
    <span className={cx("tabular-nums", colored && value < 0 && "text-owe", colored && value > 0 && "text-accent-strong", className)}>
      {value < 0 ? `(${money(-value)})` : money(value)}
    </span>
  );
}

export function Spinner({ label = "Loading" }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-2 py-14 text-muted" role="status">
      <Loader2 className="size-4 animate-spin" /> {label}
    </div>
  );
}

export function EmptyState({ title, children, action }: { title: string; children?: ReactNode; action?: ReactNode }) {
  return (
    <div className="ledger-rules flex flex-col items-center justify-center gap-2 rounded-lg px-6 py-14 text-center">
      <p className="text-[14px] font-medium">{title}</p>
      {children && <p className="max-w-sm text-[13px] text-muted">{children}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}

export function ErrorNote({ error }: { error: { message: string } | null | undefined }) {
  if (!error) return null;
  return <div className="rounded-md border border-owe/25 bg-owe-soft px-3 py-2 text-[13px] text-owe">{error.message}</div>;
}

export function Pagination({ page, pageSize, total, onPage }: { page: number; pageSize: number; total: number; onPage: (p: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  if (total === 0) return null;
  const from = (page - 1) * pageSize + 1;
  const to = Math.min(total, page * pageSize);
  return (
    <div className="flex items-center justify-between border-t border-rule px-3 py-2 text-[12.5px] text-muted">
      <span>
        {from}–{to} of {total}
      </span>
      <div className="flex items-center gap-1">
        <Button size="sm" variant="ghost" disabled={page <= 1} onClick={() => onPage(page - 1)} aria-label="Previous page">
          <ChevronLeft className="size-3.5" />
        </Button>
        <span className="px-1 tabular-nums">
          {page} / {pages}
        </span>
        <Button size="sm" variant="ghost" disabled={page >= pages} onClick={() => onPage(page + 1)} aria-label="Next page">
          <ChevronRight className="size-3.5" />
        </Button>
      </div>
    </div>
  );
}

export function Tabs<T extends string>({ value, onChange, tabs }: { value: T; onChange: (v: T) => void; tabs: { value: T; label: string }[] }) {
  return (
    <div className="inline-flex rounded-lg border border-rule bg-slate-100/70 p-0.5" role="tablist">
      {tabs.map((t) => (
        <button
          key={t.value}
          role="tab"
          aria-selected={value === t.value}
          onClick={() => onChange(t.value)}
          className={cx(
            "relative rounded-md px-3 py-1 text-[12.5px] font-medium transition-colors",
            value === t.value ? "text-ink" : "text-muted hover:text-ink",
          )}
        >
          {value === t.value && (
            <motion.span layoutId={`tab-${tabs.map((x) => x.value).join("")}`} className="absolute inset-0 rounded-md border border-rule bg-surface" transition={{ type: "spring", stiffness: 500, damping: 40 }} />
          )}
          <span className="relative">{t.label}</span>
        </button>
      ))}
    </div>
  );
}

// ------------------------------------------------------------------ Modal
export function Modal({
  open,
  onClose,
  title,
  children,
  footer,
  width = "max-w-lg",
}: {
  open: boolean;
  onClose: () => void;
  title: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
  width?: string;
}) {
  const titleId = useId();
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, onClose]);

  return (
    <AnimatePresence>
      {open && (
        <div className="no-print fixed inset-0 z-40 flex items-start justify-center overflow-y-auto p-4 pt-[8vh]">
          <motion.div
            className="fixed inset-0 bg-slate-900/25 backdrop-blur-[2px]"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={onClose}
          />
          <motion.div
            role="dialog"
            aria-modal="true"
            aria-labelledby={titleId}
            initial={{ opacity: 0, y: 10, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 6, scale: 0.98 }}
            transition={{ type: "spring", stiffness: 420, damping: 34 }}
            className={cx("relative w-full rounded-xl border border-rule bg-surface shadow-[0_8px_30px_rgba(15,23,42,0.08)]", width)}
          >
            <header className="flex items-center justify-between border-b border-rule px-4 py-3">
              <h2 id={titleId} className="text-[14px] font-semibold">
                {title}
              </h2>
              <button onClick={onClose} className="rounded p-1 text-muted hover:bg-slate-100 hover:text-ink" aria-label="Close">
                <X className="size-4" />
              </button>
            </header>
            <div className="max-h-[70vh] overflow-y-auto p-4">{children}</div>
            {footer && <footer className="flex justify-end gap-2 border-t border-rule bg-slate-50/60 px-4 py-3">{footer}</footer>}
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  );
}

/** Void confirmation that requires a written reason (stored on the document for audit). */
export function VoidDialog({
  open,
  onClose,
  onConfirm,
  number,
  loading,
  consequence,
}: {
  open: boolean;
  onClose: () => void;
  onConfirm: (reason: string) => void;
  number: string;
  loading?: boolean;
  consequence: string;
}) {
  const [reason, setReason] = useState("");
  useEffect(() => {
    if (open) setReason("");
  }, [open]);
  return (
    <Modal
      open={open}
      onClose={onClose}
      title={`Void ${number}`}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Keep it
          </Button>
          <Button variant="danger" loading={loading} disabled={reason.trim().length < 3} onClick={() => onConfirm(reason.trim())}>
            Void {number}
          </Button>
        </>
      }
    >
      <p className="mb-3 text-[13px] text-slate-600">{consequence} The document stays in the records, marked voided.</p>
      <Field label="Reason">
        <Textarea autoFocus value={reason} onChange={(e) => setReason(e.target.value)} placeholder="e.g. Entered against the wrong customer" />
      </Field>
    </Modal>
  );
}

// ------------------------------------------------------------------ Combobox
export function Combobox<T>({
  items,
  loading,
  getKey,
  getLabel,
  renderItem,
  selectedLabel,
  onSearch,
  onSelect,
  placeholder,
  autoFocus,
  className,
}: {
  items: T[];
  loading?: boolean;
  getKey: (t: T) => string;
  getLabel: (t: T) => string;
  renderItem?: (t: T) => ReactNode;
  selectedLabel?: string;
  onSearch: (s: string) => void;
  onSelect: (t: T) => void;
  placeholder?: string;
  autoFocus?: boolean;
  className?: string;
}) {
  const [open, setOpen] = useState(false);
  const [text, setText] = useState(selectedLabel ?? "");
  const [active, setActive] = useState(0);
  const wrap = useRef<HTMLDivElement>(null);

  useEffect(() => setText(selectedLabel ?? ""), [selectedLabel]);
  useEffect(() => setActive(0), [items]);
  useEffect(() => {
    const onDoc = (e: MouseEvent) => {
      if (wrap.current && !wrap.current.contains(e.target as Node)) {
        setOpen(false);
        setText(selectedLabel ?? "");
      }
    };
    document.addEventListener("mousedown", onDoc);
    return () => document.removeEventListener("mousedown", onDoc);
  }, [selectedLabel]);

  const choose = (t: T) => {
    onSelect(t);
    setText(getLabel(t));
    setOpen(false);
  };

  return (
    <div ref={wrap} className={cx("relative", className)}>
      <input
        className="input"
        value={text}
        autoFocus={autoFocus}
        placeholder={placeholder}
        role="combobox"
        aria-expanded={open}
        onFocus={(e) => {
          e.target.select();
          setOpen(true);
          onSearch("");
        }}
        onChange={(e) => {
          setText(e.target.value);
          onSearch(e.target.value);
          setOpen(true);
        }}
        onKeyDown={(e) => {
          if (e.key === "ArrowDown") {
            e.preventDefault();
            setOpen(true);
            setActive((a) => Math.min(a + 1, items.length - 1));
          } else if (e.key === "ArrowUp") {
            e.preventDefault();
            setActive((a) => Math.max(a - 1, 0));
          } else if (e.key === "Enter" && open && items[active]) {
            e.preventDefault();
            choose(items[active]);
          } else if (e.key === "Escape") {
            setOpen(false);
            setText(selectedLabel ?? "");
          }
        }}
      />
      {open && (
        <ul className="absolute z-30 mt-1 max-h-64 w-full min-w-[260px] overflow-y-auto rounded-lg border border-rule bg-surface py-1 shadow-[0_8px_24px_rgba(15,23,42,0.08)]" role="listbox">
          {loading && items.length === 0 && <li className="px-3 py-2 text-muted">Searching…</li>}
          {!loading && items.length === 0 && <li className="px-3 py-2 text-muted">No matches</li>}
          {items.map((t, i) => (
            <li
              key={getKey(t)}
              role="option"
              aria-selected={i === active}
              onMouseEnter={() => setActive(i)}
              onMouseDown={(e) => {
                e.preventDefault();
                choose(t);
              }}
              className={cx("cursor-pointer px-3 py-1.5", i === active && "bg-accent-soft/60")}
            >
              {renderItem ? renderItem(t) : getLabel(t)}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
