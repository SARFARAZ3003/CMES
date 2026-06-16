"""
Dummy data generator - 2026 (Jan 1 -> 12 Jun), saare dashboard tables ke liye.
REALISTIC: har metric ka ALAG base + alag daily variation (lines overlap na ho),
weekend kam, shift-weighted times, FES join chalta hain.
Existing Jun 3-10 (real) data SKIP. Re-run pe purana dummy (serial>=70000000) delete karke fresh.

  History (MPI_COB_T_SERIAL_NO_HISTORY) : Old=23800, New=33200, Paint=52000
  AMI     (COB_T_AMI_CAPTURE_LOG)       : Test Cell = 40200
  SerialNo(MPI_COB_T_SERIAL_NO) + Outbound(MPI_COB_T_TRANSACTION_OUTBOUND) : FES

Run:  python gen_dummy_2026.py
"""
import random
from datetime import datetime, date, timedelta
import pyodbc

SERVER = "localhost"
DATABASE = "FlexNet"          # SAFETY: sirf local test DB
DRIVER = "ODBC Driver 17 for SQL Server"

START = date(2026, 1, 1)
END = date(2026, 6, 12)
SKIP = {date(2026, 6, d) for d in range(3, 11)}   # existing real data - skip

# Har metric ka ALAG base (isse 5 lines alag-alag dikhengi, overlap nahi).
BASE_HIST = {"23800": 382, "33200": 366, "52000": 351}  # Old / New / Paint
BASE_TEST = 408   # Test Cell (AMI)
BASE_FES = 339    # FES

random.seed(7)
_serial = 70000000   # 8-digit, existing (64-65M) se alag
_wo = 3900000


def next_serial():
    global _serial
    _serial += 1
    return str(_serial)


def next_wo():
    global _wo
    _wo += 1
    return f"{_wo}-{_wo % 10}"


def mcount(base: int, d: date) -> int:
    """Us din us metric ka count - weekend kam, halka monthly ramp, + independent noise."""
    wf = 0.42 if d.weekday() >= 5 else 1.0       # weekend ~40%
    mf = 0.82 + 0.038 * d.month                  # Jan se Jun halka ramp
    return max(0, int(base * wf * mf) + random.randint(-45, 45))


def rand_utc(d: date) -> datetime:
    """Production day d (IST 06:00->06:00) ka UTC time, shift-weighted (A~45 B~40 C~15)."""
    r = random.random()
    if r < 0.45:
        sec = random.randint(1800, 9 * 3600)          # A: UTC 00:30-09:00
    elif r < 0.85:
        sec = random.randint(9 * 3600, 17 * 3600)     # B: 09:00-17:00
    else:
        sec = random.randint(17 * 3600, 88140)        # C: 17:00- (<d+1 00:30)
    return datetime(d.year, d.month, d.day) + timedelta(seconds=sec)


def main():
    assert DATABASE.lower() == "flexnet", "Sirf local FlexNet test DB pe chalao."
    hist, ami, sn, ob = [], [], [], []

    d = START
    while d <= END:
        if d not in SKIP:
            for ws, base in BASE_HIST.items():                 # Old / New / Paint - independent
                for _ in range(mcount(base, d)):
                    hist.append((next_serial(), ws, rand_utc(d)))
            for _ in range(mcount(BASE_TEST, d)):              # Test Cell
                ami.append(("40200", next_serial(), rand_utc(d)))
            for _ in range(mcount(BASE_FES, d)):               # FES (serial_no + outbound, join)
                s8 = next_serial(); wo = next_wo(); t = rand_utc(d)
                sn.append((s8, wo, random.choice([3, 4]), t))
                ob.append((wo, s8, 3, t))
        d += timedelta(days=1)

    print(f"Generated -> history:{len(hist)} ami:{len(ami)} serial_no:{len(sn)} outbound:{len(ob)}")

    conn = pyodbc.connect(f"DRIVER={{{DRIVER}}};SERVER={SERVER};DATABASE={DATABASE};"
                          f"Trusted_Connection=yes;TrustServerCertificate=yes;")
    cur = conn.cursor()
    cur.fast_executemany = True

    # Purana dummy (serial>=70000000) hata do - real Jun 3-10 (64-65M) untouched.
    print("Cleaning previous dummy ...")
    for tbl in ("dbo.MPI_COB_T_SERIAL_NO_HISTORY", "dbo.COB_T_AMI_CAPTURE_LOG",
                "dbo.MPI_COB_T_SERIAL_NO", "dbo.MPI_COB_T_TRANSACTION_OUTBOUND"):
        cur.execute(f"DELETE FROM {tbl} WHERE TRY_CAST(SERIALNO AS BIGINT) >= 70000000")
    conn.commit()

    def bulk(sql, rows):
        for i in range(0, len(rows), 5000):
            cur.executemany(sql, rows[i:i + 5000])
            conn.commit()

    print("Inserting ...")
    bulk("INSERT INTO dbo.MPI_COB_T_SERIAL_NO_HISTORY (SERIALNO, WORKSTATION, CREATEDON) VALUES (?,?,?)", hist)
    bulk("INSERT INTO dbo.COB_T_AMI_CAPTURE_LOG (WORKSTATION, SERIALNO, CREATEDON) VALUES (?,?,?)", ami)
    bulk("INSERT INTO dbo.MPI_COB_T_SERIAL_NO (SERIALNO, WORKORDERNO, STATUS, CREATEDON) VALUES (?,?,?,?)", sn)
    bulk("INSERT INTO dbo.MPI_COB_T_TRANSACTION_OUTBOUND (WIPJOBNO, SERIALNO, OVERALLSTATUS, CREATEDON) VALUES (?,?,?,?)", ob)

    cur.close()
    conn.close()
    print("Done - varied 2026 dummy data inserted.")


if __name__ == "__main__":
    main()
