-- Dashboard performance ke liye recommended indexes (bade DB pe ZAROORI).
-- Real DB pe ye (ya isse milte-julte) indexes hone chahiye - warna queries slow.
-- IF NOT EXISTS guards - dobara chalane pe error nahi.

-- Old/New (NOT EXISTS first-appearance) + Paint (ws filter) + day-window:
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_HIST_WS_SER_CON')
    CREATE INDEX IX_HIST_WS_SER_CON
    ON dbo.MPI_COB_T_SERIAL_NO_HISTORY (WORKSTATION, SERIALNO, CREATEDON);
GO

-- Test Cell (AMI ws 40200 + day-window):
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AMI_WS_CON')
    CREATE INDEX IX_AMI_WS_CO
    ON dbo.COB_T_AMI_CAPTURE_LOG (WORKSTATION, CREATEDON);
GO

-- FES outbound side (overallstatus + day-window, WIPJOBNO join key):
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OUT_STATUS_CON')
    CREATE INDEX IX_OUT_STATUS_CON
    ON dbo.MPI_COB_T_TRANSACTION_OUTBOUND (OVERALLSTATUS, CREATEDON) INCLUDE (WIPJOBNO);
GO

-- FES serial side (join key WORKORDERNO, filters SERIALNO/STATUS):
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SER_WO')
    CREATE INDEX IX_SER_WO
    ON dbo.MPI_COB_T_SERIAL_NO (WORKORDERNO) INCLUDE (SERIALNO, STATUS);
GO
