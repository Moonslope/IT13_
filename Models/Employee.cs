using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace HestiaLink.Models;

public partial class Employee
{
    public int EmployeeId { get; set; }

    public string FirstName { get; set; } = null!;

    public string? MiddleName { get; set; }

    public string LastName { get; set; } = null!;

    public string? Email { get; set; }

    public string? ContactNumber { get; set; }

    public string? Address { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public int? PositionId { get; set; }

    public DateOnly? HireDate { get; set; }

    public string Status { get; set; } = null!;

    [NotMapped]
    public decimal? HourlyRate { get; set; }

    [NotMapped]
    public decimal? BaseSalary { get; set; }

    [NotMapped]
    public string? SalaryType { get; set; } // Hourly, Monthly, Daily - Add to database when ready

    public virtual Position? Position { get; set; }

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();

    public virtual ICollection<SystemUser> SystemUsers { get; set; } = new List<SystemUser>();

    // PayrollEntry table doesn't exist - marking as NotMapped
    [NotMapped]
    public virtual ICollection<PayrollEntry> PayrollEntries { get; set; } = new List<PayrollEntry>();

    // EmployeeAttendance table doesn't exist - using Attendance table instead
    // This property is kept for compatibility but marked as NotMapped to prevent EF Core from trying to map it
    [NotMapped]
    public virtual ICollection<EmployeeAttendance> EmployeeAttendances { get; set; } = new List<EmployeeAttendance>();

    // Alias for different naming conventions
    [NotMapped]
    public int EmployeeID { get => EmployeeId; set => EmployeeId = value; }
}
