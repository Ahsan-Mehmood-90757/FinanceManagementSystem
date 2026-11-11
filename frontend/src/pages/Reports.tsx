import { useState, type ReactNode } from "react";
import { Link, useSearchParams } from "react-router";
import { Printer } from "lucide-react";
import { useApi } from "../lib/hooks";
import { cx, date, money, monthStart, qty, today } from "../lib/format";
import type { Balances, ProfitLoss, Valuation } from "../lib/types";
import { Button, Card, DateRange, ErrorNote, PageHeader, Spinner, Table, Tabs } from "../components/ui";
import { Letterhead } from "../components/documents";
import { ExpiryCell } from "./Products";

type Tab = "pl" | "valuation" | "balances";

export default function ReportsPage() {
  const [params, setParams] = useSearchParams();
  const tab = (params.get("tab") as Tab) || "pl";
  return (
    <>
      <PageHeader
        title="Reports"
        actions={
          <Button icon={<Printer className="size-3.5" />} onClick={() => window.print()}>
            Print
          </Button>
        }
      />
      <div className="no-print mb-4">
        <Tabs<Tab>
          value={tab}
          onChange={(v) => setParams({ tab: v })}
          tabs={[
            { value: "pl", label: "Profit & loss" },
            { value: "valuation", label: "Inventory valuation" },
            { value: "balances", label: "Receivables & payables" },
          ]}
        />
      </div>
      {tab === "pl" && <ProfitLossReport />}
      {tab === "valuation" && <ValuationReport />}
      {tab === "balances" && <BalancesReport />}
    </>
  );
}

function ProfitLossReport() {
  const [from, setFrom] = useState(monthStart());
  const [to, setTo] = useState(today());
  const { data, isLoading, error } = useApi<ProfitLoss>("/reports/profit-loss", { from, to }, { keepPrevious: true });

  return (
    <>
      <div className="no-print mb-3 flex flex-wrap items-center gap-2">
        <DateRange from={from} to={to} onFrom={setFrom} onTo={setTo} />
      </div>
      <ErrorNote error={error} />
      {isLoading || !data ? (
        <Spinner />
      ) : (
        <Card className="print-area max-w-3xl" bodyClassName="p-4 sm:p-6">
          <div className="print-only">
            <Letterhead title="Profit & loss" number={`${date(data.from)} to ${date(data.to)}`} />
          </div>
          <Section title="Trading">
            <Line label="Sales" value={data.grossSales} />
            <Line label="Less sales returns" value={-data.salesReturns} />
            <Line label="Net sales" value={data.netSales} strong />
            <Line label="Cost of goods sold (FIFO)" value={-data.costOfGoodsSold} />
            <Line label="Add back cost of returned goods" value={data.costReversedOnReturns} />
            <Line label="Gross profit" value={data.grossProfit} strong hint={`${data.grossMarginPercent}% of net sales`} />
          </Section>
          <Section title="Operating costs attached to bills">
            {data.billCharges.length === 0 && <Empty />}
            {data.billCharges.map((c) => (
              <Line key={c.name} label={c.name} value={-c.amount} />
            ))}
            <Line label="Total bill charges" value={-data.billChargesTotal} strong />
          </Section>
          <Section title="Standalone expenses">
            {data.operatingExpenses.length === 0 && <Empty />}
            {data.operatingExpenses.map((c) => (
              <Line key={c.name} label={c.name} value={-c.amount} />
            ))}
            <Line label="Stock written off" value={-data.stockWriteOffs} />
            <Line label="Total expenses" value={-(data.operatingExpensesTotal + data.stockWriteOffs)} strong />
          </Section>
          <div className="mt-4 flex items-baseline justify-between rounded-lg border border-rule bg-wash px-4 py-3">
            <div>
              <div className="font-semibold">Net profit</div>
              <div className="text-[12px] text-muted">{data.netMarginPercent}% of net sales</div>
            </div>
            <div className={cx("text-[22px] font-semibold tabular-nums", data.netProfit < 0 ? "text-owe" : "text-accent-strong")}>
              {data.netProfit < 0 ? `(${money(-data.netProfit)})` : money(data.netProfit)}
            </div>
          </div>
          <p className="mt-3 text-[12px] text-muted">Voided documents are excluded. Bill charges are kept out of gross margin and charged here.</p>
        </Card>
      )}
    </>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="mb-5">
      <h3 className="mb-1.5 border-b border-rule pb-1.5 text-[13px] font-semibold">{title}</h3>
      <dl>{children}</dl>
    </section>
  );
}
function Line({ label, value, strong, hint }: { label: string; value: number; strong?: boolean; hint?: string }) {
  return (
    <div className={cx("flex items-baseline justify-between py-1 text-[13px]", strong && "mt-1 border-t border-dashed border-rule pt-2 font-semibold")}>
      <dt>
        {label}
        {hint && <span className="ml-2 text-[12px] font-normal text-muted">{hint}</span>}
      </dt>
      <dd className="tabular-nums">{value < 0 ? `(${money(-value)})` : money(value)}</dd>
    </div>
  );
}
const Empty = () => <p className="py-1 text-[12.5px] text-muted">None in this period.</p>;

