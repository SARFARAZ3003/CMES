# CMES — Production Dashboard (TCL MES)

**CMES** = a real-time **Manufacturing Execution System / Production Dashboard** for **Tata Cummins (TCL)** engine plant. It reads directly from the plant's SQL Server (`FlexNet`) and shows engine production per **line, shift, hour, day, and month**, gated by **Windows login**. No mock data — everything comes from the live DB.

> Built during an internship. Comments are in Hindi/Roman style — match that when editing.
> A deeper handoff doc lives in [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md). Read it first in a new session.

---

## Tech stack

| Layer | Tech | Runs on |
|---|---|---|
| **Backend** | ASP.NET Core 8 Web API (C#) | `http://localhost:5000` |
| **Frontend** | React 19 + Vite (JSX) + recharts + axios | `http://localhost:5173` |
| **Database** | Microsoft SQL Server via EF Core 8 (+ raw SQL for heavy aggregates) | — |
| **Auth** | Windows Integrated Authentication (Negotiate, passwordless) | — |

---

## How to run (local)

```powershell
# Backend  (auto-restores packages)
cd backend
dotnet run            # http://localhost:5000

# Frontend
cd frontend
npm install
npm run dev           # http://localhost:5173
```

If `dotnet run` says **"address already in use"**:
```powershell
Get-Process CMES -EA SilentlyContinue | Stop-Process -Force
```

Connection strings live in `backend/appsettings.json` (see [Configuration](#configuration)).
Local dev Windows user = `laptop-tr0sqett\sarfaraz` → WWID **SARFARAZ** (seeded active).

---

## Architecture

```
Browser (Windows identity auto-sent)
  → React (5173)  --axios withCredentials-->  ASP.NET API (5000)
                                                 → EF Core / raw SQL → SQL Server (FlexNet)

Login flow: /auth/whoami (detect user) → Login screen → /auth/me (DB check) → Dashboard | Access Denied
```

Two separate databases:
- **CMES_DB** — production data tables (read-only).
- **AUTH_DB** — `CMES_USERS` (authorization). Can be a different server/DB.

---

## Project structure

```
CMES/
├── backend/                       ASP.NET Core 8 Web API
│   ├── Program.cs                 DI, Negotiate auth, "CmesUser" policy, CORS, MemoryCache,
│   │                              pooled DbContext factories, CommandTimeout(180)
│   ├── Controllers/
│   │   ├── DashboardController.cs  [Authorize] overview + trends (all counting in SQL)
│   │   └── AuthController.cs       whoami (detect) + me (DB check → 200/403)
│   ├── Services/
│   │   └── CurrentUserService.cs   WWID extract + CMES_USERS lookup (AuthDbContext)
│   ├── Authorization/
│   │   └── CmesUserAuthorization.cs  "CmesUser" policy handler (active user check)
│   ├── Data/
│   │   ├── CmesDbContext.cs        CMES_DB (data tables)
│   │   └── AuthDbContext.cs        AUTH_DB (CMES_USERS)
│   ├── Models/                     SerialNoHistory, ExtraTables, CmesUser
│   ├── appsettings.json            CMES_DB + AUTH_DB connection strings
│   └── web.config                  IIS: Windows Auth ON, Anonymous OFF
│
├── frontend/                      React 19 + Vite
│   └── src/
│       ├── App.jsx                 phase machine: detecting → landing → authorized | denied
│       ├── api/client.js           axios baseURL :5000/api, withCredentials
│       ├── pages/                  Login, Dashboard, AccessDenied
│       └── components/             Sidebar, Spinner
│
└── database/                      SQL + Python helpers (DEV/local only — see warning below)
    ├── indexes.sql                4 essential indexes for the heavy aggregates
    └── gen_dummy_2026.py          local dummy data generator (FlexNet-guarded)
```

---

## Database tables in use

| Table | Used for | Key columns |
|---|---|---|
| `MPI_COB_T_SERIAL_NO_HISTORY` | **Old Line (WS 23800)**, **New Line (WS 33200)**, **Paint Line (WS 52000)** | SERIALNO, WORKSTATION, CREATEDON |
| `COB_T_AMI_CAPTURE_LOG` | **Test Cell (WS 40200)** | WORKSTATION, SERIALNO, CREATEDON |
| `MPI_COB_T_TRANSACTION_OUTBOUND` | **FES** (S side) | WIPJOBNO, SERIALNO, OVERALLSTATUS, CREATEDON |
| `MPI_COB_T_SERIAL_NO` | **FES** (C side, join) | SERIALNO, WORKORDERNO, STATUS, CREATEDON |
| `CMES_USERS` | **Auth** (in AUTH_DB) | UserId, Username (WWID), FullName, Role, IsActive |

All production tables are **READ-ONLY** from the app (no INSERT/UPDATE/DELETE anywhere in C#).

---

## Domain logic (how counts are derived)

- **Time:** DB stores **UTC**. App converts **+5:30 → IST** for all shift/hour/day logic.
- **Production day = 06:00 IST → next 06:00 IST.** In UTC that's the date of `DATEADD(MINUTE,-30, CREATEDON)` (because +5:30 then −6:00 = −0:30, i.e. the 00:30-UTC boundary == 06:00-IST).
- **Shifts (IST):** A = 06:00–14:30, B = 14:30–22:30, C = 22:30–06:00.
- **Metric definitions:**
  - **Old / New Line** = distinct **new engines** = serial's **first-ever** scan at that workstation in the day (`NOT EXISTS (... CREATEDON < windowStart)` ≡ `MIN(CREATEDON)` per serial).
  - **Paint Line** = WS 52000, **distinct serials per day**.
  - **Test Cell** = AMI WS 40200, **COUNT(\*)** (every scan, duplicates included).
  - **FES** = `OUTBOUND S ⨝ SERIAL_NO C ON C.WORKORDERNO = S.WIPJOBNO` WHERE `LEN(C.SERIALNO)=8 AND C.STATUS IN (3,4) AND S.OVERALLSTATUS=3`, COUNT(\*) by `S.CREATEDON`.
  - **Shipped** = 0 (no data source yet).
- All metric logic was **verified equal to the plant's exact SQL** per day.

---

## API endpoints

All Dashboard endpoints require an active CMES user (`[Authorize(Policy = "CmesUser")]`).

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/auth/whoami` | Detected Windows user (no DB check) — for the login screen |
| GET | `/api/auth/me` | CMES_USERS check → **200** (authorized + role/name) or **403** (Access Denied) |
| GET | `/api/Dashboard/overview?date=YYYY-MM-DD` | Selected day: KPIs, shifts (A/B/C), 24-hour breakdown (IST 06→05), and `compare` (vs-yesterday % per metric, same-time cutoff). 5 metrics run **in parallel**. Hit by the 30s live refresh. |
| GET | `/api/Dashboard/trends?month=YYYY-MM` | `daily` = that month's days; `monthly` = all history. Single-pass GROUP BY; cached (daily per-month 5 min, monthly once). |

---

## Performance

- **Never fetch whole tables** — all counting is SQL `GROUP BY` / `COUNT`, returning tiny results (~26 rows/metric).
- **Overview:** 5 targeted per-day queries run **in parallel** (each with its own DbContext via the factory — required for thread safety).
- **Trends:** single-pass aggregates, **cached** (monthly once, daily per-month).
- **Indexes are essential** — see `database/indexes.sql`. The real DB already has indexes; don't recreate on prod.
- `CommandTimeout = 180s`. Local warm timings: overview ~30 ms, trends cold ~0.6 s then cached.

---

## Configuration

`backend/appsettings.json`:
```json
"CMES_DB": "Server=...;Database=<PROD_DB>;Trusted_Connection=True;TrustServerCertificate=True;",
"AUTH_DB": "Server=...;Database=<AUTH_DB>;Trusted_Connection=True;TrustServerCertificate=True;"
```
**To point at the real DB, change only these two strings — no code change** (unless `CMES_USERS` has different table/column names → edit `Models/CmesUser.cs`).

---

## Deployment (IIS, production)

1. Install **.NET 8 Hosting Bundle**.
2. IIS site: **Windows Authentication ON, Anonymous OFF** (`web.config` ready).
3. Set `CMES_DB` + `AUTH_DB`. Ensure `CMES_USERS` has the real WWIDs with `IsActive = 1`.
4. Best practice: serve the React `npm run build` output from the API's `wwwroot` (same origin → no CORS).

---

## ⚠️ Local-only DB scripts — NEVER run on production

These create/drop/load tables and are for local dev only (the dummy generator is guarded to the `FlexNet` DB):

```
database/gen_dummy_2026.py   database/load_extra_tables.py   database/load_to_sql.py
database/01_create_table.sql database/generate_insert_sql.py database/indexes.sql
```

The real DB already has all tables and indexes.

---

## Known gaps / next steps

- **Shipped** metric — no data source yet (shows 0).
- **Role-based access** — roles are stored but only "active user" is enforced; features could be gated by role.
- Monthly all-history is a single heavy scan (cached) — consider a pre-aggregated summary table if it gets slow on the full DB.
- More pages (Production Report / WIP / Model Tracking / Inventory) were removed earlier; can be rebuilt on real data if needed.
