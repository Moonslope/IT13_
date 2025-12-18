using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HestiaLink.Models;

/// <summary>
/// Employee Daily Time Record (DTR) - Digital clock in/out tracking
/// </summary>
public partial class EmployeeAttendance
{
    [Key]
    [Column("EmployeeAttendanceID")]
    public int EmployeeAttendanceId { get; set; }

    [Column("EmployeeID")]
    public int EmployeeId { get; set; }

    [Column("UserID")]
    public int? UserId { get; set; } // Reference to SystemUser if employee has account

    public DateOnly AttendanceDate { get; set; }

    public DateTime? ClockInTime { get; set; }

    public DateTime? ClockOutTime { get; set; }

    public decimal? TotalHours { get; set; }

    public decimal? RegularHours { get; set; }

    public decimal? OvertimeHours { get; set; }

    public string Status { get; set; } = "Present"; // Present, Absent, Late, On Leave, Half Day

    public string? ClockInLocation { get; set; } // Optional: GPS or location info

    public string? ClockOutLocation { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual SystemUser? User { get; set; }

    // Aliases for different naming conventions
    [NotMapped]
    public int EmployeeAttendanceID { get => EmployeeAttendanceId; set => EmployeeAttendanceId = value; }

    [NotMapped]
    public int EmployeeID { get => EmployeeId; set => EmployeeId = value; }

    [NotMapped]
    public int? UserID { get => UserId; set => UserId = value; }
}