function ValuationReport() {
  const { data, isLoading, error } = useApi<Valuation>("/reports/inventory-valuation");
  if (isLoading) return <Spinner />;
  if (error || !data) return <ErrorNote error={error} />;
  return (
    <Card
      className="print-area"
      bodyClassName="p-0"
      title={`Stock at FIFO cost: Rs ${money(data.totalValue)}`}
      actions={<span className="text-[12px] text-muted">{data.rows.length} products in stock</span>}
    >
      <Table>
        <thead>
          <tr>
            <th>SKU</th>
            <th>Product</th>
            <th>Category</th>
            <th className="num">Batches</th>
            <th>Earliest expiry</th>
            <th className="num">Quantity</th>
            <th className="num">Avg cost</th>
            <th className="num">Value</th>
          </tr>
        </thead>
        <tbody>
          {data.rows.map((r) => (
            <tr key={r.productId}>
              <td className="text-muted">{r.sku}</td>
              <td>
                <Link to={`/products/${r.productId}`} className="font-medium hover:text-accent-strong">
                  {r.name}
                </Link>
              </td>
              <td className="text-ink-soft">{r.category ?? ""}</td>
              <td className="num">{r.batchCount}</td>
              <td>
                <ExpiryCell date={r.earliestExpiry} />
              </td>
              <td className="num">
                {qty(r.quantity)} {r.unitSymbol}
              </td>
              <td className="num">{money(r.averageCost)}</td>
              <td className="num font-medium">{money(r.value)}</td>
            </tr>
          ))}
        </tbody>
      </Table>
    </Card>
  );
}

function BalancesReport() {
  const { data, isLoading, error } = useApi<Balances>("/reports/balances");
  if (isLoading) return <Spinner />;
  if (error || !data) return <ErrorNote error={error} />;
  const table = (rows: Balances["receivables"], tone: "accent" | "owe") =>
    rows.length === 0 ? (
      <p className="p-4 text-[13px] text-muted">Nothing outstanding.</p>
    ) : (
      <Table>
        <thead>
          <tr>
            <th>Party</th>
            <th>Phone</th>
            <th className="num">Amount</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.id}>
              <td>
                <Link to={`/parties/${r.id}`} className="font-medium hover:text-accent-strong">
                  {r.name}
                </Link>
                <span className="ml-1.5 text-[11.5px] text-muted">{r.code}</span>
              </td>
              <td className="text-ink-soft">{r.phone ?? ""}</td>
              <td className={cx("num", tone === "owe" ? "text-owe" : "text-accent-strong")}>{money(r.balance)}</td>
            </tr>
          ))}
        </tbody>
      </Table>
    );
  return (
    <div className="print-area grid gap-4 lg:grid-cols-2">
      <Card title={`They owe us: Rs ${money(data.totalReceivable)}`} bodyClassName="p-0">
        {table(data.receivables, "accent")}
      </Card>
      <Card title={`We owe them: Rs ${money(data.totalPayable)}`} bodyClassName="p-0">
        {table(data.payables, "owe")}
      </Card>
    </div>
  );
}
