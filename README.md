# Financial Management System

Perpetual-FIFO inventory, immutable bills, a unified party ledger and real cash/bank balances, in Pakistani rupees.

```
backend/    ASP.NET Core 10 Web API, Clean Architecture, EF Core 10 + PostgreSQL
frontend/   React 19 + TypeScript + Tailwind CSS 4 + TanStack Query (ReactBits components)
```

## Quick start (Windows)

**Easiest:** double-click `START-HERE.cmd`. It installs whatever is missing (.NET 10 SDK, Node.js LTS, PostgreSQL 17 with
user `postgres` / password `postgres`), creates the database, builds, runs the tests and starts the app. Afterwards, use
`RUN.cmd` to start it again.

## Quick start (macOS)

Needs [Homebrew](https://brew.sh). In Terminal, from the project folder:

```bash
./setup.sh   # installs .NET 10 SDK, Node.js and PostgreSQL 17 if missing, creates fms_dev, builds, tests, npm install
./run.sh     # starts the API and the web app and opens the browser (Ctrl+C stops both)
```

Run `setup.sh` once, then `run.sh` each time. If your PostgreSQL `postgres` user has a different password, `setup.sh` asks
for it and saves the connection string to `.env` (git-ignored), so `appsettings.Development.json` stays the same on every machine.

## Manual setup (any OS)

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download), Node.js 20+, and PostgreSQL 15+ (Docker Desktop or a local install).

```powershell
docker compose up -d                                   # PostgreSQL on localhost:5432 (postgres/postgres, db fms_dev)
powershell -ExecutionPolicy Bypass -File .\setup.ps1   # restore, create + apply the initial migration, run tests, npm install
```

Then, in two terminals:

```powershell
cd backend;  dotnet run --project src\Fms.Api    # API  http://localhost:5080   (API reference: /scalar)
cd frontend; npm run dev                         # App  http://localhost:5173
```

Sign in as **owner / Owner@12345**. That Owner account can do everything. Change the password under *Settings › My password*.

The first screen is **Day-0 setup**: company details, go-live date, opening cash/bank balances, what each customer and supplier
owes, and opening stock (as batches at cost). Posting documents is locked until you press *Finish setup and go live*.

If you use a local PostgreSQL with a different password, edit `ConnectionStrings:Default` in
`backend/src/Fms.Api/appsettings.Development.json` and set `FMS_DESIGN_CONNECTION` to the same string before running `setup.ps1`.

## How the money works

| Concept | Rule |
|---|---|
| **Party balance** | One running balance per party. Positive = receivable (they owe us). Negative = payable or advance held (we owe them). Receipts and payments are *unlinked*: they settle the balance, not a specific bill. |
| **Purchase bill** | Each line creates an inventory batch (qty × unit cost, optional batch no. + expiry). Supplier is credited with goods + any charges added to their bill. |
| **Sale bill** | Each line draws stock FIFO: oldest receipt first, expired batches skipped, batches received after the sale date ignored. Stock can never go negative. A line may instead name batches manually (e.g. clear stock close to expiry). COGS = cost of the exact batches used. |
| **Bill charges (split OpEx)** | Freight, labour, equipment hire on a bill are stored against the bill but kept out of COGS / gross margin. On purchases they can be paid from an account or added to the supplier's bill; on sales they're paid from an account. They reduce net profit. |
| **Standalone OpEx** | Expense vouchers (rent, salaries, utilities…) by expense category, paid from an account, charged to net profit. |
| **Immutability** | Posted documents are never edited or deleted. Corrections are **voids** (full reversal with equal-and-opposite ledger rows, reason required) or **returns** (partial). A purchase can only be voided while none of its stock has moved; a bill with returns can't be voided. Voiding a bill also voids the cash settled when it was posted. |
| **Returns** | Sale returns put goods back into the batches they left, most recent allocation first, so cost reverses exactly. Purchase returns take goods out of the bill's own batch (only what's still unsold). Either can refund cash immediately or leave it on the party balance. |
| **Write-offs** | Expired or damaged stock is written off from a batch at cost; the loss appears in P&L. |
| **Accounts** | Cash, bank and mobile-wallet accounts with real balances. Cash can never go negative; bank accounts can allow overdraft. |

### Profit & loss

```
Net sales            = sales − sale returns
COGS                 = FIFO cost of goods sold − cost of goods returned
Gross profit         = Net sales − COGS
Net profit           = Gross profit − bill charges − expense vouchers − stock write-offs
```

