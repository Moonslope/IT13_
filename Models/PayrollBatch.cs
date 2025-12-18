using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HestiaLink.Models;

public partial class PayrollBatch
{
    [Column("PayrollBatchID")]
    public int PayrollBatchId { get; set; }

    public string BatchName { get; set; } = null!;

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public string Status { get; set; } = "Draft"; // Draft, Processing, Completed, Cancelled

    public DateTime? ProcessedDate { get; set; }

    [Column("ProcessedByUserID")]
    public int? ProcessedByUserId { get; set; }

    public decimal? TotalAmount { get; set; }

    public int? EntryCount { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    // PayrollEntry table doesn't exist - marking as NotMapped
    [NotMapped]
    public virtual ICollection<PayrollEntry> PayrollEntries { get; set; } = new List<PayrollEntry>();

    public virtual SystemUser? ProcessedByUser { get; set; }

    // Aliases for different naming conventions
    [NotMapped]
    public int PayrollBatchID { get => PayrollBatchId; set => PayrollBatchId = value; }

    [NotMapped]
    public int? ProcessedByUserID { get => ProcessedByUserId; set => ProcessedByUserId = value; }
}

