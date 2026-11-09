import type { ReactNode } from "react";
import { Navigate, Route, Routes, useLocation } from "react-router";
import { useAuth } from "./lib/auth";
import AppShell from "./components/AppShell";
import { Spinner } from "./components/ui";
import LoginPage from "./pages/Login";
import DashboardPage from "./pages/Dashboard";
import SetupPage from "./pages/Setup";
import { SalesListPage, NewSalePage, SaleDetailPage, SaleReturnDetailPage } from "./pages/Sales";
import { PurchasesListPage, NewPurchasePage, PurchaseDetailPage, PurchaseReturnDetailPage } from "./pages/Purchases";
import VouchersPage from "./pages/Vouchers";
import { PartiesPage, PartyStatementPage } from "./pages/Parties";
import { ProductsPage, ProductDetailPage } from "./pages/Products";
import InventoryPage from "./pages/Inventory";
import { AccountsPage, AccountStatementPage } from "./pages/Accounts";
import ReportsPage from "./pages/Reports";
import SettingsPage from "./pages/Settings";

function RequireAuth({ children }: { children: ReactNode }) {
  const { user, ready } = useAuth();
  const location = useLocation();
  if (!ready) return <Spinner label="Signing you in" />;
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  return <>{children}</>;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        element={
          <RequireAuth>
            <AppShell />
          </RequireAuth>
        }
      >
        <Route index element={<DashboardPage />} />
        <Route path="setup" element={<SetupPage />} />
        <Route path="sales" element={<SalesListPage />} />
        <Route path="sales/new" element={<NewSalePage />} />
        <Route path="sales/returns/:id" element={<SaleReturnDetailPage />} />
        <Route path="sales/:id" element={<SaleDetailPage />} />
        <Route path="purchases" element={<PurchasesListPage />} />
        <Route path="purchases/new" element={<NewPurchasePage />} />
        <Route path="purchases/returns/:id" element={<PurchaseReturnDetailPage />} />
        <Route path="purchases/:id" element={<PurchaseDetailPage />} />
        <Route path="vouchers" element={<VouchersPage />} />
        <Route path="parties" element={<PartiesPage />} />
        <Route path="parties/:id" element={<PartyStatementPage />} />
        <Route path="products" element={<ProductsPage />} />
        <Route path="products/:id" element={<ProductDetailPage />} />
        <Route path="inventory" element={<InventoryPage />} />
        <Route path="accounts" element={<AccountsPage />} />
        <Route path="accounts/:id" element={<AccountStatementPage />} />
        <Route path="reports" element={<ReportsPage />} />
        <Route path="settings" element={<SettingsPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
