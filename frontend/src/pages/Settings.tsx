import { useEffect, useState, type ReactNode } from "react";
import { useSearchParams } from "react-router";
import { Pencil, Plus, Trash2 } from "lucide-react";
import { api } from "../lib/api";
import { useAction, useApi, useCategories, useCompany, useExpenseCategories, useUnits } from "../lib/hooks";
import { useAuth } from "../lib/auth";
import { dateTime } from "../lib/format";
import type { Category, ExpenseCategory, Unit, User, UserRole } from "../lib/types";
import { Badge, Button, Card, Checkbox, Field, Input, Modal, PageHeader, Select, Spinner, Table, Tabs, Textarea } from "../components/ui";

type Tab = "company" | "units" | "categories" | "expenses" | "users" | "password";

export default function SettingsPage() {
  const { can } = useAuth();
  const [params, setParams] = useSearchParams();
  const tab = (params.get("tab") as Tab) || "company";
  const tabs: { value: Tab; label: string }[] = [
    { value: "company", label: "Company" },
    { value: "units", label: "Units" },
    { value: "categories", label: "Product categories" },
    { value: "expenses", label: "Expense categories" },
    ...(can("users") ? [{ value: "users" as Tab, label: "Users" }] : []),
    { value: "password", label: "My password" },
  ];
  return (
    <>
      <PageHeader title="Settings" />
      <div className="mb-4">
        <Tabs<Tab> value={tab} onChange={(v) => setParams({ tab: v })} tabs={tabs} />
      </div>
      {tab === "company" && <CompanySettings />}
      {tab === "units" && <UnitsSettings />}
      {tab === "categories" && <CategorySettings />}
      {tab === "expenses" && <ExpenseSettings />}
      {tab === "users" && can("users") && <UserSettings />}
      {tab === "password" && <PasswordSettings />}
    </>
  );
}

function CompanySettings() {
  const { can } = useAuth();
  const { data } = useCompany();
  const [form, setForm] = useState({ companyName: "", address: "", phone: "", email: "", taxNumber: "", invoiceFooter: "" });
  useEffect(() => {
    if (data)
      setForm({
        companyName: data.companyName,
        address: data.address ?? "",
        phone: data.phone ?? "",
        email: data.email ?? "",
        taxNumber: data.taxNumber ?? "",
        invoiceFooter: data.invoiceFooter ?? "",
      });
  }, [data]);
  const save = useAction(() => api.put("/setup/company", { ...form, goLiveDate: null }), { success: "Company details saved" });
  if (!data) return <Spinner />;
  const editable = can("setup");
  const set = (p: Partial<typeof form>) => setForm((f) => ({ ...f, ...p }));
  return (
    <Card title="Shown on printed invoices and statements" className="max-w-3xl">
      <div className="grid gap-3 md:grid-cols-2">
        <Field label="Company name" className="md:col-span-2">
          <Input value={form.companyName} disabled={!editable} onChange={(e) => set({ companyName: e.target.value })} />
        </Field>
        <Field label="Phone">
          <Input value={form.phone} disabled={!editable} onChange={(e) => set({ phone: e.target.value })} />
        </Field>
        <Field label="Email">
          <Input value={form.email} disabled={!editable} onChange={(e) => set({ email: e.target.value })} />
        </Field>
        <Field label="Address" className="md:col-span-2">
          <Textarea value={form.address} disabled={!editable} rows={2} onChange={(e) => set({ address: e.target.value })} />
        </Field>
        <Field label="NTN / STRN">
          <Input value={form.taxNumber} disabled={!editable} onChange={(e) => set({ taxNumber: e.target.value })} />
        </Field>
        <Field label="Invoice footer">
          <Input value={form.invoiceFooter} disabled={!editable} onChange={(e) => set({ invoiceFooter: e.target.value })} />
        </Field>
      </div>
      {editable && (
        <Button className="mt-4" variant="primary" loading={save.isPending} onClick={() => save.mutate(undefined)}>
          Save company details
        </Button>
      )}
    </Card>
  );
}

