using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HestiaLink.Models
{
    public class ServiceInventory
    {
        [Key]
        [Column("ServiceInventoryID")]
        public int ServiceInventoryId { get; set; }

        [Required]
        [Column("ServiceID")]
        public int ServiceId { get; set; }

        [Required]
        [Column("InventoryItemID")]
        public int InventoryItemId { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal QuantityRequired { get; set; } = 1.0m;

        public DateTime? CreatedDate { get; set; } = DateTime.Now;

        // Navigation Properties
        public Service? Service { get; set; }

        public InventoryItem? InventoryItem { get; set; }
    }
}
