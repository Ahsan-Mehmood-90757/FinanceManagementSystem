import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { api, tokenStore } from "./api";
import type { LoginResponse, User, UserRole } from "./types";

type Permission = "post" | "master" | "void" | "setup" | "users";

// Mirrors Fms.Api.Auth.Policies on the server. The server is the authority; this only hides controls.
const matrix: Record<Permission, UserRole[]> = {
  post: ["Owner", "Admin", "Accountant"],
  master: ["Owner", "Admin", "Accountant"],
  void: ["Owner", "Admin"],
  setup: ["Owner", "Admin"],
  users: ["Owner"],
};

interface AuthState {
  user: User | null;
  ready: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
  can: (p: Permission) => boolean;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [ready, setReady] = useState(false);
  const qc = useQueryClient();

  useEffect(() => {
    if (!tokenStore.get()) {
      setReady(true);
      return;
    }
    api
      .get<User>("/auth/me")
      .then(setUser)
      .catch(() => tokenStore.clear())
      .finally(() => setReady(true));
  }, []);

  const logout = useCallback(() => {
    tokenStore.clear();
    setUser(null);
    qc.clear();
  }, [qc]);

  useEffect(() => {
    const onUnauthorized = () => setUser(null);
    window.addEventListener("fms:unauthorized", onUnauthorized);
    return () => window.removeEventListener("fms:unauthorized", onUnauthorized);
  }, []);

  const login = useCallback(async (username: string, password: string) => {
    const res = await api.post<LoginResponse>("/auth/login", { username, password });
    tokenStore.set(res.token);
    setUser(res.user);
  }, []);

  const value = useMemo<AuthState>(
    () => ({
      user,
      ready,
      login,
      logout,
      can: (p) => !!user && matrix[p].includes(user.role),
    }),
    [user, ready, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside AuthProvider");
  return ctx;
}
