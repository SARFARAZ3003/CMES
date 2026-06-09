# Database — MPI_COB_T_SERIAL_NO_HISTORY data load

PROD.xlsx (HISTORY sheet, ~10,429 rows) ka data SQL Server table
`dbo.MPI_COB_T_SERIAL_NO_HISTORY` mein daalne ke liye.

## Files

| File | Kaam |
|---|---|
| `01_create_table.sql` | Table banata hai (agar pehle se nahi hai) |
| `generate_insert_sql.py` | Excel padh ke INSERT wali `.sql` file banata hai |
| `MPI_COB_T_SERIAL_NO_HISTORY_insert.sql` | Generated INSERTs (SSMS mein run karo) |
| `load_to_sql.py` | (Optional) Excel se seedha DB mein insert — ek command |

---

## Tarika 1 — SQL file banao + SSMS mein chalao (recommended)

Ye sabse safe hai. Tum SQL dekh sakte ho phir run kar sakte ho.

```powershell
# 1. Library (ek baar)
python -m pip install openpyxl

# 2. SQL file generate karo
cd C:\Users\Sarfaraz\Desktop\CMES\database
python generate_insert_sql.py
# -> MPI_COB_T_SERIAL_NO_HISTORY_insert.sql ban jaayegi
```

Phir **SSMS** mein:
1. Sahi database select karo (jisme table hai) — top dropdown ya `USE FlexNet;`
2. Table na ho to pehle `01_create_table.sql` Execute karo
3. `MPI_COB_T_SERIAL_NO_HISTORY_insert.sql` kholo → **Execute (F5)**
4. Check: `SELECT COUNT(*) FROM dbo.MPI_COB_T_SERIAL_NO_HISTORY;`

---

## Tarika 2 — Seedha DB mein insert (ek command, fast)

Bada data ho aur baar-baar load karna ho to ye behtar.

```powershell
# Libraries (ek baar)
python -m pip install openpyxl pyodbc

# load_to_sql.py ke andar SERVER / DATABASE / DRIVER check kar lo, phir:
python load_to_sql.py
```

> Note: ye Windows Auth (`Trusted_Connection`) use karta hai aur
> "ODBC Driver 17 for SQL Server" maangta hai. Driver alag ho to
> script ke top mein `DRIVER` badal do.

---

## Naya data aaye to?

Bas `PROD.xlsx` replace karo (ya path badlo script ke top mein) aur
dobara wahi command chala do. Schema same rahega to kuch aur nahi badalta.
