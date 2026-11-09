import { NavLink, Outlet, Link, useLocation } from "react-router";
import {
  BarChart3,
  Boxes,
  ClipboardList,
  LayoutGrid,
  LogOut,
  Package,
  Receipt,
  Settings,
  ShoppingCart,
  Truck,
  Users,
  Wallet,
  Rocket,
} from "lucide-react";
import { useAuth } from "../lib/auth";
import { useCompany } from "../lib/hooks";
import { cx } from "../lib/format";

const nav = [
  {
    group: "Daily work",
    items: [
      { to: "/", label: "Dashboard", icon: LayoutGrid, end: true },
      { to: "/sales", label: "Sales", icon: ShoppingCart },
      { to: "/purchases", label: "Purchases", icon: Truck },
      { to: "/vouchers", label: "Payments & expenses", icon: Receipt },
    ],
  },
  {
    group: "Records",
    items: [
      { to: "/parties", label: "Customers & suppliers", icon: Users },
      { to: "/products", label: "Products", icon: Package },
      { to: "/inventory", label: "Stock batches", icon: Boxes },
      { to: "/accounts", label: "Cash & bank", icon: Wallet },
    ],
  },
  {
    group: "Insight",
    items: [
      { to: "/reports", label: "Reports", icon: BarChart3 },
      { to: "/settings", label: "Settings", icon: Settings },
    ],
  },
];

export default function AppShell() {
  const { user, logout } = useAuth();
  const { data: company } = useCompany();
  const location = useLocation();
  const inSetup = company && !company.isOperational;

  return (
    <div className="flex min-h-screen">
      <aside className="no-print sticky top-0 flex h-screen w-[232px] shrink-0 flex-col border-r border-rule bg-surface">
        <Link to="/" className="flex items-center gap-2.5 px-4 pb-4 pt-5">
          <img src="/favicon.svg" alt="" className="size-7" />
          <div className="min-w-0">
            <div className="truncate text-[13.5px] font-semibold leading-4">{company?.companyName ?? "Finance"}</div>
            <div className="text-[11.5px] text-muted">Financial management</div>
          </div>
        </Link>

        <nav className="flex-1 space-y-4 overflow-y-auto px-2.5 pb-4">
          {inSetup && (
            <NavLink
              to="/setup"
              className={({ isActive }) =>
                cx(
                  "flex items-center gap-2.5 rounded-md border border-warn/25 bg-warn-soft px-2.5 py-1.5 text-[13px] font-medium text-warn",
                  isActive && "ring-1 ring-warn/40",
                )
              }
            >
              <Rocket className="size-4" /> Finish Day-0 setup
            </NavLink>
          )}
          {nav.map((g) => (
            <div key={g.group}>
              <div className="px-2.5 pb-1 text-[11.5px] font-medium text-slate-400">{g.group}</div>
              <ul className="space-y-0.5">
                {g.items.map((item) => (
                  <li key={item.to}>
                    <NavLink
                      to={item.to}
                      end={item.end}
                      className={({ isActive }) =>
                        cx(
                          "relative flex items-center gap-2.5 rounded-md px-2.5 py-1.5 text-[13px] transition-colors",
                          isActive ? "bg-accent-soft/60 font-medium text-accent-strong" : "text-slate-600 hover:bg-slate-100 hover:text-ink",
                        )
                      }
                    >
                      <item.icon className="size-4 shrink-0" strokeWidth={1.8} />
                      {item.label}
                    </NavLink>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </nav>

        <div className="flex items-center gap-2 border-t border-rule px-3 py-3">
          <div className="flex size-7 items-center justify-center rounded-full bg-slate-100 text-[12px] font-semibold text-slate-600">
            {user?.fullName.slice(0, 1).toUpperCase()}
          </div>
          <div className="min-w-0 flex-1">
            <div className="truncate text-[12.5px] font-medium">{user?.fullName}</div>
            <div className="text-[11.5px] text-muted">{user?.role}</div>
          </div>
          <button onClick={logout} className="rounded p-1.5 text-muted hover:bg-slate-100 hover:text-ink" aria-label="Sign out" title="Sign out">
            <LogOut className="size-4" />
          </button>
        </div>
      </aside>

      <main className="min-w-0 flex-1">
        {inSetup && location.pathname !== "/setup" && (
          <div className="no-print flex items-center gap-2 border-b border-warn/20 bg-warn-soft px-6 py-2 text-[13px] text-warn">
            <ClipboardList className="size-4" />
            Posting is locked until opening balances are entered.
            <Link to="/setup" className="font-medium underline underline-offset-2">
              Finish Day-0 setup
            </Link>
          </div>
        )}
        <div className="mx-auto max-w-[1280px] px-6 py-6">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
