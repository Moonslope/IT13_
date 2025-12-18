-- =============================================
-- Create Payroll System Tables
-- Run this script in SQL Server Management Studio
-- Creates: PayrollBatch, PayrollEntry, EmployeeAttendance
-- =============================================

USE IT13;
GO

PRINT 'Starting Payroll System tables migration...';
PRINT '';

-- =============================================
-- 1. CREATE PayrollBatch TABLE
-- =============================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PayrollBatch')
BEGIN
    CREATE TABLE [dbo].[PayrollBatch] (
        [PayrollBatchID] INT IDENTITY(1,1) PRIMARY KEY,
        [BatchName] NVARCHAR(200) NOT NULL,
        [PeriodStart] DATE NOT NULL,
        [PeriodEnd] DATE NOT NULL,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Draft',
        [ProcessedDate] DATETIME NULL,
        [ProcessedByUserID] INT NULL,
        [TotalAmount] DECIMAL(18,2) NULL,
        [EntryCount] INT NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdatedAt] DATETIME NULL
    );
    
    PRINT '✓ PayrollBatch table created successfully.';
    
    -- Add Foreign Key Constraint
    ALTER TABLE [dbo].[PayrollBatch]
    ADD CONSTRAINT FK_PayrollBatch_SystemUser 
        FOREIGN KEY ([ProcessedByUserID]) REFERENCES [dbo].[SystemUser]([UserID]) ON DELETE SET NULL;
    
    -- Create Index
    CREATE NONCLUSTERED INDEX IX_PayrollBatch_PeriodStart 
        ON [dbo].[PayrollBatch]([PeriodStart]);
    
    PRINT '✓ PayrollBatch indexes and foreign keys created.';
END
ELSE
BEGIN
    PRINT '✓ PayrollBatch table already exists.';
END
GO

-- =============================================
-- 2. CREATE PayrollEntry TABLE
-- =============================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PayrollEntry')
BEGIN
    CREATE TABLE [dbo].[PayrollEntry] (
        [PayrollEntryID] INT IDENTITY(1,1) PRIMARY KEY,
        [PayrollBatchID] INT NOT NULL,
        [EmployeeID] INT NOT NULL,
        [PeriodStart] DATE NOT NULL,
        [PeriodEnd] DATE NOT NULL,
        [RegularHours] DECIMAL(10,2) NULL,
        [OvertimeHours] DECIMAL(10,2) NULL,
        [HourlyRate] DECIMAL(18,2) NULL,
        [BaseSalary] DECIMAL(18,2) NULL,
        [RegularPay] DECIMAL(18,2) NULL,
        [OvertimePay] DECIMAL(18,2) NULL,
        [GrossPay] DECIMAL(18,2) NULL,
        [TaxAmount] DECIMAL(18,2) NULL,
        [Deductions] DECIMAL(18,2) NULL,
        [NetPay] DECIMAL(18,2) NULL,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        [Notes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdatedAt] DATETIME NULL
    );
    
    PRINT '✓ PayrollEntry table created successfully.';
    
    -- Add Foreign Key Constraints
    ALTER TABLE [dbo].[PayrollEntry]
    ADD CONSTRAINT FK_PayrollEntry_PayrollBatch 
        FOREIGN KEY ([PayrollBatchID]) REFERENCES [dbo].[PayrollBatch]([PayrollBatchID]) ON DELETE CASCADE;
    
    ALTER TABLE [dbo].[PayrollEntry]
    ADD CONSTRAINT FK_PayrollEntry_Employee 
        FOREIGN KEY ([EmployeeID]) REFERENCES [dbo].[Employee]([EmployeeID]) ON DELETE RESTRICT;
    
    -- Create Indexes
    CREATE NONCLUSTERED INDEX IX_PayrollEntry_PayrollBatchID 
        ON [dbo].[PayrollEntry]([PayrollBatchID]);
    
    CREATE NONCLUSTERED INDEX IX_PayrollEntry_EmployeeID 
        ON [dbo].[PayrollEntry]([EmployeeID]);
    
    PRINT '✓ PayrollEntry indexes and foreign keys created.';
