using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HestiaLink.Models
{
    public class InventoryConsumption
    {
        [Key]
        [Column("ConsumptionID")]
        public int ConsumptionId { get; set; }

        [Required]
        [Column("ServiceTransactionID")]
        public int ServiceTransactionId { get; set; }

        [Required]
        [Column("InventoryItemID")]
        public int InventoryItemId { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal QuantityConsumed { get; set; }

        public DateTime ConsumptionDate { get; set; } = DateTime.Now;

        [StringLength(10)]
        public string? RoomNumber { get; set; }

        // Navigation Properties
        [ForeignKey("ServiceTransactionId")]
        public ServiceTransaction? ServiceTransaction { get; set; }

        [ForeignKey("InventoryItemId")]
        public InventoryItem? InventoryItem { get; set; }
    }
}
