import { Link } from "react-router";
import { cx, date, documentHref, documentLabel, money } from "../lib/format";
import type { Statement } from "../lib/types";
import { EmptyState } from "./ui";

/** Ledger statement table shared by parties (debit/credit) and accounts (in/out). */
export function StatementView({
  statement,
  debitLabel,
  creditLabel,
  balanceFormatter = (n) => money(n),
}: {
  statement: Statement;
  debitLabel: string;
  creditLabel: string;
  balanceFormatter?: (n: number) => string;
}) {
  if (statement.lines.length === 0 && statement.openingBalance === 0)
    return <EmptyState title="No entries in this period">Change the dates to see older activity.</EmptyState>;

  return (
    <table className="table-dense w-full">
      <thead>
        <tr>
          <th className="w-[110px]">Date</th>
          <th className="w-[130px]">Document</th>
          <th>Description</th>
          <th className="num w-[130px]">{debitLabel}</th>
          <th className="num w-[130px]">{creditLabel}</th>
          <th className="num w-[150px]">Balance</th>
        </tr>
      </thead>
      <tbody>
        <tr className="bg-slate-50/60">
          <td colSpan={5} className="text-muted">
            {statement.from ? `Brought forward to ${date(statement.from)}` : "Opening"}
          </td>
          <td className="num font-medium">{balanceFormatter(statement.openingBalance)}</td>
        </tr>
        {statement.lines.map((l, i) => {
          const href = documentHref(l.documentType, l.documentId);
          return (
            <tr key={i} className={cx(l.isReversal && "text-muted")}>
              <td>{date(l.date)}</td>
              <td>
                {href ? (
                  <Link to={href} className="font-medium hover:text-accent-strong">
                    {l.documentNumber}
                  </Link>
                ) : (
                  <span className="font-medium">{l.documentNumber}</span>
                )}
                <div className="text-[11.5px] text-muted">{documentLabel[l.documentType]}</div>
              </td>
              <td className={cx(l.isReversal && "italic")}>{l.description}</td>
              <td className="num">{l.debit ? money(l.debit) : ""}</td>
              <td className="num">{l.credit ? money(l.credit) : ""}</td>
              <td className="num">{balanceFormatter(l.runningBalance)}</td>
            </tr>
          );
        })}
      </tbody>
      <tfoot>
        <tr className="font-semibold">
          <td colSpan={3} className="border-t border-rule px-3 py-2 text-right">
            Period totals and closing balance
          </td>
          <td className="num border-t border-rule px-3 py-2">{money(statement.totalDebit)}</td>
          <td className="num border-t border-rule px-3 py-2">{money(statement.totalCredit)}</td>
          <td className="num border-t border-rule px-3 py-2">{balanceFormatter(statement.closingBalance)}</td>
        </tr>
      </tfoot>
    </table>
  );
}