// ---------------------------------------------------------------- generic small CRUD list
function CrudCard<T extends { id: string }>({
  title,
  items,
  columns,
  onAdd,
  onEdit,
  onDelete,
}: {
  title: string;
  items: T[] | undefined;
  columns: { label: string; render: (t: T) => ReactNode; className?: string }[];
  onAdd: () => void;
  onEdit: (t: T) => void;
  onDelete?: (t: T) => void;
}) {
  const { can } = useAuth();
  return (
    <Card
      title={title}
      className="max-w-3xl"
      bodyClassName="p-0"
      actions={
        can("master") && (
          <Button size="sm" icon={<Plus className="size-3.5" />} onClick={onAdd}>
            Add
          </Button>
        )
      }
    >
      {!items ? (
        <Spinner />
      ) : (
        <Table>
          <thead>
            <tr>
              {columns.map((c) => (
                <th key={c.label} className={c.className}>
                  {c.label}
                </th>
              ))}
              <th className="w-16" />
            </tr>
          </thead>
          <tbody>
            {items.map((t) => (
              <tr key={t.id}>
                {columns.map((c) => (
                  <td key={c.label} className={c.className}>
                    {c.render(t)}
                  </td>
                ))}
                <td className="text-right">
                  {can("master") && (
                    <div className="flex justify-end gap-0.5">
                      <button aria-label="Edit" className="rounded-md p-1 text-muted hover:bg-wash-strong hover:text-ink" onClick={() => onEdit(t)}>
                        <Pencil className="size-3.5" />
                      </button>
                      {onDelete && (
                        <button aria-label="Delete" className="rounded-md p-1 text-muted hover:bg-owe-soft hover:text-owe" onClick={() => onDelete(t)}>
                          <Trash2 className="size-3.5" />
                        </button>
                      )}
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
    </Card>
  );
}

function FormModal({ open, title, onClose, onSave, saving, valid, children }: { open: boolean; title: string; onClose: () => void; onSave: () => void; saving: boolean; valid: boolean; children: ReactNode }) {
  return (
    <Modal
      open={open}
      onClose={onClose}
      title={title}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" disabled={!valid} loading={saving} onClick={onSave}>
            Save
          </Button>
        </>
      }
    >
      <div className="grid gap-3">{children}</div>
    </Modal>
  );
}

function UnitsSettings() {
  const { data } = useUnits();
  const [edit, setEdit] = useState<Unit | "new" | null>(null);
  const [form, setForm] = useState({ name: "", symbol: "", allowsFractions: false });
  useEffect(() => {
    if (edit) setForm(edit === "new" ? { name: "", symbol: "", allowsFractions: false } : { name: edit.name, symbol: edit.symbol, allowsFractions: edit.allowsFractions });
  }, [edit]);
  const save = useAction(() => (edit === "new" ? api.post("/units", form) : api.put(`/units/${(edit as Unit).id}`, form)), { success: "Unit saved", onSuccess: () => setEdit(null) });
  const del = useAction((id: string) => api.del(`/units/${id}`), { success: "Unit deleted" });
  return (
    <>
      <CrudCard<Unit>
        title="Units of measure"
        items={data}
        onAdd={() => setEdit("new")}
        onEdit={setEdit}
        onDelete={(u) => del.mutate(u.id)}
        columns={[
          { label: "Name", render: (u) => <span className="font-medium">{u.name}</span> },
          { label: "Symbol", render: (u) => u.symbol },
          { label: "Quantities", render: (u) => (u.allowsFractions ? "Fractions allowed (e.g. 0.3)" : "Whole numbers only") },
          { label: "Products", render: (u) => u.productCount, className: "num" },
        ]}
      />
      <FormModal open={!!edit} title={edit === "new" ? "Add unit" : "Edit unit"} onClose={() => setEdit(null)} onSave={() => save.mutate(undefined)} saving={save.isPending} valid={!!form.name.trim() && !!form.symbol.trim()}>
        <Field label="Name">
          <Input autoFocus value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="Litre" />
        </Field>
        <Field label="Symbol">
          <Input value={form.symbol} onChange={(e) => setForm({ ...form, symbol: e.target.value })} placeholder="L" />
        </Field>
        <Checkbox label="Allow fractional quantities" checked={form.allowsFractions} onChange={(v) => setForm({ ...form, allowsFractions: v })} />
      </FormModal>
    </>
  );
}

function CategorySettings() {
  const { data } = useCategories();
  const [edit, setEdit] = useState<Category | "new" | null>(null);
  const [form, setForm] = useState({ name: "", parentId: "" });
  useEffect(() => {
    if (edit) setForm(edit === "new" ? { name: "", parentId: "" } : { name: edit.name, parentId: edit.parentId ?? "" });
  }, [edit]);
  const body = () => ({ name: form.name, parentId: form.parentId || null });
  const save = useAction(() => (edit === "new" ? api.post("/categories", body()) : api.put(`/categories/${(edit as Category).id}`, body())), {
    success: "Category saved",
    onSuccess: () => setEdit(null),
  });
  const del = useAction((id: string) => api.del(`/categories/${id}`), { success: "Category deleted" });
  const parents = data?.filter((c) => !c.parentId && (edit === "new" || c.id !== (edit as Category | null)?.id)) ?? [];
  return (
    <>
      <CrudCard<Category>
        title="Categories and subcategories"
        items={data}
        onAdd={() => setEdit("new")}
        onEdit={setEdit}
        onDelete={(c) => del.mutate(c.id)}
        columns={[
          { label: "Name", render: (c) => (c.parentId ? <span className="pl-5 text-ink-soft">{c.name}</span> : <span className="font-medium">{c.name}</span>) },
          { label: "Parent", render: (c) => c.parentName ?? "" },
          { label: "Products", render: (c) => c.productCount, className: "num" },
        ]}
      />
      <FormModal open={!!edit} title={edit === "new" ? "Add category" : "Edit category"} onClose={() => setEdit(null)} onSave={() => save.mutate(undefined)} saving={save.isPending} valid={!!form.name.trim()}>
        <Field label="Name">
          <Input autoFocus value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
        </Field>
        <Field label="Parent category" hint="Leave as top level, or nest one level under another category">
          <Select value={form.parentId} onChange={(e) => setForm({ ...form, parentId: e.target.value })}>
            <option value="">Top level</option>
            {parents.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </Select>
        </Field>
      </FormModal>
    </>
  );
}

function ExpenseSettings() {
  const { data } = useExpenseCategories(false);
  const [edit, setEdit] = useState<ExpenseCategory | "new" | null>(null);
  const [form, setForm] = useState({ name: "", description: "", isActive: true });
  useEffect(() => {
    if (edit) setForm(edit === "new" ? { name: "", description: "", isActive: true } : { name: edit.name, description: edit.description ?? "", isActive: edit.isActive });
  }, [edit]);
  const save = useAction(() => (edit === "new" ? api.post("/expense-categories", form) : api.put(`/expense-categories/${(edit as ExpenseCategory).id}`, form)), {
    success: "Expense category saved",
    onSuccess: () => setEdit(null),
  });
  const del = useAction((id: string) => api.del(`/expense-categories/${id}`), { success: "Expense category deleted" });
  return (
    <>
      <CrudCard<ExpenseCategory>
        title="Expense categories"
        items={data}
        onAdd={() => setEdit("new")}
        onEdit={setEdit}
        onDelete={(c) => del.mutate(c.id)}
        columns={[
          { label: "Name", render: (c) => <span className="font-medium">{c.name}</span> },
          { label: "Description", render: (c) => <span className="text-ink-soft">{c.description ?? ""}</span> },
          { label: "Status", render: (c) => (c.isActive ? <Badge tone="accent">Active</Badge> : <Badge>Inactive</Badge>) },
        ]}
      />
      <FormModal open={!!edit} title={edit === "new" ? "Add expense category" : "Edit expense category"} onClose={() => setEdit(null)} onSave={() => save.mutate(undefined)} saving={save.isPending} valid={!!form.name.trim()}>
        <Field label="Name">
          <Input autoFocus value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
        </Field>
        <Field label="Description">
          <Input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
        </Field>
        {edit !== "new" && <Checkbox label="Active" checked={form.isActive} onChange={(v) => setForm({ ...form, isActive: v })} />}
      </FormModal>
    </>
  );
}

const roleHelp: Record<UserRole, string> = {
  Owner: "Everything, including users",
  Admin: "Everything except users",
  Accountant: "Post documents and edit records; cannot void",
  Viewer: "Read only",
};

function UserSettings() {
  const { user: me } = useAuth();
  const { data } = useApi<User[]>("/users");
  const [edit, setEdit] = useState<User | "new" | null>(null);
  const [reset, setReset] = useState<User | null>(null);
  const [form, setForm] = useState({ username: "", fullName: "", role: "Accountant" as UserRole, password: "", isActive: true });
  const [newPassword, setNewPassword] = useState("");
  useEffect(() => {
    if (edit)
      setForm(edit === "new" ? { username: "", fullName: "", role: "Accountant", password: "", isActive: true } : { username: edit.username, fullName: edit.fullName, role: edit.role, password: "", isActive: edit.isActive });
  }, [edit]);
  const save = useAction(
    () =>
      edit === "new"
        ? api.post("/users", { username: form.username, fullName: form.fullName, role: form.role, password: form.password })
        : api.put(`/users/${(edit as User).id}`, { fullName: form.fullName, role: form.role, isActive: form.isActive }),
    { success: "User saved", onSuccess: () => setEdit(null) },
  );
  const resetAction = useAction(() => api.post(`/users/${reset!.id}/reset-password`, { newPassword }), {
    success: "Password reset",
    onSuccess: () => {
      setReset(null);
      setNewPassword("");
    },
  });

  return (
    <>
      <Card
        title="People who can sign in"
        className="max-w-4xl"
        bodyClassName="p-0"
        actions={
          <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEdit("new")}>
            Add user
          </Button>
        }
      >
        {!data ? (
          <Spinner />
        ) : (
          <Table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Username</th>
                <th>Role</th>
                <th>Last sign-in</th>
                <th className="w-[170px]" />
              </tr>
            </thead>
            <tbody>
              {data.map((u) => (
                <tr key={u.id} className={u.isActive ? "" : "text-muted"}>
                  <td className="font-medium">
                    {u.fullName} {u.id === me?.id && <Badge>You</Badge>} {!u.isActive && <Badge>Inactive</Badge>}
                  </td>
                  <td>{u.username}</td>
                  <td>
                    <Badge tone={u.role === "Owner" ? "accent" : "neutral"}>{u.role}</Badge>
                  </td>
                  <td className="text-muted">{u.lastLoginAt ? dateTime(u.lastLoginAt) : "Never"}</td>
                  <td className="text-right">
                    <Button size="sm" variant="ghost" onClick={() => setEdit(u)}>
                      Edit
                    </Button>
                    <Button size="sm" variant="ghost" onClick={() => setReset(u)}>
                      Reset password
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Card>

      <FormModal
        open={!!edit}
        title={edit === "new" ? "Add user" : "Edit user"}
        onClose={() => setEdit(null)}
        onSave={() => save.mutate(undefined)}
        saving={save.isPending}
        valid={!!form.fullName.trim() && (edit !== "new" || (!!form.username.trim() && form.password.length >= 8))}
      >
        <Field label="Full name">
          <Input autoFocus value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} />
        </Field>
        <Field label="Username">
          <Input value={form.username} disabled={edit !== "new"} onChange={(e) => setForm({ ...form, username: e.target.value })} />
        </Field>
        <Field label="Role" hint={roleHelp[form.role]}>
          <Select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value as UserRole })}>
            {(Object.keys(roleHelp) as UserRole[]).map((r) => (
              <option key={r} value={r}>
                {r}
              </option>
            ))}
          </Select>
        </Field>
        {edit === "new" ? (
          <Field label="Temporary password" hint="At least 8 characters. Ask them to change it after signing in.">
            <Input type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} />
          </Field>
        ) : (
          <Checkbox label="Active (inactive users cannot sign in)" checked={form.isActive} onChange={(v) => setForm({ ...form, isActive: v })} />
        )}
      </FormModal>

      <FormModal open={!!reset} title={`Reset password for ${reset?.fullName ?? ""}`} onClose={() => setReset(null)} onSave={() => resetAction.mutate(undefined)} saving={resetAction.isPending} valid={newPassword.length >= 8}>
        <Field label="New password" hint="At least 8 characters">
          <Input type="password" autoFocus value={newPassword} onChange={(e) => setNewPassword(e.target.value)} />
        </Field>
      </FormModal>
    </>
  );
}

function PasswordSettings() {
  const [form, setForm] = useState({ currentPassword: "", newPassword: "", confirm: "" });
  const save = useAction(() => api.post("/auth/change-password", { currentPassword: form.currentPassword, newPassword: form.newPassword }), {
    success: "Password changed",
    onSuccess: () => setForm({ currentPassword: "", newPassword: "", confirm: "" }),
  });
  const mismatch = form.confirm.length > 0 && form.confirm !== form.newPassword;
  return (
    <Card title="Change your password" className="max-w-md">
      <div className="grid gap-3">
        <Field label="Current password">
          <Input type="password" autoComplete="current-password" value={form.currentPassword} onChange={(e) => setForm({ ...form, currentPassword: e.target.value })} />
        </Field>
        <Field label="New password" hint="At least 8 characters">
          <Input type="password" autoComplete="new-password" value={form.newPassword} onChange={(e) => setForm({ ...form, newPassword: e.target.value })} />
        </Field>
        <Field label="Confirm new password" error={mismatch ? "Passwords don't match" : undefined}>
          <Input type="password" autoComplete="new-password" value={form.confirm} onChange={(e) => setForm({ ...form, confirm: e.target.value })} />
        </Field>
        <div>
          <Button variant="primary" disabled={!form.currentPassword || form.newPassword.length < 8 || mismatch || !form.confirm} loading={save.isPending} onClick={() => save.mutate(undefined)}>
            Change password
          </Button>
        </div>
      </div>
    </Card>
  );
}
