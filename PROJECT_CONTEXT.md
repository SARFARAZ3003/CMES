# CMES — Project Context & Handoff

> Read this first. Full context of the CMES project so any new session can continue smoothly.
> CMES = **TCL MES** — a Manufacturing Execution System / Production Dashboard for Tata Cummins (engine plant).
> Built during Sarfaraz's internship. Author writes Hindi/Roman comments — match that style when editing.

---

## 1. What it is
A single **Production Dashboard** (real-time) that reads from the plant's SQL Server (`FlexNet`) and shows engine production per **line, shift, hour, day, month**, with Windows-login auth. No mock data — everything is from the DB.

## 2. Tech stack
- **Backend:** ASP.NET Core 8 Web API, **C#** (`backend/`). Runs on `http://localhost:5000`.
- **Frontend:** **React 19 + Vite** (JavaScript/JSX) + **recharts** + **axios** (`frontend/`). Runs on `http://localhost:5173`.
- **DB:** Microsoft **SQL Server**, accessed via **EF Core 8** (+ raw SQL for heavy aggregates).
- **Auth:** **Windows Integrated Authentication** (Negotiate, passwordless).

## 3. How to run (local)
```powershell
cd backend  ; dotnet run          # :5000  (auto-restores packages)
cd frontend ; npm install ; npm run dev   # :5173
```
If `dotnet run` says "address already in use": `Get-Process CMES -EA SilentlyContinue | Stop-Process -Force`.
Connection strings in `backend/appsettings.json` (see §10). Local dev Windows user = `laptop-tr0sqett\sarfaraz` → WWID **SARFARAZ** (seeded active).

## 4. Architecture
```
Browser (Windows identity auto-sent)
  → React (5173)  --axios withCredentials-->  ASP.NET API (5000)
                                                 → EF Core / raw SQL → SQL Server (FlexNet)
Flow: /auth/whoami (detect user) → Login screen → /auth/me (DB check) → Dashboard or Access Denied
```

## 5. Database tables in use
| Table | Used for | Key columns we read |
|---|---|---|
| `MPI_COB_T_SERIAL_NO_HISTORY` | **Old Line (WS 23800)**, **New Line (WS 33200)**, **Paint Line (WS 52000)** | SERIALNO, WORKSTATION, CREATEDON |
| `MPI_COB_T_AMI_CAPTURE_LOG` | **Test Cell (WS 40200)** | WORKSTATION, SERIALNO, CREATEDON |
| `MPI_COB_T_TRANSACTION_OUTBOUND` | **FES** (S side) | WIPJOBNO, SERIALNO, OVERALLSTATUS, CREATEDON |
| `MPI_COB_T_SERIAL_NO` | **FES** (C side, join) | SERIALNO, WORKORDERNO, STATUS, CREATEDON |
| `CMES_USERS` | **Auth** (authorization) — lives in **AUTH_DB** (separate) | UserId, Username(WWID), FullName, Role, IsActive |

All production tables are **READ-ONLY** from the app (no INSERT/UPDATE/DELETE anywhere in C#).

## 6. Domain logic (CRITICAL — how counts are derived)
- **Time:** DB stores **UTC**. App converts **+5:30 → IST** for all shift/hour/day logic.
- **Production day = 06:00 IST → next 06:00 IST.** In UTC that's `DATEADD(MINUTE,-30, CREATEDON)`'s date (because +5:30 then −6:00 = −0:30). The 00:30-UTC boundary == 06:00-IST == sir's original SQL boundary.
- **Shifts (IST):** A 06:00–14:30, B 14:30–22:30, C 22:30–06:00.
- **Metric definitions:**
  - **Old / New Line** = distinct **NEW engines** = serial's **first-ever** scan at that workstation in the day (sir's `NOT EXISTS (... CREATEDON < windowStart)` ≡ `MIN(CREATEDON)` per serial).
  - **Paint Line** = WS 52000, **distinct serials per day**.
  - **Test Cell** = AMI WS 40200, **COUNT(\*)** (every scan, duplicates).
  - **FES** = `OUTBOUND S ⨝ SERIAL_NO C ON C.WORKORDERNO=S.WIPJOBNO` WHERE `LEN(C.SERIALNO)=8 AND C.STATUS IN (3,4) AND S.OVERALLSTATUS=3`, COUNT(\*) by S.CREATEDON.
  - **Shipped** = 0 (no data source yet).
