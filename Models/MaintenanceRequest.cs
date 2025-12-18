using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HestiaLink.Models
{
    [Table("MaintenanceRequest")]
    public class MaintenanceRequest
    {
        [Key]
        [Column("RequestID")]
        public int RequestID { get; set; }

        [Column("RoomID")]
        public int RoomID { get; set; }

        [Column("ReportedByUserID")]
        public int ReportedByUserID { get; set; }

        [Required]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, In Progress, Resolved

        public DateTime ReportedDate { get; set; } = DateTime.Now;

        public DateTime? ResolvedDate { get; set; }

        [MaxLength(500)]
        public string? ResolutionNotes { get; set; }

        // Navigation
        [ForeignKey("RoomID")]
        public virtual Room? Room { get; set; }

        [ForeignKey("ReportedByUserID")]
        public virtual SystemUser? ReportedByUser { get; set; }
    }
}
