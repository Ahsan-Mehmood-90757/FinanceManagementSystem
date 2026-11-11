import { useState, type ReactNode } from "react";
import { Link } from "react-router";
import { AlertTriangle, CalendarClock, Plus } from "lucide-react";
import { useApi } from "../lib/hooks";
import { useAuth } from "../lib/auth";
import type { Dashboard } from "../lib/types";
import { cx, date, money, qty, rs, rsCompact } from "../lib/format";
import { Button, Card, ErrorNote, PageHeader, Spinner, StatusBadge, Table } from "../components/ui";
import CountUp from "../components/reactbits/CountUp";
import SpotlightCard from "../components/reactbits/SpotlightCard";

export default function DashboardPage() {
  const { data, isLoading, error } = useApi<Dashboard>("/reports/dashboard");
  const { user, can } = useAuth();

  if (isLoading) return <Spinner />;
  if (error || !data) return <ErrorNote error={error} />;

  const hour = new Date().getHours();
  const greeting = hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";

  return (
    <>
      <PageHeader
        title={`${greeting}, ${user?.fullName.split(" ")[0]}`}
        description={`Figures for ${date(data.today)}. Month-to-date covers the 1st until today.`}
        actions={
          can("post") && (
            <>
              <Link to="/purchases/new">
                <Button icon={<Plus className="size-3.5" />}>Purchase</Button>
              </Link>
              <Link to="/sales/new">
                <Button variant="primary" icon={<Plus className="size-3.5" />}>
                  New sale
                </Button>
              </Link>
            </>
          )
        }
      />

      <div className="grid gap-4 lg:grid-cols-[1.25fr_1fr]">
        <SpotlightCard className="p-5">
          <div className="text-[12.5px] text-muted">Cash and bank right now</div>
          <div className="mt-1 flex items-baseline gap-2">
            <span className="text-[15px] font-medium text-muted">Rs</span>
            <CountUp to={data.cashAndBank} decimals={2} className="text-[38px] font-semibold leading-none tracking-[-0.02em] tabular-nums" />
          </div>
          <ul className="mt-5 grid gap-x-6 gap-y-1.5 sm:grid-cols-2">
            {data.accounts.map((a) => (
              <li key={a.id} className="flex items-center justify-between border-b border-dashed border-rule py-1 text-[13px]">
                <Link to={`/accounts/${a.id}`} className="truncate text-ink-soft hover:text-accent-strong">
                  {a.name}
                </Link>
                <span className={cx("tabular-nums", a.balance < 0 && "text-owe")}>{money(a.balance)}</span>
              </li>
            ))}
            {data.accounts.length === 0 && <li className="text-muted">No accounts yet.</li>}
          </ul>
        </SpotlightCard>

        <div className="grid grid-cols-2 gap-px overflow-hidden rounded-xl border border-rule bg-rule">
          <Metric label="Sales this month" value={data.salesMonth} sub={`Today ${rs(data.salesToday)}`} />
          <Metric label="Gross profit" value={data.grossProfitMonth} sub={data.salesMonth ? `${((data.grossProfitMonth / data.salesMonth) * 100).toFixed(1)}% margin` : "No sales yet"} />
          <Metric label="Net profit" value={data.netProfitMonth} sub={`After ${rs(data.expensesMonth)} expenses`} tone={data.netProfitMonth < 0 ? "owe" : undefined} />
          <Metric label="Stock at cost" value={data.stockValue} sub="FIFO valuation" />
          <Metric label="Customers owe us" value={data.receivables} sub={<Link to="/reports?tab=balances" className="hover:underline">See who</Link>} tone="accent" />
          <Metric label="We owe suppliers" value={data.payables} sub={<Link to="/reports?tab=balances" className="hover:underline">See who</Link>} tone="owe" />
        </div>
      </div>

      {(data.lowStockCount > 0 || data.expiringSoonCount > 0) && (
        <div className="mt-4 flex flex-wrap gap-2">
          {data.lowStockCount > 0 && (
            <Link to="/products?lowStock=1" className="inline-flex items-center gap-2 rounded-lg border border-warn/25 bg-warn-soft px-3 py-1.5 text-[13px] text-warn hover:border-warn/50">
              <AlertTriangle className="size-4" /> {data.lowStockCount} product{data.lowStockCount > 1 ? "s" : ""} at or below reorder level
            </Link>
          )}
          {data.expiringSoonCount > 0 && (
            <Link to="/inventory?expiring=30" className="inline-flex items-center gap-2 rounded-lg border border-owe/20 bg-owe-soft px-3 py-1.5 text-[13px] text-owe hover:border-owe/40">
              <CalendarClock className="size-4" /> {data.expiringSoonCount} batch{data.expiringSoonCount > 1 ? "es" : ""} expire within 30 days
            </Link>
          )}
        </div>
      )}

      <div className="mt-4 grid gap-4 lg:grid-cols-[1.6fr_1fr]">
        <Card title="Last 30 days" actions={<Legend />}>
          <TrendChart points={data.trend} />
        </Card>
        <Card title="Best sellers this month" bodyClassName="p-0">
          {data.topProducts.length === 0 ? (
            <p className="p-4 text-[13px] text-muted">No sales this month yet.</p>
          ) : (
            <Table>
              <thead>
                <tr>
                  <th>Product</th>
                  <th className="num">Qty</th>
                  <th className="num">Revenue</th>
                  <th className="num">Profit</th>
                </tr>
              </thead>
              <tbody>
                {data.topProducts.map((p) => (
                  <tr key={p.productId}>
                    <td className="max-w-[160px] truncate">
                      <Link className="hover:text-accent-strong" to={`/products/${p.productId}`}>{p.name}</Link>
                    </td>
                    <td className="num">{qty(p.quantity)}</td>
                    <td className="num">{money(p.revenue)}</td>
                    <td className="num text-accent-strong">{money(p.grossProfit)}</td>
                  </tr>
                ))}
              </tbody>
            </Table>
          )}
        </Card>
      </div>

      <Card title="Recent activity" className="mt-4" bodyClassName="p-0">
        {data.recent.length === 0 ? (
          <p className="p-4 text-[13px] text-muted">Nothing posted yet. Your first sale or purchase will appear here.</p>
        ) : (
          <Table>
            <thead>
              <tr>
                <th>Document</th>
                <th>Date</th>
                <th>Party / category</th>
                <th>Status</th>
                <th className="num">Amount</th>
              </tr>
            </thead>
            <tbody>
              {data.recent.map((d) => (
                <tr key={d.id}>
                  <td>
                    <Link
                      className="font-medium hover:text-accent-strong"
                      to={d.kind === "sale" ? `/sales/${d.id}` : d.kind === "purchase" ? `/purchases/${d.id}` : `/vouchers?open=${d.id}`}
                    >
                      {d.number}
                    </Link>
                  </td>
                  <td>{date(d.date)}</td>
                  <td className="text-ink-soft">{d.counterparty ?? "—"}</td>
                  <td>
                    <StatusBadge status={d.status} />
                  </td>
                  <td className="num">{money(d.amount)}</td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Card>
    </>
  );
}

function Metric({ label, value, sub, tone }: { label: string; value: number; sub?: ReactNode; tone?: "accent" | "owe" }) {
  return (
    <div className="bg-surface px-4 py-3">
      <div className="text-[12px] text-muted">{label}</div>
      <div className={cx("mt-0.5 text-[18px] font-semibold tabular-nums tracking-[-0.01em]", tone === "owe" && "text-owe", tone === "accent" && "text-accent-strong")}>
        {money(value)}
      </div>
      {sub && <div className="mt-0.5 text-[12px] text-muted">{sub}</div>}
    </div>
  );
}

function Legend() {
  return (
    <div className="flex items-center gap-3 text-[12px] text-muted">
      <span className="inline-flex items-center gap-1.5">
        <span className="size-2 rounded-sm bg-rule-strong" /> Net sales
      </span>
      <span className="inline-flex items-center gap-1.5">
        <span className="size-2 rounded-sm bg-accent" /> Gross profit
      </span>
    </div>
  );
}

function TrendChart({ points }: { points: Dashboard["trend"] }) {
  const [hover, setHover] = useState<number | null>(null);
  const W = 640;
  const H = 180;
  const pad = { l: 44, r: 8, t: 8, b: 22 };
  const max = Math.max(1, ...points.map((p) => Math.max(p.sales, p.grossProfit)));
  const min = Math.min(0, ...points.map((p) => p.grossProfit));
  const range = max - min;
  const bw = (W - pad.l - pad.r) / points.length;
  const y = (v: number) => pad.t + ((max - v) / range) * (H - pad.t - pad.b);
  const ticks = [max, max / 2, 0];
  const h = hover !== null ? points[hover] : null;

  return (
    <div className="relative">
      <svg viewBox={`0 0 ${W} ${H}`} className="h-[200px] w-full" onMouseLeave={() => setHover(null)} role="img" aria-label="Daily net sales and gross profit for the last 30 days">
        {ticks.map((t) => (
          <g key={t}>
            <line x1={pad.l} x2={W - pad.r} y1={y(t)} y2={y(t)} className="stroke-rule" strokeDasharray={t === 0 ? undefined : "3 3"} />
            <text x={pad.l - 6} y={y(t) + 3} textAnchor="end" fontSize="10" className="fill-faint">
              {t === 0 ? "0" : rsCompact(t).replace("Rs ", "")}
            </text>
          </g>
        ))}
        {points.map((p, i) => {
          const x = pad.l + i * bw;
          return (
            <g key={p.date} onMouseEnter={() => setHover(i)}>
              <rect x={x} y={pad.t} width={bw} height={H - pad.t - pad.b} className={hover === i ? "fill-wash-strong" : "fill-transparent"} />
              <rect x={x + bw * 0.18} y={y(Math.max(p.sales, 0))} width={bw * 0.64} height={Math.max(0, y(0) - y(Math.max(p.sales, 0)))} rx={2} className="fill-rule-strong" />
              <rect x={x + bw * 0.34} y={y(Math.max(p.grossProfit, 0))} width={bw * 0.32} height={Math.abs(y(0) - y(p.grossProfit))} rx={1.5} className={p.grossProfit < 0 ? "fill-owe" : "fill-accent"} />
              {(i % 7 === 0 || i === points.length - 1) && (
                <text x={x + bw / 2} y={H - 6} textAnchor="middle" fontSize="10" className="fill-faint">
                  {date(p.date).slice(0, 6)}
                </text>
              )}
            </g>
          );
        })}
      </svg>
      {h && (
        <div className="pointer-events-none absolute right-2 top-0 rounded-md border border-rule bg-surface px-2.5 py-1.5 text-[12px]">
          <div className="font-medium">{date(h.date)}</div>
          <div className="tabular-nums text-ink-soft">Sales {money(h.sales)}</div>
          <div className="tabular-nums text-accent-strong">Profit {money(h.grossProfit)}</div>
        </div>
      )}
    </div>
  );
}