- These were **verified equal to sir's exact SQL** for every day via `database/verify_dashboard.py` + sqlcmd.

## 7. Backend (key files in `backend/`)
- `Program.cs` — DI, **Negotiate auth**, authorization policy `"CmesUser"`, CORS (`AllowCredentials`, any localhost), MemoryCache, **pooled** DbContext factories, `CommandTimeout(180)`.
- `Controllers/DashboardController.cs` — `[Authorize(Policy="CmesUser")]`. Endpoints:
  - `GET /api/Dashboard/overview?date=YYYY-MM-DD` — selected day: KPIs, shifts(A/B/C), hourly(24, IST 06→05) — all 5 metrics. Each metric fetches that day's **scan timestamps** (helpers `NewEngineScans`/`PaintScans`/`TestCellScans`/`FesScans`, returning `List<DateTime>` — small, one row per engine), 5 run in PARALLEL (own contexts via factory); shift/hour/KPI counting done in C# (`ShiftOf`, `.Hour`). Live 30s refresh hits only this. (No vs-yesterday `compare` anymore.)
  - `GET /api/Dashboard/trends` — **daily = last 30 days**, **monthly = ALL history**. Per-day SQL `GROUP BY` (`PerDayNewEngines`/`PerDayPaint`/`PerDayTestCell`/`PerDayFes` → `DayCount` rows), merged in C#. Cached 5 min under one `trends` key. (No `month` param.)
- `Controllers/AuthController.cs` — `GET /api/auth/whoami` (detected Windows user, no DB check) + `GET /api/auth/me` (CMES_USERS check → 200 or 403).
- `Services/CurrentUserService.cs` — WWID extract + CMES_USERS lookup (uses **AuthDbContext / AUTH_DB**); dev fallback to `Environment.UserName` ONLY in Development.
- `Authorization/CmesUserAuthorization.cs` — policy handler.
- `Data/CmesDbContext.cs` (CMES_DB, data tables) + `Data/AuthDbContext.cs` (AUTH_DB, CMES_USERS).
- `Models/` — SerialNoHistory, ExtraTables (AmiCaptureLog, SerialMaster=MPI_COB_T_SERIAL_NO, TransactionOutbound), CmesUser.
- `web.config` — IIS Windows-Auth on / anonymous off.
- **Gotcha:** ASP.NET camelCases JSON → anonymous shift keys `A/B/C` serialize as `a/b/c`; frontend reads `shifts.a/b/c`.

## 8. Auth (Windows Integrated)
Browser sends Windows identity → API extracts WWID → looks up `CMES_USERS` (active?) → dashboard or Access Denied. Roles (Admin/Viewer) stored & exposed for future role-based authorization (not yet enforced beyond active check). Server-side enforced on Dashboard via `[Authorize(Policy="CmesUser")]` (not just frontend).

## 9. Frontend (key files in `frontend/src/`)
- `App.jsx` — phase machine: `detecting → landing → authorized | denied`. Calls `/auth/whoami` then shows `Login`; on Log In → `/auth/me` → `Dashboard` or `AccessDenied`. `onLogout` resets to landing.
- `pages/Login.jsx` — legacy-style landing ("TCL MANUFACTURING EXECUTION SYSTEM", detected username, Log In).
- `pages/Dashboard.jsx` — the whole dashboard:
  - **5 KPI cards** (Old/New/Test/Paint/FES) — plain value cards (no vs-yesterday badge).
  - **Calendar date picker** (`<input type=date>`, min/max = DB range) + `‹ ›` arrows (±1 day, timezone-safe `addDays`). Nav uses `effDate = selectedDate || currentDay`.
  - Shift A/B/C summary cards.
  - **4 chart tabs:** Hourly / Shift / Daily / Monthly. Reusable `ProdChart` (**line or bar** toggle). **Daily = last 30 days; Monthly = all history.** Shift tab = 3 separate graphs (A/B/C).
  - **5 series with select/unselect checkboxes** (`visible` state). **Averages side panel** per visible series (`ChartWithAvg`).
  - 30s live refresh (overview only); trends fetched once on mount (backend-cached 5 min).