## Architecture

```
Fms.Domain          Entities + behaviour, Money / Quantity value objects, FifoAllocator, domain exceptions. No dependencies.
Fms.Application     Use-case services, DTOs, IRepository<T> / IUnitOfWork / IReadDb abstractions, LedgerWriter.
Fms.Infrastructure  EF Core DbContext (snake_case schema), repositories, unit of work, document numbering, seeding.
Fms.Api             Controllers, JWT auth + role policies, ProblemDetails error mapping, OpenAPI/Scalar.
Fms.Domain.Tests    xUnit tests for FIFO allocation, returns, ledgers and rounding.
```

- **Unit of work:** every write runs in one database transaction and commits once.
- **Concurrency:** inventory batches, parties and accounts carry PostgreSQL `xmin` as an optimistic concurrency token. If two
  users sell the last units of a batch at the same moment, one transaction conflicts and the whole operation is retried against
  fresh data (up to 3 times), so stock can't be over-drawn and balances can't drift.
- **CQRS-lite:** list/detail/report queries go through `IReadDb` (`AsNoTracking`) and project straight to DTOs.
- **Audit trail:** every balance change writes an append-only ledger row (`party_ledger_entries`, `account_ledger_entries`,
  `inventory_movements`) pointing at its source document. Documents record who posted / voided them and when.
- **Numbering:** gap-free per-type sequences (`SB-000001`, `PB-…`, `RV-…`, `PV-…`, `EV-…`, `TV-…`, `SR-…`, `PR-…`, `SA-…`).

### Roles

| Role | Can |
|---|---|
| Owner | Everything, including managing users. Created automatically on first start. |
| Admin | Everything except managing users. |
| Accountant | Post documents and edit master data. Cannot void or change Day-0 setup. |
| Viewer | Read only. |

## Environments

| | Development | Production |
|---|---|---|
| Config | `appsettings.Development.json` | `appsettings.Production.json` + environment variables |
| Database | `fms_dev` | `fms_prod` |
| Migrations | applied on start | **not** applied on start (`dotnet ef database update` or a migration bundle) |
| Secrets | dev defaults in file | `ConnectionStrings__Default`, `Jwt__SigningKey` (32+ chars), `Seed__Owner__Password` (first start), `Cors__Origins__0` |

Production single-host option: `npm run build` in `frontend/`, copy `dist/*` into `backend/src/Fms.Api/wwwroot/`, and the API
serves the app and falls back to `index.html` for client routes.

## API overview

All routes are under `/api` and need a bearer token except `POST /api/auth/login`. Full reference at `/scalar` in development.

```
auth          POST login · GET me · POST change-password
users         GET · POST · PUT {id} · POST {id}/reset-password                                   (Owner)
setup         GET company|status · PUT company · PUT parties|accounts/{id}/opening-balance
              POST|DELETE opening-stock · POST complete
units | categories | expense-categories              CRUD
products      GET ?search&categoryId&lowStockOnly · GET {id} · POST · PUT · GET {id}/movements · GET {id}/fifo-preview?quantity&date
parties       GET ?search&kind&balance · GET {id} · POST · PUT · GET {id}/statement?from&to
accounts      GET · GET {id} · POST · PUT · GET {id}/statement?from&to
inventory     GET batches?productId&expiringWithinDays&includeDepleted · GET|POST adjustments
purchases     GET · GET {id} · POST · POST {id}/void · POST {id}/returns     purchase-returns GET · GET {id}
sales         GET · GET {id} · POST · POST {id}/void · POST {id}/returns     sale-returns     GET · GET {id}
vouchers      GET ?type&from&to&partyId&accountId · GET {id} · POST · POST {id}/void
reports       GET dashboard · profit-loss?from&to · inventory-valuation · balances
```

Errors are RFC 7807 problem documents with a machine-readable `code`
(`insufficient_stock`, `insufficient_funds`, `setup_incomplete`, `has_returns`, `batch_in_use`, `concurrency`…).

## Front end

Slate neutrals, hairline borders, one ledger-teal accent (rust for payables), IBM Plex Sans with tabular figures, dense tables
built for keyboard entry. ReactBits pieces adapted in `src/components/reactbits/`: **Squares** (ruled-ledger background on
sign-in), **BlurText** (sign-in headline), **ShinyText**, **CountUp** (cash-on-hand figure) and **SpotlightCard** (dashboard
hero). Invoices, statements and reports have print stylesheets (`Print` buttons → A4).
