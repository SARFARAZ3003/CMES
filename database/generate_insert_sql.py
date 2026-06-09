"""
PROD.xlsx (HISTORY sheet) -> SQL INSERT script generator
---------------------------------------------------------
Excel ki har row ko dbo.MPI_COB_T_SERIAL_NO_HISTORY ke INSERT statement
mein convert karta hai. Output ek .sql file hoti hai jise SSMS mein
Execute kar do.

Run:
    python generate_insert_sql.py

Output:
    MPI_COB_T_SERIAL_NO_HISTORY_insert.sql  (isi folder mein)
"""

import os
import openpyxl

# ---- Config ----
EXCEL_PATH = r"C:\Users\Sarfaraz\Downloads\PROD.xlsx"
SHEET_NAME = "HISTORY"
TABLE_NAME = "dbo.MPI_COB_T_SERIAL_NO_HISTORY"
OUT_PATH = os.path.join(os.path.dirname(__file__), "MPI_COB_T_SERIAL_NO_HISTORY_insert.sql")
BATCH_SIZE = 1000  # SQL Server max 1000 rows per INSERT ... VALUES

# Table ki columns - schema ke order mein (Excel bhi isi order mein hai).
COLUMNS = [
    "ID", "PRODUCTID", "SERIALNO", "LOTNO", "WORKORDERNO", "WORKSTATION",
    "STATUS", "PREVIOUSSTATUS", "ENGINEBUILDPROPERTY", "LOCATION", "REPAIRGROUP",
    "REINTRODUCEFLAG", "REINTRODUCEWORKSTATION", "LIFTOFFREASON", "APPLICATION",
    "ISLIFTOFFWHENPAUSED", "LIFTOFFWORKSTATION", "REFERENCEID", "LASTUPDATEON",
    "LASTUPDATEDBY", "CREATEDON", "CREATEDBY", "ACTIVE", "LASTDELETEON",
    "LASTDELETEDBY", "LASTREACTIVATEON", "LASTREACTIVATEDBY", "ARCHIVED",
    "LASTARCHIVEON", "LASTARCHIVEDBY", "LASTRESTOREON", "LASTRESTOREDBY",
    "ROWVERSIONSTAMP",
]

# Har column ka type: 'num' (FLOAT/SMALLINT/NUMERIC), 'dt' (DATETIME2), 'str' (NVARCHAR)
NUM_IDX = {0, 1, 6, 7, 8, 11, 15, 17, 22, 27, 32}
DT_IDX  = {18, 20, 23, 25, 28, 30}


def fmt_num(v):
    if v is None or (isinstance(v, str) and v.strip() == ""):
        return "NULL"
    f = float(v)
    return str(int(f)) if f.is_integer() else repr(f)


def fmt_dt(v):
    if v is None or (isinstance(v, str) and v.strip() == ""):
        return "NULL"
    # datetime object -> 'YYYY-MM-DD HH:MM:SS.ffffff'
    return "'" + v.strftime("%Y-%m-%d %H:%M:%S.%f") + "'"


def fmt_str(v):
    if v is None:
        return "NULL"
    s = str(v).strip()
    if s == "":
        return "NULL"
    return "N'" + s.replace("'", "''") + "'"


def format_cell(idx, value):
    if idx in NUM_IDX:
        return fmt_num(value)
    if idx in DT_IDX:
        return fmt_dt(value)
    return fmt_str(value)


def main():
    print(f"Reading {EXCEL_PATH} ...")
    wb = openpyxl.load_workbook(EXCEL_PATH, data_only=True)
    ws = wb[SHEET_NAME]

    rows = list(ws.iter_rows(min_row=2, values_only=True))  # row 1 = header, skip
    total = len(rows)
    print(f"{total} data rows mile. Generating SQL ...")

    col_list = ", ".join(COLUMNS)
    written = 0

    with open(OUT_PATH, "w", encoding="utf-8") as f:
        f.write("-- Auto-generated INSERTs for dbo.MPI_COB_T_SERIAL_NO_HISTORY\n")
        f.write("-- Sahi database select karke (USE <db>) Execute karein.\n")
        f.write("SET NOCOUNT ON;\n\n")

        for i in range(0, total, BATCH_SIZE):
            batch = rows[i:i + BATCH_SIZE]
            f.write(f"INSERT INTO {TABLE_NAME} ({col_list}) VALUES\n")
            value_rows = []
            for row in batch:
                cells = [format_cell(c, row[c] if c < len(row) else None)
                         for c in range(len(COLUMNS))]
                value_rows.append("(" + ", ".join(cells) + ")")
            f.write(",\n".join(value_rows))
            f.write(";\nGO\n\n")
            written += len(batch)

        f.write(f"-- Total {written} rows inserted.\n")

    print(f"Done! {written} rows -> {OUT_PATH}")


if __name__ == "__main__":
    main()