- `pages/AccessDenied.jsx`, `components/Spinner.jsx` (loading everywhere), `components/Sidebar.jsx` (real user + logout).
- `api/client.js` — axios baseURL `http://localhost:5000/api`, `withCredentials:true`.
- Colors: Old=green #4CAF50, New=blue #2196F3, Test=orange #FF9800, Paint=purple #9C27B0, FES=red #F44336. Accent dark-red #8B0000, bg #111/#1a1a1a. Tab title + favicon = CMES.

## 10. Connection strings (`backend/appsettings.json`)
```json
"CMES_DB": "Server=...;Database=<PROD_DB>;Trusted_Connection=True;TrustServerCertificate=True;",
"AUTH_DB": "Server=...;Database=<AUTH_DB>;Trusted_Connection=True;TrustServerCertificate=True;"
```
- `CMES_DB` = production data tables. `AUTH_DB` = CMES_USERS (can be a **different server/DB**; SQL auth: `User Id=..;Password=..`).
- **To point at sir's real DB: only change these two strings. No code change** (unless CMES_USERS has different table/column names → edit `Models/CmesUser.cs`).

## 11. Performance (for the real ~5-crore-row DB)
- **Never fetch whole table** — every query is filtered to one day (overview) or aggregated per-day (trends); results stay tiny (≈ one row per engine, or one row per day).
- Overview: 5 targeted per-day queries **parallel** (indexed); shift/hour/KPI counts computed in C# on the small timestamp lists. Trends: per-day SQL `GROUP BY`, merged in C#, **cached 5 min** (single `trends` key).
- **Indexes are essential** — `database/indexes.sql` (HIST(WS,SER,CON), AMI(WS,CON), OUTBOUND(OVERALLSTATUS,CON) incl WIPJOBNO, SERIAL_NO(WORKORDERNO) incl SERIALNO,STATUS). Sir says real DB already has indexes → don't recreate on prod.
- `CommandTimeout=180s`. Local warm timings: overview ~30ms, trends cold ~0.6s then cached.

## 12. Local test data
- `database/gen_dummy_2026.py` — generated **Jan 1 – 12 Jun 2026** realistic dummy (~380/day per metric, **each metric independent**, weekend lower, shift-weighted, FES join coordinated). Skips existing real Jun 3–10. Dummy serials are **70M+** (real are 64–65M). Re-run = deletes old dummy (serial≥70M) + regenerates. **Guard: only runs on `FlexNet`.** pyodbc used for bulk insert.
- ⚠️ **NEVER run on the real production DB:** `gen_dummy_2026.py`, `load_extra_tables.py`, `load_to_sql.py`, `01_create_table.sql`, `generate_insert_sql.py`, `indexes.sql`. These create/drop/load tables. Real DB already has everything.

## 13. Verification
- `database/verify_dashboard.py` — cross-checks API vs raw Excel/DB per day/shift/hour (old/new line). NOTE: its testcell branch is outdated (testcell moved to AMI); the AMI/Paint/FES metrics were instead verified directly vs sir's SQL via sqlcmd.

## 14. Deployment (IIS, production)
1. Install **.NET 8 Hosting Bundle**.
2. IIS site: **Windows Authentication ON, Anonymous OFF** (`web.config` ready).
3. Set `CMES_DB` + `AUTH_DB`. Ensure CMES_USERS has the real WWIDs (`IsActive=1`).
4. Best practice: serve React `npm run build` from the API's wwwroot (same origin → no CORS).

## 15. Known gaps / possible next steps
- **Shipped** metric — no data source yet (shows 0). Add table/query when available.
- **Role-based access** — roles stored but only "active user" is enforced; could gate features by role.
- Monthly all-history on a huge DB is a single heavy scan (cached) — if too slow, consider a pre-aggregated summary table.
- More pages (Production Report / WIP / Model Tracking / Inventory) were removed earlier; can be rebuilt on real data if needed.
- Repo: https://github.com/SARFARAZ3003/CMES (branch main). Changes not yet pushed in latest sessions — push when ready.

## 16. Quick mental model
> React shows it · C# API computes it (counts in SQL) · SQL Server stores it · Windows Auth gates it. DB is UTC → +5:30 IST → 6am–6am production day → split into A/B/C shifts. Old/New = new engines (first scan), Paint/TestCell/FES = their own logic. Everything verified against sir's queries.
