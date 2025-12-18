-- Create EmployeeAttendance table for Digital Time Record (DTR) system
-- This table tracks employee clock-in/clock-out times
-- Run this script in SQL Server Management Studio

USE IT13;
GO

-- Check if table exists before creating
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'EmployeeAttendance')
BEGIN
    CREATE TABLE [dbo].[EmployeeAttendance] (
        [EmployeeAttendanceID] INT IDENTITY(1,1) PRIMARY KEY,
        [EmployeeID] INT NOT NULL,
        [UserID] INT NULL,
        [AttendanceDate] DATE NOT NULL,
        [ClockInTime] DATETIME NULL,
        [ClockOutTime] DATETIME NULL,
        [TotalHours] DECIMAL(18,2) NULL,
        [RegularHours] DECIMAL(18,2) NULL,
        [OvertimeHours] DECIMAL(18,2) NULL,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Present',
        [ClockInLocation] NVARCHAR(255) NULL,
        [ClockOutLocation] NVARCHAR(255) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdatedAt] DATETIME NULL
    );
    
    PRINT 'EmployeeAttendance table created successfully.';
    
    -- Add Foreign Key Constraints
    ALTER TABLE [dbo].[EmployeeAttendance]
    ADD CONSTRAINT FK_EmployeeAttendance_Employee 
        FOREIGN KEY ([EmployeeID]) REFERENCES [dbo].[Employee]([EmployeeID]) ON DELETE CASCADE;
    
    ALTER TABLE [dbo].[EmployeeAttendance]
    ADD CONSTRAINT FK_EmployeeAttendance_SystemUser 
        FOREIGN KEY ([UserID]) REFERENCES [dbo].[SystemUser]([UserID]) ON DELETE SET NULL;
    
    -- Create indexes for better query performance
    CREATE NONCLUSTERED INDEX IX_EmployeeAttendance_EmployeeID 
        ON [dbo].[EmployeeAttendance]([EmployeeID]);
    
    CREATE NONCLUSTERED INDEX IX_EmployeeAttendance_AttendanceDate 
        ON [dbo].[EmployeeAttendance]([AttendanceDate]);
    
    CREATE NONCLUSTERED INDEX IX_EmployeeAttendance_EmployeeID_AttendanceDate 
        ON [dbo].[EmployeeAttendance]([EmployeeID], [AttendanceDate]);
    
    PRINT 'Indexes and foreign keys created successfully.';
END
ELSE
BEGIN
    PRINT 'EmployeeAttendance table already exists.';
    
    -- Add indexes if they don't exist
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EmployeeAttendance_EmployeeID' AND object_id = OBJECT_ID('EmployeeAttendance'))
    BEGIN
        CREATE NONCLUSTERED INDEX IX_EmployeeAttendance_EmployeeID 
            ON [dbo].[EmployeeAttendance]([EmployeeID]);
        PRINT 'Index IX_EmployeeAttendance_EmployeeID created.';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EmployeeAttendance_AttendanceDate' AND object_id = OBJECT_ID('EmployeeAttendance'))
    BEGIN
        CREATE NONCLUSTERED INDEX IX_EmployeeAttendance_AttendanceDate 
            ON [dbo].[EmployeeAttendance]([AttendanceDate]);
        PRINT 'Index IX_EmployeeAttendance_AttendanceDate created.';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EmployeeAttendance_EmployeeID_AttendanceDate' AND object_id = OBJECT_ID('EmployeeAttendance'))
    BEGIN
        CREATE NONCLUSTERED INDEX IX_EmployeeAttendance_EmployeeID_AttendanceDate 
            ON [dbo].[EmployeeAttendance]([EmployeeID], [AttendanceDate]);
        PRINT 'Index IX_EmployeeAttendance_EmployeeID_AttendanceDate created.';
    END
END
GO

PRINT 'EmployeeAttendance table migration completed!';
GO

