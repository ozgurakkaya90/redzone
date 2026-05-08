-- SQL Server script to create sequence and stored procedure for generating risk codes
-- Run this on the target SQL Server database (e.g., via SSMS or migration)

-- Create a sequence (one global monotonic counter)
IF NOT EXISTS (SELECT * FROM sys.sequences WHERE name = 'RiskSeq')
BEGIN
    CREATE SEQUENCE dbo.RiskSeq
        START WITH 1
        INCREMENT BY 1
        NO CACHE; -- use NO CACHE for strictness; consider CACHE for perf
END
GO

-- Stored procedure to return next risk code formatted as R-<year>-<seq:03d>
IF OBJECT_ID('dbo.sp_GetNextRiskCode', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetNextRiskCode;
GO

CREATE PROCEDURE dbo.sp_GetNextRiskCode @year INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @v BIGINT = NEXT VALUE FOR dbo.RiskSeq;
    DECLARE @code NVARCHAR(50) = CONCAT('R-', @year, '-', RIGHT('000' + CAST(@v AS VARCHAR(20)), 3));
    SELECT @code AS Code;
END;
GO
