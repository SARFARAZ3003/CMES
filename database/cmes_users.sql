-- CMES_USERS: Windows Integrated Authentication ke liye authorization table.
-- Detected Windows user (WWID) yahan active hona chahiye, warna Access Denied.

IF OBJECT_ID('dbo.CMES_USERS', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CMES_USERS
    (
        UserId    INT IDENTITY(1,1) PRIMARY KEY,
        Username  NVARCHAR(100) NOT NULL,                 -- WWID (e.g. OD741) - uppercase store
        FullName  NVARCHAR(150) NULL,
        Role      NVARCHAR(50)  NOT NULL DEFAULT 'Viewer',
        IsActive  BIT           NOT NULL DEFAULT 1,
        CreatedOn DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_CMES_USERS_Username UNIQUE (Username)
    );
END
GO

-- Seed (idempotent). Username hamesha UPPERCASE.
MERGE dbo.CMES_USERS AS t
USING (VALUES
    ('SARFARAZ', 'Sarfaraz Ahmed', 'Admin',    1),  -- local dev Windows user (active)
    ('OD741',    'Sarfaraz Ahmed', 'Admin',    1),  -- WWID example (active)
    ('DEMOUSER', 'Demo Inactive',  'Viewer',   0)   -- inactive -> Access Denied test
) AS s (Username, FullName, Role, IsActive)
ON t.Username = s.Username
WHEN MATCHED THEN UPDATE SET FullName = s.FullName, Role = s.Role, IsActive = s.IsActive
WHEN NOT MATCHED THEN INSERT (Username, FullName, Role, IsActive)
    VALUES (s.Username, s.FullName, s.Role, s.IsActive);
GO

SELECT Username, FullName, Role, IsActive FROM dbo.CMES_USERS ORDER BY Username;
GO
