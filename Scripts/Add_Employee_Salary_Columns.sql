-- Add salary fields to Employee table
-- Run this script to enable the new payroll features

USE IT13;
GO

-- Check if columns exist before adding them
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Employee' AND COLUMN_NAME = 'HourlyRate')
BEGIN
    ALTER TABLE Employee ADD HourlyRate DECIMAL(18,2) NULL;
    PRINT 'Added HourlyRate column to Employee table';
END
ELSE
BEGIN
    PRINT 'HourlyRate column already exists in Employee table';
END
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Employee' AND COLUMN_NAME = 'BaseSalary')
BEGIN
    ALTER TABLE Employee ADD BaseSalary DECIMAL(18,2) NULL;
    PRINT 'Added BaseSalary column to Employee table';
END
ELSE
BEGIN
    PRINT 'BaseSalary column already exists in Employee table';
END
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Employee' AND COLUMN_NAME = 'SalaryType')
BEGIN
    ALTER TABLE Employee ADD SalaryType NVARCHAR(50) NULL;
    PRINT 'Added SalaryType column to Employee table';
END
ELSE
BEGIN
    PRINT 'SalaryType column already exists in Employee table';
END
GO

PRINT 'Employee salary columns migration completed!';
GO

