import { useMutation, useQuery, useQueryClient, keepPreviousData } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { api, ApiError } from "./api";
import { useToast } from "./toast";
import type {
  Account,
  Category,
  Company,
  ExpenseCategory,
  Paged,
  Party,
  Product,
  Unit,
} from "./types";

type Query = Record<string, string | number | boolean | null | undefined>;

/** GET with caching. Pass `null` as path to disable. */
export function useApi<T>(path: string | null, query?: Query, opts?: { keepPrevious?: boolean }) {
  return useQuery<T, ApiError>({
    queryKey: [path, query ?? {}],
    queryFn: () => api.get<T>(path!, query),
    enabled: path !== null,
    placeholderData: opts?.keepPrevious ? keepPreviousData : undefined,
  });
}

/**
 * POST/PUT/DELETE with toast feedback and cache invalidation. Financial postings touch many
 * aggregates (stock, balances, reports), so by default every cached query is invalidated.
 */
export function useAction<TVars, TResult = unknown>(
  fn: (vars: TVars) => Promise<TResult>,
  opts?: { success?: string | ((r: TResult) => string); onSuccess?: (r: TResult, vars: TVars) => void },
) {
  const qc = useQueryClient();
  const toast = useToast();
  return useMutation<TResult, ApiError, TVars>({
    mutationFn: fn,
    onSuccess: (r, vars) => {
      qc.invalidateQueries();
      if (opts?.success) toast(typeof opts.success === "function" ? opts.success(r) : opts.success);
      opts?.onSuccess?.(r, vars);
    },
    onError: (e) => toast(e.message, "error"),
  });
}

export function useDebounced<T>(value: T, ms = 250) {
  const [v, setV] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms);
    return () => clearTimeout(t);
  }, [value, ms]);
  return v;
}

// ---- shared lookups (cached for the session, refreshed after any mutation)
export const useUnits = () => useApi<Unit[]>("/units");
export const useCategories = () => useApi<Category[]>("/categories");
export const useExpenseCategories = (activeOnly = true) =>
  useApi<ExpenseCategory[]>("/expense-categories", { activeOnly });
export const useAccounts = (activeOnly = true) => useApi<Account[]>("/accounts", { activeOnly });
export const useCompany = () => useApi<Company>("/setup/company");
export const useProductsLookup = (search: string) =>
  useApi<Paged<Product>>("/products", { search, activeOnly: true, pageSize: 20 }, { keepPrevious: true });
export const usePartiesLookup = (search: string, kind?: string) =>
  useApi<Paged<Party>>("/parties", { search, kind, activeOnly: true, pageSize: 20 }, { keepPrevious: true });
