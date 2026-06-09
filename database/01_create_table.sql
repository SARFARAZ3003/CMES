-- Table: dbo.MPI_COB_T_SERIAL_NO_HISTORY
-- Pehle ye table banao, phir MPI_COB_T_SERIAL_NO_HISTORY_insert.sql se data daalo.
-- Agar table pehle se hai to ye skip kar sakte ho.

IF OBJECT_ID('dbo.MPI_COB_T_SERIAL_NO_HISTORY', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MPI_COB_T_SERIAL_NO_HISTORY
    (
        ID                      FLOAT          NULL,
        PRODUCTID               FLOAT          NULL,
        SERIALNO                NVARCHAR(40)   NULL,
        LOTNO                   NVARCHAR(40)   NULL,
        WORKORDERNO             NVARCHAR(40)   NULL,
        WORKSTATION             NVARCHAR(40)   NULL,
        STATUS                  FLOAT          NULL,
        PREVIOUSSTATUS          FLOAT          NULL,
        ENGINEBUILDPROPERTY     FLOAT          NULL,
        LOCATION                NVARCHAR(40)   NULL,
        REPAIRGROUP             NVARCHAR(40)   NULL,
        REINTRODUCEFLAG         FLOAT          NULL,
        REINTRODUCEWORKSTATION  NVARCHAR(40)   NULL,
        LIFTOFFREASON           NVARCHAR(40)   NULL,
        APPLICATION             NVARCHAR(20)   NULL,
        ISLIFTOFFWHENPAUSED     FLOAT          NULL,
        LIFTOFFWORKSTATION      NVARCHAR(40)   NULL,
        REFERENCEID             FLOAT          NULL,
        LASTUPDATEON            DATETIME2      NULL,
        LASTUPDATEDBY           NVARCHAR(50)   NULL,
        CREATEDON               DATETIME2      NULL,
        CREATEDBY               NVARCHAR(50)   NULL,
        ACTIVE                  SMALLINT       NULL,
        LASTDELETEON            DATETIME2      NULL,
        LASTDELETEDBY           NVARCHAR(50)   NULL,
        LASTREACTIVATEON        DATETIME2      NULL,
        LASTREACTIVATEDBY       NVARCHAR(50)   NULL,
        ARCHIVED                FLOAT          NULL,
        LASTARCHIVEON           DATETIME2      NULL,
        LASTARCHIVEDBY          NVARCHAR(50)   NULL,
        LASTRESTOREON           DATETIME2      NULL,
        LASTRESTOREDBY          NVARCHAR(50)   NULL,
        ROWVERSIONSTAMP         NUMERIC(38, 0) NULL
    );
END
GO
