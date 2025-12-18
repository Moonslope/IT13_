using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HestiaLink.Models
{
    public class InventoryItem
    {
        [Key]
        [Column("ItemID")]
        public int ItemId { get; set; }

        [Required]
        [StringLength(50)]
        public string ItemCode { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string ItemName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Category { get; set; } = string.Empty; // FOOD, BEVERAGE, AMENITY, CLEANING, MAINTENANCE

        [Required]
        [StringLength(20)]
        public string UnitOfMeasure { get; set; } = string.Empty; // KG, L, PC, BOX, SET

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }

        public int? CurrentStock { get; set; } = 0;

        public int? ReorderPoint { get; set; } = 10;

        public bool? IsActive { get; set; } = true;

        public DateTime? CreatedDate { get; set; } = DateTime.Now;

        // Supplier relationship
        public int? SupplierID { get; set; }

        [ForeignKey("SupplierID")]
        public virtual Supplier? Supplier { get; set; }
        
        // Service Category relationship (for service-based inventory deduction)
        [Column("ServiceCategoryID")]
        public int? ServiceCategoryId { get; set; }

        [ForeignKey("ServiceCategoryId")]
        public virtual ServiceCategory? ServiceCategory { get; set; }

        // Flag to mark if item is used by services
        public bool? IsServiceItem { get; set; } = false;
        
        // Navigation properties for relationships
        public virtual ICollection<InventoryConsumption> InventoryConsumptions { get; set; } = new List<InventoryConsumption>();

        public virtual ICollection<ServiceInventory> ServiceInventories { get; set; } = new List<ServiceInventory>();
    }
}
