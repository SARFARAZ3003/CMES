# CMES — Project Explained (Beginner-Friendly Mentor Guide)

> **CMES** = Cummins/TCL **Manufacturing Execution System** — a web dashboard that reads
> live engine-assembly production data from a SQL Server database and shows it in a
> dark-themed React UI (KPIs, shift summary, charts, searchable tables).

This document explains **every file**, the **full data flow**, **where everything lives**,
**50 mentor Q&A**, **how to pitch it in an interview**, and a **learning roadmap**.

> ⚠️ **Honesty note (read this first):** This is a working **prototype/MVP**. Some things a
> "full" production app has are **not built yet**: real authentication, JWT, cookies, email,
> and custom middleware. Where this doc reaches those topics, it clearly says **"Not
> implemented yet"** and explains *where* they would go. Never claim features that don't
> exist — a good engineer is honest about scope.

---

## Table of Contents
1. [Big Picture](#1-big-picture)
2. [Tech Stack](#2-tech-stack)
3. [Folder Structure](#3-folder-structure)
4. [File-by-File Explanation](#4-file-by-file-explanation)
5. [Complete Application Flow](#5-complete-application-flow)
6. [Where Is Everything Located?](#6-where-is-everything-located)
7. [If My Mentor Asks Me Questions (50 Q&A)](#7-if-my-mentor-asks-me-questions)
8. [How I Should Explain This Project in an Interview](#8-how-to-explain-in-an-interview)
9. [Things I Must Understand Before Putting This on My Resume](#9-things-to-understand-before-resume)

---

## 1. Big Picture

This is a **3-tier web application**:

```
┌──────────────┐    HTTP + JSON    ┌──────────────┐     SQL      ┌──────────────┐
│  FRONTEND    │ ───────────────►  │  BACKEND     │ ──────────►  │  DATABASE    │
│  React (UI)  │ ◄───────────────  │  ASP.NET 8   │ ◄──────────  │  SQL Server  │
│  port 5173   │   axios calls     │  port 5000   │   EF Core    │  (FlexNet)   │
└──────────────┘                   └──────────────┘              └──────────────┘
   "shows data"                      "logic + API"                 "stores data"
```

- **Frontend (React):** what the user sees in the browser. It never talks to the database directly — it only calls the backend.
- **Backend (ASP.NET Core, C#):** exposes REST API endpoints. It runs logic and fetches data from the database using **EF Core** (an ORM).
- **Database (SQL Server):** stores the real production data in the table `dbo.MPI_COB_T_SERIAL_NO_HISTORY` (~10,429 rows).

**Why 3 tiers?** Separation of concerns + security. The DB password lives only in the
backend, never in the browser. Each layer can change independently.

---

## 2. Tech Stack

| Layer | Technology | Why |
|---|---|---|
| Frontend | React 19 + Vite | Component UI, fast dev server |
| Routing | react-router-dom 7 | Multiple pages without full reloads (SPA) |
| HTTP client | axios | Clean way to call the backend API |
| Charts | recharts | Bar/line charts for production data |
| Backend | ASP.NET Core 8 (C#) | REST API framework |
| ORM | Entity Framework Core 8 | C# code → SQL queries (no manual SQL) |
| Database | SQL Server | Stores production data |
| API docs | Swagger (Swashbuckle) | Test endpoints in browser |

**Key idea — SPA + REST API:** The frontend is a *Single Page Application*. It loads once,
then fetches data as JSON from the backend's *REST API* and re-renders parts of the page.

---

## 3. Folder Structure

```
CMES/
├── PROJECT_EXPLAINED.md        ← this file
├── README.md
│
├── backend/                    ← ASP.NET Core API (C#)
│   ├── CMES.csproj             ← project + NuGet packages
│   ├── Program.cs              ← app startup, DI, CORS, DB registration
│   ├── appsettings.json        ← config + DB connection string
│   ├── Properties/launchSettings.json  ← which port to run on
│   ├── Controllers/            ← API endpoints (6 files)
│   ├── Models/                 ← C# classes that map to data (6 files)
│   ├── Data/CmesDbContext.cs   ← EF Core bridge to the database
│   └── guide                   ← author's personal notes
│
├── frontend/                   ← React app (Vite)
│   ├── index.html              ← single HTML page (entry)
│   ├── vite.config.js          ← Vite build config
│   ├── package.json            ← npm dependencies
│   └── src/
│       ├── main.jsx            ← React entry point
│       ├── App.jsx             ← routes (URL → page)
│       ├── api/client.js       ← axios instance (backend URL)
│       ├── components/Sidebar.jsx + .css   ← left nav menu
│       ├── pages/              ← one file per screen
│       ├── data/mockData.js    ← leftover demo data (used by Login user info)
│       ├── index.css / App.css ← global styles
│       └── assets/             ← images
│
└── database/                   ← SQL + Python scripts to load data
    ├── 01_create_table.sql     ← creates the table
    ├── MPI_COB_T_SERIAL_NO_HISTORY_insert.sql  ← generated INSERTs
    ├── generate_insert_sql.py  ← Excel → SQL file
    ├── load_to_sql.py          ← Excel → DB directly
    └── README.md
```

---

## 4. File-by-File Explanation

> For each important file: **path, purpose, why it exists, problem it solves, main
> functions/classes, important-line explanation, interactions, inputs/outputs, and what
> breaks if removed.** Trivial files (assets, lockfiles) are summarized briefly at the end.

---

### 🔹 BACKEND

---

#### `backend/CMES.csproj`

1. **Path:** `backend/CMES.csproj`
2. **Purpose:** The .NET project definition — target framework and NuGet packages.
3. **Why it exists:** `dotnet build`/`dotnet run` need this to know how to compile the app. Without it, there is no "project."
4. **Problem it solves:** Declares dependencies (EF Core, Swagger) so .NET downloads and links them.
5. **Main parts:** `<TargetFramework>net8.0</TargetFramework>`, and `<PackageReference>` entries.
6. **Important lines:**
   - `Sdk="Microsoft.NET.Sdk.Web"` → this is a web project.
   - `Microsoft.EntityFrameworkCore.SqlServer` → EF Core + SQL Server driver.
   - `Microsoft.EntityFrameworkCore.Design` → tooling (migrations, scaffolding).
   - `Swashbuckle.AspNetCore` → Swagger UI.
7. **Interacts with:** Everything in `backend/` compiles under this project.
8. **Inputs/Outputs:** Input = source files; Output = `CMES.dll` (the running app).
9. **If removed:** The backend cannot build or run at all.

---

#### `backend/Program.cs`  ⭐ (most important backend file)

1. **Path:** `backend/Program.cs`
2. **Purpose:** The **startup file**. First code that runs. Wires up everything: controllers, database, CORS, Swagger.
3. **Why it exists:** ASP.NET needs one entry point to configure and launch the web server.
4. **Problem it solves:** Central place to register services (Dependency Injection) and the request pipeline (middleware order).
5. **Main parts:** the `builder` (services) section and the `app` (pipeline) section.
6. **Line-by-line (the important bits):**
   ```csharp
   var builder = WebApplication.CreateBuilder(args);   // create app builder

   builder.Services.AddControllers();                  // enable controller classes
   builder.Services.AddEndpointsApiExplorer();         // needed for Swagger
   builder.Services.AddSwaggerGen();                   // generate Swagger docs

   // Register the database connection (Dependency Injection)
   builder.Services.AddDbContext<CmesDbContext>(options =>
       options.UseSqlServer(builder.Configuration.GetConnectionString("CMES_DB")));
   //        ▲ use SQL Server          ▲ read connection string named "CMES_DB" from appsettings.json

   // CORS: allow the React dev server (port 5173) to call this API (port 5000)
   builder.Services.AddCors(options => {
       options.AddPolicy("AllowReactApp", policy =>
           policy.WithOrigins("http://localhost:5173")
                 .AllowAnyHeader().AllowAnyMethod());
   });

   var app = builder.Build();

   app.UseSwagger();              // serve swagger.json
   app.UseSwaggerUI();            // serve the Swagger test page
   app.UseCors("AllowReactApp");  // apply the CORS policy
   app.UseAuthorization();        // authorization middleware (currently a no-op — no auth yet)
   app.MapControllers();          // route /api/... requests to controllers
   app.Run();                     // start listening (port from launchSettings.json = 5000)
   ```
7. **Interacts with:** `appsettings.json` (connection string), `CmesDbContext` (DB), every controller (`MapControllers`).
8. **Inputs/Outputs:** Input = config + HTTP requests; Output = a running web server on port 5000.
9. **If removed:** No app. Nothing starts.

> **DI in one line:** `AddDbContext<CmesDbContext>(...)` tells ASP.NET "whenever a controller
> asks for a `CmesDbContext` in its constructor, create and hand one over." That's why
> controllers can just write `public DashboardController(CmesDbContext db)`.

---

#### `backend/appsettings.json`

1. **Path:** `backend/appsettings.json`
2. **Purpose:** Configuration file — most importantly the **database connection string**.
3. **Why it exists:** Keeps environment-specific settings out of code. Change the DB without recompiling.
4. **Problem it solves:** One place to point the app at a different server/database/login.
5. **Main parts:** `ConnectionStrings.CMES_DB`, `Logging`, `AllowedHosts`.
6. **Important line:**
   ```json
   "CMES_DB": "Server=localhost;Database=FlexNet;Trusted_Connection=True;TrustServerCertificate=True;"
   ```
   - `Server` = SQL Server name/IP. `Database` = DB name. `Trusted_Connection=True` = Windows login.
   - For **SQL login** instead: replace `Trusted_Connection=True` with `User Id=xxx;Password=yyy`.
   - `TrustServerCertificate=True` = don't fail on the dev SSL certificate.
7. **Interacts with:** `Program.cs` reads `GetConnectionString("CMES_DB")`.
8. **Inputs/Outputs:** Input = read at startup; Output = the live DB connection.
9. **If removed:** App starts but every DB call throws "connection string not found."

> 🔑 **This is the single most important file when deploying to another machine / real DB.**
> Change this one line → done.

---

#### `backend/Properties/launchSettings.json`

1. **Path:** `backend/Properties/launchSettings.json`
2. **Purpose:** Local run settings — the **port** (`http://localhost:5000`) and environment (`Development`).
3. **Why it exists:** Tells `dotnet run` which URL to bind and whether to open Swagger.
4. **Problem it solves:** Without it, .NET picks a random port; the frontend expects exactly 5000.
5. **Important line:** `"applicationUrl": "http://localhost:5000"`.
6. **Interacts with:** Frontend `client.js` hard-codes `localhost:5000`, so these must match.
7. **Inputs/Outputs:** Input = used by `dotnet run`; Output = server bound to port 5000.
8. **If removed:** App may run on a different/random port → frontend calls fail (CORS/404).

---

#### `backend/Models/` (6 files) — the "shape" of data

Each model is a **plain C# class** describing a table/record. EF Core maps class ↔ table.

| File | Represents | Notes |
|---|---|---|
| `SerialNoHistory.cs` ⭐ | Real table `MPI_COB_T_SERIAL_NO_HISTORY` | The only model wired to the real DB |
| `Production.cs` | A shift/day production summary | Used conceptually by mock controller |
| `WIP.cs` | Work-in-progress count per location | Mock |
| `Engine.cs` | One engine unit | Mock |
| `Inventory.cs` | A stock/part item | Mock |
| `Employee.cs` | A plant employee | Mock |

**`SerialNoHistory.cs` in detail (the important one):**
```csharp
[Keyless]                                   // table has no single primary key → read-only
[Table("MPI_COB_T_SERIAL_NO_HISTORY")]      // exact DB table name
public class SerialNoHistory {
    [Column("SERIALNO")] public string? SerialNo { get; set; }   // DB column ↔ C# property
    [Column("WORKSTATION")] public string? Workstation { get; set; }
    [Column("STATUS")] public double? Status { get; set; }
    [Column("LOCATION")] public string? Location { get; set; }
    [Column("CREATEDON")] public DateTime? CreatedOn { get; set; }
    // ...a subset of the table's 33 columns (only what we display)
}
```
- `[Keyless]` = "this entity has no primary key" → EF won't try to track/update rows; perfect for read-only reporting.
- `[Column("SERIALNO")]` = maps the C# property `SerialNo` to the DB column `SERIALNO`.

7. **Interacts with:** `CmesDbContext` (declares `DbSet`s of these), controllers (query them).
8. **Inputs/Outputs:** No logic — just data shape. Output = typed objects EF fills from rows.
9. **If `SerialNoHistory.cs` removed:** Dashboard + Model Tracking break (no type to query the real table). If a mock model is removed: nothing user-facing breaks (mock controllers use anonymous objects, not these classes — they exist mainly for future DB wiring).

---

#### `backend/Data/CmesDbContext.cs`  ⭐

1. **Path:** `backend/Data/CmesDbContext.cs`
2. **Purpose:** The **EF Core context** — the bridge between C# and the database.
3. **Why it exists:** EF needs one class that lists all tables (`DbSet`s) and holds the connection.
4. **Problem it solves:** Lets you query the DB with C# (`_db.SerialNoHistory.ToList()`) instead of writing raw SQL.
5. **Main class:** `CmesDbContext : DbContext`.
6. **Important lines:**
   ```csharp
   public class CmesDbContext : DbContext {
       public CmesDbContext(DbContextOptions<CmesDbContext> options) : base(options) {}
       // each DbSet = one table you can query
       public DbSet<SerialNoHistory> SerialNoHistory => Set<SerialNoHistory>();
       public DbSet<Employee> Employees => Set<Employee>();
       // ...Engines, Productions, WIPs, Inventories
   }
   ```
7. **Interacts with:** `Program.cs` (registered via `AddDbContext`), controllers (injected in), Models (the `DbSet` types).
8. **Inputs/Outputs:** Input = connection string (via options); Output = query-able tables.
9. **If removed:** Every real DB controller fails to compile. No database access.

> **How a C# query becomes SQL:** `_db.SerialNoHistory.Where(x => x.SerialNo == "123")`
> → EF Core translates to `SELECT ... FROM MPI_COB_T_SERIAL_NO_HISTORY WHERE SERIALNO = '123'`
> and runs it. You never write SQL by hand. This also prevents SQL-injection.

---

#### `backend/Controllers/` (6 files) — the API endpoints

A **controller** handles incoming HTTP requests for a URL group and returns JSON.
Common attributes:
- `[ApiController]` = it's a Web API controller.
- `[Route("api/[controller]")]` = base URL is `/api/<ControllerName-without-"Controller">`.
- `[HttpGet("xyz")]` = handle `GET /api/.../xyz`.

**Two kinds of controllers here:**

##### ✅ Real (talk to the database)

**`DashboardController.cs`** → `GET /api/Dashboard/overview`
- Injects `CmesDbContext _db`.
- Finds the **latest date** in the data and treats it as "today."
- Loads that day's rows into memory and computes: KPIs, per-shift (A/B/C) counts, hourly, daily.
- **Shift logic:** `ShiftOf(hour)` → 6–14 = A, 14–22 = B, else C (derived from `CREATEDON`).
- Returns one JSON object the Dashboard page renders.
- Key line:
  ```csharp
  var latest = await table.MaxAsync(x => x.CreatedOn);   // newest timestamp = "today"
  var dayRows = await table.Where(x => x.CreatedOn >= day && x.CreatedOn < nextDay)... ;
  ```

**`SerialHistoryController.cs`** → `GET /api/SerialHistory`, `GET /api/SerialHistory/summary`
- Paginated + searchable list of real rows.
- Key lines:
  ```csharp
  var query = _db.SerialNoHistory.AsNoTracking().AsQueryable();
  if (search) query = query.Where(x => x.SerialNo.Contains(s) || ...);  // server-side search
  var total = await query.CountAsync();
  var rows = await query.OrderByDescending(x => x.CreatedOn)
                        .Skip((page-1)*pageSize).Take(pageSize).ToListAsync();  // pagination
  ```
- `AsNoTracking()` = read-only, faster (EF won't watch for changes).

##### ⚠️ Mock (return hard-coded demo data — no DB)

| Controller | Endpoints | Returns |
|---|---|---|
| `ProductionController.cs` | `/test`, `/summary`, `/report`, `/hourly` | Dummy production numbers |
| `WipController.cs` | `/test`, `/summary`, `/locations` | Dummy WIP-by-location |
| `EngineController.cs` | `/test`, `/tracking`, `/models`, `/{engineNo}` | Dummy engine list |
| `InventoryController.cs` | `/test`, `/summary`, `/items` | Dummy stock items |

Mock pattern (e.g. Inventory):
```csharp
[HttpGet("items")]
public IActionResult GetItems() {
    var items = new[] { new { partNo = "PRT-1001", inStock = 320, status = "OK" }, ... };
    return Ok(items);   // same JSON shape a real query would return
}
```
They exist so the UI is fully usable **before** those real tables are available. To make
them real later: replace the `new[]{...}` with `_db.X.ToList()` — frontend stays unchanged.

**For all controllers:**
7. **Interact with:** `CmesDbContext` (real ones), and are called by frontend pages via axios.
8. **Inputs/Outputs:** Input = HTTP request (+ query params like `page`, `search`); Output = JSON.
9. **If removed:** The matching page shows its error state ("Backend se data nahi mila"). E.g., remove `DashboardController` → Dashboard page errors; other pages keep working.

---

#### `backend/guide`
Author's personal study notes (analogy: "controller = receptionist"). Not used by the app.
If removed: nothing breaks.

---

### 🔹 FRONTEND

---

#### `frontend/index.html`
1. **Path:** `frontend/index.html`
2. **Purpose:** The single HTML page. Has `<div id="root"></div>` where React mounts.
3. **Why/Problem:** A SPA needs one host page; React injects everything into `#root`.
4. **Important line:** `<script type="module" src="/src/main.jsx"></script>` → loads React.
5. **Interacts with:** `main.jsx`.
6. **If removed:** Nothing renders — no mount point.

#### `frontend/src/main.jsx`
1. **Purpose:** React entry point — mounts `<App/>` into `#root`.
2. **Important lines:**
   ```jsx
   createRoot(document.getElementById('root')).render(
     <StrictMode><App /></StrictMode>
   )
   ```
   - `StrictMode` runs effects twice in dev (that's why you may see two API calls in the console — harmless).
3. **Interacts with:** `index.html` (`#root`), `App.jsx`.
4. **If removed:** App never starts in the browser.

#### `frontend/src/App.jsx`  ⭐ (routing)
1. **Purpose:** Maps URLs to page components (client-side routing).
2. **Important lines:**
   ```jsx
   <BrowserRouter>
     <Routes>
       <Route path="/" element={<Login />} />
       <Route path="/dashboard" element={<Dashboard />} />
       <Route path="/production-report" element={<ProductionReport />} />
       <Route path="/wip-report" element={<WipReport />} />
       <Route path="/model-tracking" element={<ModelTracking />} />
       <Route path="/inventory" element={<Inventory />} />
       <Route path="*" element={<Navigate to="/" />} />   // unknown URL → login
     </Routes>
   </BrowserRouter>
   ```
3. **Interacts with:** every page component; the `Sidebar` links navigate between these routes.
4. **Inputs/Outputs:** Input = URL; Output = the matching page rendered.
5. **If removed:** No navigation; the app wouldn't know which page to show.

#### `frontend/src/api/client.js`  ⭐ (the API bridge)
1. **Purpose:** One configured **axios** instance pointing at the backend.
2. **Important lines:**
   ```js
   const api = axios.create({
     baseURL: 'http://localhost:5000/api',          // backend address
     headers: { 'Content-Type': 'application/json' },
   })
   export default api
   ```
3. **Problem it solves:** Every page imports `api` and calls e.g. `api.get('/Dashboard/overview')` without repeating the base URL.
4. **Interacts with:** every page; the backend controllers.
5. **Inputs/Outputs:** Input = endpoint path + params; Output = a Promise resolving to JSON.
6. **If removed:** No page can reach the backend. Whole app shows error states.

> **To deploy:** change `baseURL` here to the deployed backend URL. (One line, like the
> backend's connection string.)

#### `frontend/src/components/Sidebar.jsx` (+ `Sidebar.css`)
1. **Purpose:** Left navigation menu shown on every page; brand ("TCL CMES"), user info, nav links, logout.
2. **Important parts:**
   - `navItems` array → the 5 links (`/dashboard`, `/production-report`, `/wip-report`, `/model-tracking`, `/inventory`).
   - `<NavLink>` → highlights the active page automatically.
   - `handleLogout = () => navigate('/')` → **dummy logout** (just goes to login; no session to clear).
   - User name/code come from `mockData.js` (`currentUser`).
3. **Interacts with:** `App.jsx` routes (via `NavLink`), `mockData.js`.
4. **Inputs/Outputs:** Input = current URL (for active styling); Output = the menu UI.
5. **If removed:** Pages lose navigation and logout; you'd be stuck on one screen.

#### `frontend/src/pages/` — one file per screen

All pages share the same pattern:
```jsx
const [data, setData] = useState(null)      // 1. state to hold fetched data
const [loading, setLoading] = useState(true)
const [error, setError] = useState('')

useEffect(() => {                            // 2. on first render, fetch
  api.get('/SomeEndpoint')
     .then(res => setData(res.data))         // 3. success → save data
     .catch(() => setError('...'))           // 4. failure → show error
     .finally(() => setLoading(false))
}, [])
// 5. render: loading? error? else show data in cards/tables/charts
```

| Page file | Route | Data source | What it shows |
|---|---|---|---|
| `Login.jsx` ⚠️ | `/` | none | Fake Windows-SSO screen; button → `/dashboard` |
| `Dashboard.jsx` ✅ | `/dashboard` | `GET /api/Dashboard/overview` | KPIs, shift A/B/C, hourly & daily charts |
| `ModelTracking.jsx` ✅ | `/model-tracking` | `GET /api/SerialHistory` | Real serial rows + **server-side search + pagination** |
| `ProductionReport.jsx` ⚠️ | `/production-report` | `/api/Production/*` (mock) | KPIs + shift table |
| `WipReport.jsx` ⚠️ | `/wip-report` | `/api/Wip/*` (mock) | WIP chart + table |
| `Inventory.jsx` ⚠️ | `/inventory` | `/api/Inventory/*` (mock) | Stock table + status pills |

**`ModelTracking.jsx` extra detail (best example of real, efficient data fetching):**
- Holds `page`, `search` in state.
- A debounced `useEffect` refetches when `page` or `search` changes:
  ```jsx
  useEffect(() => { setPage(1) }, [search])           // new search → back to page 1
  useEffect(() => {
    api.get('/SerialHistory', { params: { page, pageSize: 50, search } })
       .then(res => { setRows(res.data.rows); setTotalPages(res.data.totalPages) })
  }, [page, search])
  ```
- Search runs **on the server** (DB filters), so the browser only ever holds 50 rows even though the table has 10k+.

7. **Pages interact with:** `api/client.js` (data), `Sidebar` (layout), CSS files (styling).
8. **Inputs/Outputs:** Input = user actions (search text, page click); Output = rendered UI.
9. **If a page removed:** Its route 404s / redirects; other pages unaffected.

#### `frontend/src/data/mockData.js`
- Leftover demo data from the original template. **Still used** by `Sidebar` for the displayed
  user (`currentUser`). The old dashboard KPIs/charts here are **no longer used** (Dashboard now
  fetches real data).
- If removed: Sidebar user name/code break (and any stray import).

#### CSS files
- `Dashboard.css` → layout (`.dash-root` is `height:100vh; overflow:hidden` so content scrolls
  inside and the sidebar/logout stays visible), KPI cards, shift cards, charts, tabs.
- `Reports.css` → shared table, status **pills**, search box, **pagination** styles.
- `Sidebar.css`, `Login.css`, `index.css`, `App.css` → component/global styling.
- If removed: app still works but looks unstyled.

#### Config/asset files (brief)
| File | Purpose |
|---|---|
| `vite.config.js` | Vite + React plugin config |
| `package.json` / `package-lock.json` | npm dependencies + exact versions |
| `eslint.config.js` | Linting rules |
| `public/`, `src/assets/` | Icons/images |
| `.gitignore` (both) | Files git should ignore (`node_modules`, `bin`, `obj`) |

---

### 🔹 DATABASE (`database/`)

| File | Purpose |
|---|---|
| `01_create_table.sql` | Creates `dbo.MPI_COB_T_SERIAL_NO_HISTORY` (33 columns) if missing |
| `generate_insert_sql.py` | Reads `PROD.xlsx` → writes a `.sql` file full of `INSERT`s |
| `MPI_COB_T_SERIAL_NO_HISTORY_insert.sql` | The generated INSERTs (run in SSMS with F5) |
| `load_to_sql.py` | Alternative: read Excel → insert straight into DB via pyodbc |
| `README.md` | How to load the data |

**Why these exist:** the real data started life in an Excel export. These scripts turn that
Excel into rows inside SQL Server. One-time setup (re-runnable for new data).
**If removed:** the app still runs (data already loaded); you'd just lose the reload tooling.

---

## 5. Complete Application Flow

### 5.1 User opens the website (end-to-end happy path)

```
┌──────┐   1. open localhost:5173/dashboard
│ USER │ ─────────────────────────────────────────────►  Browser loads index.html
└──────┘                                                       │
                                                               ▼
   2. main.jsx mounts <App/>  →  App.jsx router matches "/dashboard" → <Dashboard/>
                                                               │
                                                               ▼
   3. Dashboard useEffect runs:  api.get('/Dashboard/overview')   (axios → port 5000)
                                                               │
                                                               ▼
   4. Backend: DashboardController.Overview() handles GET /api/Dashboard/overview
                                                               │
                                                               ▼
   5. _db.SerialNoHistory...  →  EF Core builds SQL  →  SQL Server (FlexNet)
                                                               │
                                                               ▼
   6. Rows come back → controller computes KPIs/shifts/hourly → return Ok(json)
                                                               │
                                                               ▼
   7. axios resolves → setData(res.data) → React re-renders cards & charts
                                                               │
                                                               ▼
   8. USER sees "178 Engines Today", shift A/B/C, charts  ✅
```

### 5.2 Sequence diagram (text)

```
User        Browser/React        axios          ASP.NET Controller      EF Core        SQL Server
 │   open       │                  │                    │                  │                 │
 │─────────────►│  render Dashboard│                    │                  │                 │
 │              │─── api.get ─────►│                    │                  │                 │
 │              │                  │── GET /overview ──►│                  │                 │
 │              │                  │                    │── LINQ query ───►│                 │
 │              │                  │                    │                  │── SELECT ──────►│
 │              │                  │                    │                  │◄── rows ────────│
 │              │                  │                    │◄── objects ──────│                 │
 │              │                  │◄── 200 + JSON ─────│                  │                 │
 │              │◄── res.data ─────│                    │                  │                 │
 │◄── UI shows ─│  (setData → render)                   │                  │                 │
```

### 5.3 Frontend routing flow

```
URL in address bar ─► BrowserRouter ─► matches a <Route> ─► renders that page component
   "/"               → Login
   "/dashboard"      → Dashboard
   "/model-tracking" → ModelTracking
   anything else     → redirect to "/"
Sidebar <NavLink> changes the URL without a full page reload (SPA navigation).
```

### 5.4 API flow (request/response shape)

```
Frontend                         Backend
api.get('/SerialHistory',        →  GET /api/SerialHistory?page=2&search=64595804
  {params:{page:2, search:...}})
                                 ←  200 OK
                                    { page, pageSize, total, totalPages, rows: [ {...}, ... ] }
```

### 5.5 Backend flow (inside one request)

```
HTTP request → Routing (MapControllers) → matches DashboardController.Overview()
  → constructor injection gives it CmesDbContext (DI from Program.cs)
  → method runs LINQ queries on _db
  → EF Core opens DB connection, runs SQL, maps rows to objects
  → method shapes result → return Ok(object)
  → ASP.NET serializes object to JSON → HTTP 200 response
```

### 5.6 Database flow

```
EF Core LINQ (C#)                         →  SQL sent to SQL Server
_db.SerialNoHistory                          SELECT * FROM MPI_COB_T_SERIAL_NO_HISTORY
   .Where(x => x.SerialNo.Contains("646"))   WHERE SERIALNO LIKE '%646%'
   .OrderByDescending(x => x.CreatedOn)       ORDER BY CREATEDON DESC
   .Skip(50).Take(50)                         OFFSET 50 ROWS FETCH NEXT 50 ROWS ONLY
Connection details come from appsettings.json → "CMES_DB".
```

### 5.7 Authentication flow  ⚠️ NOT IMPLEMENTED YET

```
CURRENT (dummy):
  Login page  → click "Log In"  → setTimeout 1s → navigate('/dashboard')
  (no username/password check, no token, no server call)
  Logout      → navigate('/')   (nothing to clear)

WHAT A REAL VERSION WOULD ADD (future):
  Login form → POST /api/auth/login {user, pass}
            → backend validates (DB / Active Directory)
            → returns a JWT (or sets an HttpOnly cookie)
  Frontend stores token → sends "Authorization: Bearer <token>" on every api call
  Backend [Authorize] attribute + JWT middleware protects endpoints
  Logout → discard token / clear cookie
```
> **Be honest in interviews:** "Auth is currently mocked; here is exactly where and how I'd
> add JWT-based auth." That answer is *stronger* than pretending it exists.

### 5.8 Email flow  ⚠️ NOT IMPLEMENTED
There is **no email** in this project. If needed later (e.g., low-stock alerts), it would be
a backend service using SMTP/SendGrid called from a controller or a background job.

### 5.9 Cookie / JWT flow  ⚠️ NOT IMPLEMENTED
No cookies or JWT today. The session is purely client-side navigation. (See 5.7 for the plan.)

### 5.10 Error-handling flow  ✅ (what actually exists)

```
Frontend (every page):
  api.get(...)
     .then(success → setData)
     .catch(()   → setError("Backend se data nahi mila..."))   // network/500 → friendly message
     .finally(() → setLoading(false))                          // always stop the spinner
  Render: loading? "Loading…"  : error? red message : show data
  → The app never white-screens if the backend is down.

Backend:
  Development mode shows the full exception page (great for debugging — this is how we found
  the SQL "error 40 / server not found" connection issue).
  Unhandled exceptions in a controller → HTTP 500 → frontend's .catch() shows the error state.
```

---

## 6. Where Is Everything Located?

### 6.1 All API routes

| Method | Route | Controller → Function | Real/Mock | Purpose |
|---|---|---|---|---|
| GET | `/api/Dashboard/overview` | `DashboardController.Overview` | ✅ Real | KPIs, shifts, hourly, daily |
| GET | `/api/SerialHistory` | `SerialHistoryController.Get` | ✅ Real | Paginated/searchable rows |
| GET | `/api/SerialHistory/summary` | `SerialHistoryController.GetSummary` | ✅ Real | Totals/unique serials |
| GET | `/api/Production/test` | `ProductionController.Test` | ⚠️ Mock | Health check |
| GET | `/api/Production/summary` | `ProductionController.GetSummary` | ⚠️ Mock | Summary numbers |
| GET | `/api/Production/report` | `ProductionController.GetReport` | ⚠️ Mock | Shift table |
| GET | `/api/Production/hourly` | `ProductionController.GetHourly` | ⚠️ Mock | Hourly data |
| GET | `/api/Wip/test` | `WipController.Test` | ⚠️ Mock | Health check |
| GET | `/api/Wip/summary` | `WipController.GetSummary` | ⚠️ Mock | WIP totals |
| GET | `/api/Wip/locations` | `WipController.GetLocations` | ⚠️ Mock | WIP per location |
| GET | `/api/Engine/test` | `EngineController.Test` | ⚠️ Mock | Health check |
| GET | `/api/Engine/tracking` | `EngineController.GetTracking` | ⚠️ Mock | Engine list |
| GET | `/api/Engine/models` | `EngineController.GetModels` | ⚠️ Mock | Per-model counts |
| GET | `/api/Engine/{engineNo}` | `EngineController.GetByEngineNo` | ⚠️ Mock | One engine |
| GET | `/api/Inventory/test` | `InventoryController.Test` | ⚠️ Mock | Health check |
| GET | `/api/Inventory/summary` | `InventoryController.GetSummary` | ⚠️ Mock | Stock summary |
| GET | `/api/Inventory/items` | `InventoryController.GetItems` | ⚠️ Mock | Stock list |

### 6.2 All controllers
| Controller | File | Talks to DB? |
|---|---|---|
| DashboardController | `backend/Controllers/DashboardController.cs` | ✅ |
| SerialHistoryController | `backend/Controllers/SerialHistoryController.cs` | ✅ |
| ProductionController | `backend/Controllers/ProductionController.cs` | ❌ mock |
| WipController | `backend/Controllers/WipController.cs` | ❌ mock |
| EngineController | `backend/Controllers/EngineController.cs` | ❌ mock |
| InventoryController | `backend/Controllers/InventoryController.cs` | ❌ mock |

### 6.3 All database queries (only the real ones)
| Where | Function | Query (in words) |
|---|---|---|
| `DashboardController.cs` | `Overview` | `MAX(CreatedOn)`; rows for latest day; `COUNT`, `Distinct().Count()`; distinct (date,serial) pairs for daily |
| `SerialHistoryController.cs` | `Get` | `Where(search)` + `Count` + `OrderByDescending` + `Skip/Take` (pagination) |
| `SerialHistoryController.cs` | `GetSummary` | `Count`, distinct `SerialNo`, distinct `Workstation` |

### 6.4 All models
| Model | File | Mapped to |
|---|---|---|
| SerialNoHistory ✅ | `backend/Models/SerialNoHistory.cs` | `MPI_COB_T_SERIAL_NO_HISTORY` |
| Production | `backend/Models/Production.cs` | (future) |
| WIP | `backend/Models/WIP.cs` | (future) |
| Engine | `backend/Models/Engine.cs` | (future) |
| Inventory | `backend/Models/Inventory.cs` | (future) |
| Employee | `backend/Models/Employee.cs` | (future) |

### 6.5 All middleware (in `Program.cs`, in order)
| Middleware | What it does | Custom? |
|---|---|---|
| `UseSwagger` / `UseSwaggerUI` | Serve API docs/test page | No (library) |
| `UseCors("AllowReactApp")` | Allow frontend origin 5173 | Config only |
| `UseAuthorization` | Authorization pipeline | **No-op (no auth configured)** |
| `MapControllers` | Route requests to controllers | No |
> There is **no custom middleware** written in this project.

### 6.6 All "environment variables" / configuration
| Setting | Location | Value |
|---|---|---|
| DB connection string | `appsettings.json` → `ConnectionStrings:CMES_DB` | server/db/login |
| App port | `Properties/launchSettings.json` | `http://localhost:5000` |
| Environment | `launchSettings.json` | `ASPNETCORE_ENVIRONMENT=Development` |
| Frontend API base URL | `frontend/src/api/client.js` | `http://localhost:5000/api` |
| Allowed CORS origin | `Program.cs` | `http://localhost:5173` |
> No `.env` file is used. Config lives in `appsettings.json` (backend) and `client.js` (frontend).

### 6.7 All authentication logic
| Item | Location | Reality |
|---|---|---|
| "Login" | `frontend/src/pages/Login.jsx` → `handleLogin` | Dummy: timer → navigate |
| "Logout" | `frontend/src/components/Sidebar.jsx` → `handleLogout` | Dummy: navigate to `/` |
| Protected routes | — | **None** (any URL is open) |
| JWT/cookies/sessions | — | **None** |

### 6.8 All email logic
**None.** No email anywhere in the project.

### 6.9 All frontend pages
| Page | File | Route |
|---|---|---|
| Login | `frontend/src/pages/Login.jsx` | `/` |
| Dashboard ✅ | `frontend/src/pages/Dashboard.jsx` | `/dashboard` |
| Production Report | `frontend/src/pages/ProductionReport.jsx` | `/production-report` |
| WIP Report | `frontend/src/pages/WipReport.jsx` | `/wip-report` |
| Model Tracking ✅ | `frontend/src/pages/ModelTracking.jsx` | `/model-tracking` |
| Inventory | `frontend/src/pages/Inventory.jsx` | `/inventory` |

### 6.10 All state management
| Item | Location | Tool |
|---|---|---|
| Page data/loading/error | each page | React `useState` |
| Data fetching on load | each page | React `useEffect` + axios |
| Search/pagination state | `ModelTracking.jsx` | `useState` (`page`, `search`) + debounce |
| Active nav highlight | `Sidebar.jsx` | `NavLink` (router state) |
> **No Redux / Zustand / Context.** State is **local component state** only — appropriate for
> this app's size. (If many components needed to share auth/user state, you'd add Context or a
> store.)

---

## 7. If My Mentor Asks Me Questions

> 50 likely questions with ideal, honest answers.

**Architecture & basics**
1. **What is this project?** A 3-tier MES dashboard: React frontend, ASP.NET Core REST API, SQL Server DB. It shows real engine-assembly production data.
2. **Why 3 tiers?** Separation of concerns and security — DB credentials stay in the backend; each layer evolves independently.
3. **Why doesn't the frontend talk to the DB directly?** Security (no DB password in the browser) and to keep business logic centralized and reusable.
4. **What is a REST API?** A set of HTTP endpoints (GET/POST/...) that return data (JSON) over standard web methods.
5. **What is a SPA?** Single Page Application — loads once, then updates parts of the page by fetching JSON, instead of full page reloads.
6. **Which ports run what?** Backend on 5000, frontend dev server on 5173.
7. **How do the two talk?** The frontend's axios calls `http://localhost:5000/api/...`; the backend allows that origin via CORS.
8. **What is CORS and why needed?** Browsers block cross-origin requests by default; CORS is the backend telling the browser "5173 is allowed."

**Backend / C# / ASP.NET**
9. **What is a Controller?** A class that handles HTTP requests for a route group and returns responses (JSON here).
10. **What does `[ApiController]` do?** Marks the class as a Web API controller (model binding, automatic 400s, etc.).
11. **What does `[Route("api/[controller]")]` mean?** Base URL = `/api/<ControllerName>` (e.g., `DashboardController` → `/api/Dashboard`).
12. **What is `Program.cs`?** App startup — registers services (DI) and sets up the request pipeline (middleware), then runs the server.
13. **What is Dependency Injection here?** `Program.cs` registers `CmesDbContext`; ASP.NET injects it into controller constructors automatically.
14. **What is EF Core?** An ORM — it maps C# classes to DB tables and translates LINQ into SQL.
15. **What is an ORM and why use it?** Object-Relational Mapper. Less boilerplate, type-safety, and protection from SQL injection.
16. **How does a C# query become SQL?** EF Core's provider translates LINQ (`Where/OrderBy/Skip/Take`) into a parameterized SQL statement.
17. **What is `DbContext`?** The EF class holding the connection and `DbSet`s (one per table); your gateway to query/save.
18. **What is a `DbSet`?** A queryable collection representing a table (e.g., `DbSet<SerialNoHistory>`).
19. **What is `[Keyless]` and why on `SerialNoHistory`?** It tells EF the entity has no primary key → read-only; perfect for a reporting/history table.
20. **What does `[Table]`/`[Column]` do?** Map the C# class/property names to the exact DB table/column names.
21. **What is `AsNoTracking()`?** Read-only query mode — EF won't track changes, which is faster and lighter for reads.
22. **How is pagination implemented?** `Skip((page-1)*pageSize).Take(pageSize)` → SQL `OFFSET/FETCH`, so only 50 rows come back at a time.
23. **Is search client- or server-side?** Server-side: `Where(x => x.SerialNo.Contains(s))` runs in SQL, so the browser never loads all 10k rows.
24. **How do you compute "today"?** `MAX(CreatedOn)` — the latest date present in the data — so the dashboard always shows the most recent day.
25. **How are shifts A/B/C derived?** From the hour of `CREATEDON`: 6–14 = A, 14–22 = B, else C (the table has no shift column).
26. **Why load a day's rows into memory for the dashboard?** It's only ~1–2k rows; computing distinct counts per shift/hour is simpler and reliable in C# than in one SQL query.
27. **What is Swagger?** Auto-generated API documentation + a test UI at `/swagger`.
28. **What's the difference between async/await here?** DB calls are `await`ed so the thread isn't blocked while waiting on I/O — better scalability.
29. **What does `return Ok(obj)` do?** Returns HTTP 200 with `obj` serialized to JSON.
30. **Real vs mock controllers?** Dashboard & SerialHistory query the DB; Production/Wip/Engine/Inventory return hard-coded demo data until their real tables exist.

**Database**
31. **Which table holds real data?** `dbo.MPI_COB_T_SERIAL_NO_HISTORY` (~10,429 rows), assembly events.
32. **How did the data get in?** Excel export → `generate_insert_sql.py` produced INSERTs → run in SSMS (or `load_to_sql.py` directly).
33. **Why only a subset of the 33 columns mapped?** We only map what the UI displays — simpler model, less data over the wire.
34. **How do you connect to the DB?** Connection string in `appsettings.json` (`Server`, `Database`, login), used by `AddDbContext`.
35. **Windows vs SQL authentication?** `Trusted_Connection=True` = Windows login; otherwise `User Id=...;Password=...` = SQL login.
36. **How would you point this at the production DB?** Change the one connection string (server/db/credentials); no code changes.
37. **What does the data represent?** Each row is a status/scan event for an engine serial at a workstation (assembly application only).

**Frontend / React**
38. **What does `App.jsx` do?** Defines client-side routes (URL → page component) using react-router.
39. **What is `useState`?** A hook to store component state (data, loading, error, page, search).
40. **What is `useEffect`?** A hook to run side-effects (like fetching data) after render / when dependencies change.
41. **Why does the API get called twice in dev?** React 18/19 `StrictMode` double-invokes effects in development to surface bugs; harmless.
42. **What is axios and why a shared client?** A promise-based HTTP client; one configured instance keeps the base URL in one place.
43. **How do you handle loading/error states?** `.then`/`.catch`/`.finally` set `data`/`error`/`loading`; the UI renders accordingly.
44. **How does search debounce work?** A `setTimeout` delays the API call until the user pauses typing, reducing requests.
45. **What state library do you use?** None — local component state with hooks; adequate for this size.
46. **How is the active menu item highlighted?** `NavLink` adds an "active" class based on the current route.

**Honesty / scope / improvements**
47. **Is login real?** No — it's a mocked Windows-SSO screen. Real auth (JWT or AD) is the planned next step.
48. **Is there JWT/cookies/email?** No, not yet — I can explain exactly where they'd plug in.
49. **What are the current limitations?** No auth, read-only (no create/update), 4 pages still on mock data, line-mapping for the shop-floor board still pending domain info.
50. **What would you improve next?** (1) Real authentication, (2) wire the remaining pages to real tables, (3) add write endpoints, (4) caching + a loading skeleton, (5) deploy with proper config/secrets.

---

## 8. How to Explain in an Interview

### ⏱️ 30-second version
> "I built **CMES**, a manufacturing-execution dashboard for an engine plant. It's a 3-tier
> web app: a **React** frontend, an **ASP.NET Core** REST API, and a **SQL Server** database.
> The backend uses **Entity Framework Core** to read ~10,000 rows of real production data and
> exposes JSON endpoints; the React UI shows KPIs, shift-wise summaries, charts, and a
> searchable, paginated table. The frontend never touches the database directly — everything
> goes through the API."

### ⏱️ 2-minute version
> "CMES is a production-monitoring dashboard I built during my internship at Tata Cummins.
> Architecturally it's three tiers. The **frontend** is a React single-page app using
> react-router for navigation and axios to call the backend; charts are done with recharts.
> The **backend** is ASP.NET Core 8 in C#, organized into controllers — each controller is a
> group of REST endpoints. It uses **EF Core** as the ORM, so instead of writing raw SQL I
> write LINQ and EF translates it to parameterized SQL against **SQL Server**.
>
> The real data is an assembly history table with about 10,000 rows. I load a day's data and
> compute KPIs, per-shift counts (I derive the shift from the timestamp since there's no shift
> column), and hourly/daily trends for the Dashboard. The Model-Tracking page does
> **server-side search and pagination**, so even with 10k rows the browser only ever holds 50.
>
> A design decision I'm proud of: the frontend and backend are decoupled by a clean JSON
> contract, and the database connection is a single config line — so pointing it at the real
> production server is a one-line change, no code edits. I also kept some pages on mock data
> with the **same JSON shape** as the real endpoints, so they can be switched to real tables
> later without touching the UI. To be transparent: authentication is currently mocked — the
> next milestone is JWT-based auth."

### 🔬 Deep technical version
> "Request lifecycle: the browser loads `index.html`, `main.jsx` mounts `<App/>`, and
> react-router renders the page for the current route. On mount, a `useEffect` fires an axios
> GET to `http://localhost:5000/api/...`. CORS is configured in `Program.cs` to allow the
> 5173 origin. ASP.NET routes the request via `MapControllers` to the matching controller
> action. The controller receives a `CmesDbContext` through constructor **dependency
> injection** (registered with `AddDbContext` + `UseSqlServer(connectionString)`).
>
> For the Dashboard, I call `MaxAsync(CreatedOn)` to find the latest date, pull that day's
> rows with `AsNoTracking()`, and compute distinct-serial counts per shift/hour in memory —
> chosen over a single SQL aggregate because `COUNT(DISTINCT ...)` grouped multiple ways is
> awkward in one EF query, and the per-day volume is small. For Model-Tracking I keep it in
> SQL: a `Where(...Contains...)` for search, `CountAsync()` for totals, and
> `OrderByDescending(CreatedOn).Skip().Take()` which EF compiles to `OFFSET/FETCH` — true
> server-side pagination.
>
> The entity `SerialNoHistory` is `[Keyless]` with `[Table]`/`[Column]` attributes mapping a
> subset of 33 columns; keyless because it's a read-only history table without a natural single
> key. The controller returns an anonymous object via `Ok(...)`, ASP.NET serializes it to
> JSON, axios resolves the promise, and React commits new state (`setData`) triggering a
> re-render. Errors surface through `.catch` into a friendly error state, and in Development the
> backend's exception page gives full stack traces — that's how I diagnosed a SQL 'error 40'
> connection issue (wrong server in the connection string). Configuration is externalized:
> `appsettings.json` for the DB, `launchSettings.json` for the port, and `client.js` for the
> API base URL — so environment changes need no recompilation of logic."

---

## 9. Things to Understand Before Resume

> Rate your own understanding honestly. Aim to reach **confident** on every Beginner and
> Intermediate item before listing this project.

### Concept ratings

| Concept | Level | Where it appears |
|---|---|---|
| HTTP methods (GET) & status codes (200/500) | 🟢 Beginner | every API call |
| JSON request/response | 🟢 Beginner | axios ↔ controllers |
| Client–server model / 3-tier | 🟢 Beginner | whole app |
| React components & JSX | 🟢 Beginner | every page |
| `useState` / `useEffect` | 🟢 Beginner | every page |
| React Router (routes, NavLink) | 🟢 Beginner | `App.jsx`, `Sidebar` |
| axios + base URL config | 🟢 Beginner | `client.js` |
| C# classes & properties | 🟢 Beginner | Models |
| What an API controller is | 🟢 Beginner | Controllers |
| CORS (what & why) | 🟡 Intermediate | `Program.cs` |
| Dependency Injection | 🟡 Intermediate | `Program.cs` → controllers |
| EF Core / ORM concept | 🟡 Intermediate | `CmesDbContext`, controllers |
| LINQ → SQL translation | 🟡 Intermediate | real controllers |
| Connection strings (Windows vs SQL auth) | 🟡 Intermediate | `appsettings.json` |
| Server-side pagination & search | 🟡 Intermediate | `SerialHistoryController` |
| async/await (I/O-bound) | 🟡 Intermediate | DB calls |
| Debouncing input | 🟡 Intermediate | `ModelTracking.jsx` |
| `[Keyless]` entities / column mapping | 🟡 Intermediate | `SerialNoHistory` |
| Aggregation strategy (SQL vs in-memory) | 🔴 Advanced | `DashboardController` |
| Authentication/JWT design (you'd *add*) | 🔴 Advanced | not yet built |
| Caching, indexing, query tuning | 🔴 Advanced | future |
| Deployment, secrets management, HTTPS | 🔴 Advanced | future |

### Learning roadmap (in order)

**Week 1 — Foundations (🟢)**
- HTTP basics: methods, status codes, request/response, JSON.
- Client–server & 3-tier architecture (draw the diagram from memory).
- React: components, props, `useState`, `useEffect`, conditional rendering.
- React Router + axios: how a page fetches and renders data.
- ✅ Goal: explain the full "user opens Dashboard → data appears" flow without notes.

**Week 2 — Backend & data (🟡)**
- C# basics + what an ASP.NET controller/endpoint is.
- Dependency Injection (why constructors receive `CmesDbContext`).
- EF Core: `DbContext`, `DbSet`, LINQ, and how it becomes SQL (use SQL Server Profiler/logs to *see* the SQL).
- CORS, connection strings, Windows vs SQL auth.
- Server-side pagination + search; async/await for I/O.
- ✅ Goal: change a mock controller to a real DB query yourself.

**Week 3 — Depth & "next steps" (🔴)**
- Authentication: implement a simple JWT login (this is the project's #1 gap — building it teaches a lot and makes a great interview story).
- DB performance: indexes on `SERIALNO`/`CREATEDON`, why pagination needs `ORDER BY`.
- Error handling patterns, logging, and a global exception filter.
- Deployment: environment config, secrets (user-secrets/Key Vault), HTTPS, building the React app for production.
- ✅ Goal: confidently answer "what would you improve and how?"

### Honesty checklist before you list it
- [ ] I can draw the architecture diagram from memory.
- [ ] I can explain DI, EF Core, and CORS in my own words.
- [ ] I can point to where each feature lives in the code.
- [ ] I will say "auth is mocked; here's how I'd add JWT" — not pretend it exists.
- [ ] I understand the difference between the real (Dashboard, Model Tracking) and mock pages.
- [ ] I can explain why pagination/search are server-side.

---

### Quick run reference
```powershell
# Backend (terminal 1)
cd backend ; dotnet run            # → http://localhost:5000  (Swagger at /swagger)

# Frontend (terminal 2)
cd frontend ; npm install ; npm run dev   # → http://localhost:5173
```

> Built during an internship at Tata Cummins. Frontend: React + Vite. Backend: ASP.NET Core 8 +
> EF Core. Database: SQL Server. Status: working prototype — Dashboard & Model Tracking on real
> data; authentication and remaining pages are the next milestones.
