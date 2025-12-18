using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HestiaLink.Models;

public partial class PayrollEntry
{
    [Key]
    [Column("PayrollEntryID")]
    public int PayrollEntryId { get; set; }

    [Column("PayrollBatchID")]
    public int PayrollBatchId { get; set; }

    [Column("EmployeeID")]
    public int EmployeeId { get; set; }

    public decimal? RegularHours { get; set; }

    public decimal? OvertimeHours { get; set; }

    public decimal? HourlyRate { get; set; }

    public decimal? BaseSalary { get; set; }

    public decimal? RegularPay { get; set; }

    public decimal? OvertimePay { get; set; }

    public decimal? GrossPay { get; set; }

    public decimal? TaxAmount { get; set; }

    public decimal? Deductions { get; set; }

    public decimal? NetPay { get; set; }

    public string Status { get; set; } = "Pending"; // Pending, Paid, Cancelled

    public DateTime? PaidDate { get; set; }

    public string? PaymentMethod { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual PayrollBatch PayrollBatch { get; set; } = null!;

    public virtual Employee Employee { get; set; } = null!;

    // Aliases for different naming conventions
    [NotMapped]
    public int PayrollEntryID { get => PayrollEntryId; set => PayrollEntryId = value; }

    [NotMapped]
    public int PayrollBatchID { get => PayrollBatchId; set => PayrollBatchId = value; }

    [NotMapped]
    public int EmployeeID { get => EmployeeId; set => EmployeeId = value; }
}

