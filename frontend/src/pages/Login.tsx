import { useState, type FormEvent } from "react";
import { Navigate, useLocation, useNavigate } from "react-router";
import { useAuth } from "../lib/auth";
import { Button, ErrorNote, Field, Input } from "../components/ui";
import Squares from "../components/reactbits/Squares";
import BlurText from "../components/reactbits/BlurText";
import ShinyText from "../components/reactbits/ShinyText";

export default function LoginPage() {
  const { user, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<Error | null>(null);
  const [busy, setBusy] = useState(false);

  if (user) return <Navigate to="/" replace />;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await login(username, password);
      const from = (location.state as { from?: string } | null)?.from ?? "/";
      navigate(from, { replace: true });
    } catch (err) {
      setError(err as Error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="grid min-h-screen lg:grid-cols-[1.15fr_1fr]">
      <section className="relative hidden overflow-hidden border-r border-rule bg-paper lg:block">
        <div className="absolute inset-0">
          <Squares direction="diagonal" speed={0.18} squareSize={34} />
        </div>
        <div className="relative flex h-full flex-col justify-between p-12">
          <div className="flex items-center gap-2.5">
            <img src="/favicon.svg" alt="" className="size-8" />
            <span className="text-[14px] font-semibold">Financial Management System</span>
          </div>
          <div className="max-w-[30rem]">
            <BlurText
              text="Stock, cash and every party balance in one ledger."
              className="text-[40px] font-semibold leading-[1.1] tracking-[-0.02em] text-ink"
            />
            <p className="mt-5 max-w-[26rem] text-[15px] leading-6 text-slate-600">
              Purchases build FIFO batches, sales draw from the oldest stock first, and every rupee that moves
              leaves a ledger line behind it.
            </p>
          </div>
          <dl className="grid max-w-[30rem] grid-cols-3 gap-6 border-t border-rule pt-6 text-[12.5px]">
            <div>
              <dt className="text-muted">Valuation</dt>
              <dd className="mt-1 font-medium">Perpetual FIFO</dd>
            </div>
            <div>
              <dt className="text-muted">Currency</dt>
              <dd className="mt-1 font-medium">Pakistani rupee</dd>
            </div>
            <div>
              <dt className="text-muted">Records</dt>
              <dd className="mt-1 font-medium">Posted, never edited</dd>
            </div>
          </dl>
        </div>
      </section>

      <section className="flex items-center justify-center bg-surface px-6 py-12">
        <form onSubmit={submit} className="w-full max-w-[340px] space-y-4">
          <div className="mb-6">
            <h1 className="text-[22px] font-semibold tracking-[-0.01em]">
              Sign in to <ShinyText text="your books" speed={5} />
            </h1>
            <p className="mt-1 text-[13px] text-muted">Use the account your administrator gave you.</p>
          </div>
          <ErrorNote error={error} />
          <Field label="Username">
            <Input autoFocus autoComplete="username" value={username} onChange={(e) => setUsername(e.target.value)} required />
          </Field>
          <Field label="Password">
            <Input
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </Field>
          <Button type="submit" variant="primary" loading={busy} className="h-9 w-full">
            Sign in
          </Button>
        </form>
      </section>
    </div>
  );
}
