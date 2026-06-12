# CMES — Setup & Connection Guide

Do alag database (alag server bhi ho sakte hain):

| Connection key | DB | Use | Access |
|---|---|---|---|
| `CMES_DB`  | Production DB (data tables) | Dashboard ka saara data | **READ-only** |
| `AUTH_DB`  | Auth DB (sir ka alag server/DB) | `CMES_USERS` login check | READ-only |

> Code dono ko ALAG `DbContextFactory` se connect karta hain — alag server hone se koi farak nahi. **Sirf connection string change karni hai, code mein kuch nahi.**

---

## 1. Connection strings — `backend/appsettings.json`

```json
"ConnectionStrings": {
  "CMES_DB": "Server=PROD_SERVER;Database=PROD_DB;Trusted_Connection=True;TrustServerCertificate=True;",
  "AUTH_DB": "Server=AUTH_SERVER;Database=AUTH_DB;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

### Auth method (do format) — jo sir ke server pe ho wahi use karo

**A) Windows Authentication** (agar app ki Windows identity ko us server pe access hain):
```
Server=AUTH_SERVER;Database=AUTH_DB;Trusted_Connection=True;TrustServerCertificate=True;
```

**B) SQL Authentication** (alag server, alag login — aksar yahi):
```
Server=AUTH_SERVER;Database=AUTH_DB;User Id=<sql_user>;Password=<sql_pass>;TrustServerCertificate=True;
```

- `Server` = SSMS mein jo dikhe (e.g. `10.20.30.40` ya `SQLSRV01\INSTANCE`).
- Named instance ho to `Server=HOST\INSTANCE`. Non-default port ho to `Server=HOST,1433`.
- `CMES_DB` aur `AUTH_DB` alag-alag server/auth ho sakte hain — koi dikkat nahi.

---

## 2. Auth DB mein `CMES_USERS` table (code yahi expect karta hain)

```
UserId    INT (PK)
Username  NVARCHAR   -- WWID, uppercase (e.g. OD741)
FullName  NVARCHAR
Role      NVARCHAR
IsActive  BIT        -- 1 = allowed, 0 = denied
```
Sir ka WWID `IsActive=1` hona chahiye. Table na ho to `database/cmes_users.sql` AUTH DB pe chala do.

> Column/table naam alag ho to sirf `backend/Models/CmesUser.cs` ke `[Table(...)]`/`[Column(...)]` badalne padenge — aur kuch nahi.

---

## 3. Run

```powershell
cd backend
dotnet run            # http://localhost:5000

# naya terminal
cd frontend
npm install
npm run dev           # http://localhost:5173
```

---

## ⚠️ Production DB pe ye scripts KABHI mat chalana
`load_extra_tables.py`, `load_to_sql.py`, `01_create_table.sql`, `generate_insert_sql.py`, `indexes.sql`
— ye sirf LOCAL test DB ke liye hain. Real DB pe tables already hain (crores rows). App sirf **padhta** hain, kabhi likhta nahi.

---

## Summary
Sir ka naya AUTH server/DB ready hone pe → `appsettings.json` mein **`AUTH_DB`** us server pe point karo (upar wala format) → `CMES_USERS` table + WWID active → **auth chal jayega. Code mein koi change nahi.**