END
ELSE
BEGIN
    PRINT '✓ PayrollEntry table already exists.';
END
GO

-- =============================================
-- 3. CREATE EmployeeAttendance TABLE (DTR)
-- =============================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'EmployeeAttendance')
BEGIN
    CREATE TABLE [dbo].[EmployeeAttendance] (
        [EmployeeAttendanceID] INT IDENTITY(1,1) PRIMARY KEY,
        [EmployeeID] INT NOT NULL,
        [UserID] INT NULL,
        [AttendanceDate] DATE NOT NULL,
        [ClockInTime] DATETIME NULL,
        [ClockOutTime] DATETIME NULL,
        [TotalHours] DECIMAL(10,2) NULL,
        [RegularHours] DECIMAL(10,2) NULL,
        [OvertimeHours] DECIMAL(10,2) NULL,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Present',
        [ClockInLocation] NVARCHAR(255) NULL,
        [ClockOutLocation] NVARCHAR(255) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdatedAt] DATETIME NULL
    );
    
    PRINT '✓ EmployeeAttendance table created successfully.';
    
    -- Add Foreign Key Constraints
    ALTER TABLE [dbo].[EmployeeAttendance]
    ADD CONSTRAINT FK_EmployeeAttendance_Employee 
        FOREIGN KEY ([EmployeeID]) REFERENCES [dbo].[Employee]([EmployeeID]) ON DELETE CASCADE;
    
    ALTER TABLE [dbo].[EmployeeAttendance]
    ADD CONSTRAINT FK_EmployeeAttendance_SystemUser 
        FOREIGN KEY ([UserID]) REFERENCES [dbo].[SystemUser]([UserID]) ON DELETE SET NULL;
    
    -- Create Indexes for better query performance
    CREATE NONCLUSTERED INDEX IX_EmployeeAttendance_EmployeeID 
        ON [dbo].[EmployeeAttendance]([EmployeeID]);
    
    CREATE NONCLUSTERED INDEX IX_EmployeeAttendance_AttendanceDate 
        ON [dbo].[EmployeeAttendance]([AttendanceDate]);
    
    CREATE NONCLUSTERED INDEX IX_EmployeeAttendance_EmployeeID_AttendanceDate 
        ON [dbo].[EmployeeAttendance]([EmployeeID], [AttendanceDate]);
    
    PRINT '✓ EmployeeAttendance indexes and foreign keys created.';
END
ELSE
BEGIN
    PRINT '✓ EmployeeAttendance table already exists.';
    
    -- Add indexes if they don't exist
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EmployeeAttendance_EmployeeID' AND object_id = OBJECT_ID('EmployeeAttendance'))
    BEGIN
        CREATE NONCLUSTERED INDEX IX_EmployeeAttendance_EmployeeID 
            ON [dbo].[EmployeeAttendance]([EmployeeID]);
        PRINT '✓ Index IX_EmployeeAttendance_EmployeeID created.';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EmployeeAttendance_AttendanceDate' AND object_id = OBJECT_ID('EmployeeAttendance'))
    BEGIN
        CREATE NONCLUSTERED INDEX IX_EmployeeAttendance_AttendanceDate 
            ON [dbo].[EmployeeAttendance]([AttendanceDate]);
        PRINT '✓ Index IX_EmployeeAttendance_AttendanceDate created.';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EmployeeAttendance_EmployeeID_AttendanceDate' AND object_id = OBJECT_ID('EmployeeAttendance'))
    BEGIN
        CREATE NONCLUSTERED INDEX IX_EmployeeAttendance_EmployeeID_AttendanceDate 
            ON [dbo].[EmployeeAttendance]([EmployeeID], [AttendanceDate]);
        PRINT '✓ Index IX_EmployeeAttendance_EmployeeID_AttendanceDate created.';
    END
END
GO

PRINT '';
PRINT '=============================================';
PRINT 'Payroll System tables migration completed!';
PRINT '=============================================';
GO

