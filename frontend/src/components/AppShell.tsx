import { useEffect, useState } from "react";
import { NavLink, Outlet, Link, useLocation } from "react-router";
import {
  BarChart3,
  Boxes,
  ClipboardList,
  LayoutGrid,
  LogOut,
  Menu,
  Package,
  Receipt,
  Settings,
  ShoppingCart,
  Truck,
  Users,
  Wallet,
  Rocket,
  X,
} from "lucide-react";
import { useAuth } from "../lib/auth";
import { useCompany } from "../lib/hooks";
import { cx } from "../lib/format";
import { ThemeToggle } from "./ui";

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
  // Below `lg` the sidebar is a drawer, opened from the top bar.
  const [menuOpen, setMenuOpen] = useState(false);

  useEffect(() => setMenuOpen(false), [location.pathname]);
  useEffect(() => {
    if (!menuOpen) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setMenuOpen(false);
    window.addEventListener("keydown", onKey);
    document.body.style.overflow = "hidden";
    return () => {
      window.removeEventListener("keydown", onKey);
      document.body.style.overflow = "";
    };
  }, [menuOpen]);

  const brand = (
    <Link to="/" className="flex min-w-0 items-center gap-2.5">
      <img src="/favicon.svg" alt="" className="size-7" />
      <div className="min-w-0">
        <div className="truncate text-[13.5px] font-semibold leading-4">{company?.companyName ?? "Finance"}</div>
        <div className="text-[11.5px] text-muted">Financial management</div>
      </div>
    </Link>
  );

  return (
    <div className="flex min-h-screen">
      {/* Drawer backdrop (phones and tablets only) */}
      <div
        aria-hidden
        onClick={() => setMenuOpen(false)}
        className={cx(
          "no-print fixed inset-0 z-40 bg-slate-950/30 backdrop-blur-[2px] transition-opacity duration-200 lg:hidden dark:bg-black/50",
          menuOpen ? "opacity-100" : "pointer-events-none opacity-0",
        )}
      />
      <aside
        className={cx(
          "no-print fixed inset-y-0 left-0 z-50 flex w-[264px] max-w-[85vw] shrink-0 flex-col border-r border-rule bg-surface transition-transform duration-200 ease-out",
          "lg:sticky lg:top-0 lg:z-auto lg:h-screen lg:w-[232px] lg:translate-x-0 lg:transition-none",
          menuOpen ? "translate-x-0 shadow-xl" : "-translate-x-full",
        )}
        aria-label="Main navigation"
      >
        <div className="flex items-center justify-between gap-2 px-4 pb-4 pt-5">
          {brand}
          <button onClick={() => setMenuOpen(false)} className="rounded-md p-1.5 text-muted hover:bg-wash-strong hover:text-ink lg:hidden" aria-label="Close menu">
            <X className="size-4" />
          </button>
        </div>

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
              <div className="px-2.5 pb-1 text-[11.5px] font-medium text-faint">{g.group}</div>
              <ul className="space-y-0.5">
                {g.items.map((item) => (
                  <li key={item.to}>
                    <NavLink
                      to={item.to}
                      end={item.end}
                      className={({ isActive }) =>
                        cx(
                          "relative flex items-center gap-2.5 rounded-md px-2.5 py-2 text-[13px] transition-colors lg:py-1.5",
                          isActive ? "bg-accent-soft/60 font-medium text-accent-strong" : "text-ink-soft hover:bg-wash-strong hover:text-ink",
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
          <div className="flex size-7 items-center justify-center rounded-full bg-wash-strong text-[12px] font-semibold text-ink-soft">
            {user?.fullName.slice(0, 1).toUpperCase()}
          </div>
          <div className="min-w-0 flex-1">
            <div className="truncate text-[12.5px] font-medium">{user?.fullName}</div>
            <div className="text-[11.5px] text-muted">{user?.role}</div>
          </div>
          <ThemeToggle />
          <button onClick={logout} className="grid size-8 place-items-center rounded-md text-muted hover:bg-wash-strong hover:text-ink" aria-label="Sign out" title="Sign out">
            <LogOut className="size-4" />
          </button>
        </div>
      </aside>

      <main className="min-w-0 flex-1">
        <header className="no-print sticky top-0 z-30 flex items-center gap-2 border-b border-rule bg-surface/90 px-3 py-2 backdrop-blur lg:hidden">
          <button
            onClick={() => setMenuOpen(true)}
            className="grid size-9 place-items-center rounded-md text-muted hover:bg-wash-strong hover:text-ink"
            aria-label="Open menu"
            aria-expanded={menuOpen}
          >
            <Menu className="size-5" />
          </button>
          <div className="min-w-0 flex-1">{brand}</div>
          <ThemeToggle className="size-9" />
        </header>
        {inSetup && location.pathname !== "/setup" && (
          <div className="no-print flex flex-wrap items-center gap-x-2 gap-y-1 border-b border-warn/20 bg-warn-soft px-4 py-2 text-[13px] text-warn sm:px-6">
            <ClipboardList className="size-4 shrink-0" />
            Posting is locked until opening balances are entered.
            <Link to="/setup" className="font-medium underline underline-offset-2">
              Finish Day-0 setup
            </Link>
          </div>
        )}
        <div className="mx-auto max-w-[1280px] px-4 py-5 sm:px-6 sm:py-6">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
