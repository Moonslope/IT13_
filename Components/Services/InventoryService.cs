using HestiaLink.Data;
using HestiaLink.Models;
using Microsoft.EntityFrameworkCore;

namespace HestiaLink.Services
{
    /// <summary>
    /// Service for managing inventory operations, consumption tracking, suppliers, and purchases.
    /// </summary>
    public class InventoryService
    {
        private readonly HestiaLinkContext _context;
        private readonly DualWriteService? _dualWrite;

        public InventoryService(HestiaLinkContext context, DualWriteService? dualWrite = null)
        {
            _context = context;
            _dualWrite = dualWrite;
        }

        #region Inventory Item Operations

        /// <summary>
        /// Gets all active inventory items.
        /// </summary>
        public async Task<List<InventoryItem>> GetActiveItemsAsync()
        {
            return await _context.InventoryItems
                .Include(i => i.Supplier)
                .Where(i => i.IsActive != null && i.IsActive.Value)
                .OrderBy(i => i.ItemName)
                .ToListAsync();
        }

        /// <summary>
        /// Gets all active inventory items (alias for GetActiveItemsAsync).
        /// </summary>
        public async Task<List<InventoryItem>> GetActiveInventoryItemsAsync()
        {
            return await GetActiveItemsAsync();
        }

        /// <summary>
        /// Gets an inventory item by ID.
        /// </summary>
        public async Task<InventoryItem?> GetItemByIdAsync(int itemId)
        {
            return await _context.InventoryItems
                .Include(i => i.Supplier)
                .FirstOrDefaultAsync(i => i.ItemId == itemId);
        }

        /// <summary>
        /// Gets an inventory item by code.
        /// </summary>
        public async Task<InventoryItem?> GetItemByCodeAsync(string itemCode)
        {
            return await _context.InventoryItems
                .Include(i => i.Supplier)
                .FirstOrDefaultAsync(i => i.ItemCode == itemCode);
        }

        /// <summary>
        /// Creates a new inventory item.
        /// </summary>
        public async Task<InventoryItem> CreateItemAsync(InventoryItem item)
        {
            item.CreatedDate = DateTime.Now;
            item.IsActive = true;

            if (_dualWrite != null)
            {
                return await _dualWrite.ExecuteDualWriteAsync(async (context) =>
                {
                    context.InventoryItems.Add(item);
                    await context.SaveChangesAsync();
                    return item;
                }, "InventoryItem", item);
            }
            else
            {
                _context.InventoryItems.Add(item);
                await _context.SaveChangesAsync();
                return item;
            }
        }

        /// <summary>
        /// Updates an existing inventory item.
        /// </summary>
        public async Task<bool> UpdateItemAsync(InventoryItem item)
        {
            if (_dualWrite != null)
            {
                return await _dualWrite.ExecuteDualWriteAsync(async (context) =>
                {
                    var existing = await context.InventoryItems.FirstOrDefaultAsync(i => i.ItemId == item.ItemId);
                    if (existing == null) return false;

                    existing.ItemCode = item.ItemCode;
                    existing.ItemName = item.ItemName;
                    existing.Category = item.Category;
                    existing.UnitOfMeasure = item.UnitOfMeasure;
                    existing.UnitCost = item.UnitCost;
                    existing.CurrentStock = item.CurrentStock;
                    existing.ReorderPoint = item.ReorderPoint;
                    existing.SupplierID = item.SupplierID;
                    existing.ServiceCategoryId = item.ServiceCategoryId;
                    existing.IsServiceItem = item.IsServiceItem;

                    await context.SaveChangesAsync();
                    return true;
                }, "InventoryItem", item);
            }
            else
            {
                var existing = await _context.InventoryItems.FirstOrDefaultAsync(i => i.ItemId == item.ItemId);
                if (existing == null) return false;

                existing.ItemCode = item.ItemCode;
                existing.ItemName = item.ItemName;
                existing.Category = item.Category;
                existing.UnitOfMeasure = item.UnitOfMeasure;
                existing.UnitCost = item.UnitCost;
                existing.CurrentStock = item.CurrentStock;
                existing.ReorderPoint = item.ReorderPoint;
                existing.SupplierID = item.SupplierID;
                existing.ServiceCategoryId = item.ServiceCategoryId;
                existing.IsServiceItem = item.IsServiceItem;

                await _context.SaveChangesAsync();
                return true;
            }
        }

        /// <summary>
        /// Deactivates an inventory item (soft delete).
        /// </summary>
        public async Task<bool> DeactivateItemAsync(int itemId)
        {
            var item = await _context.InventoryItems.FirstOrDefaultAsync(i => i.ItemId == itemId);
            if (item == null) return false;

            item.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Reactivates an inventory item.
        /// </summary>
        public async Task<bool> ReactivateItemAsync(int itemId)
        {
            var item = await _context.InventoryItems.FirstOrDefaultAsync(i => i.ItemId == itemId);
            if (item == null) return false;

            item.IsActive = true;
            await _context.SaveChangesAsync();
            return true;
        }

        #endregion

        #region Supplier Operations

        /// <summary>
        /// Gets all active suppliers.
        /// </summary>
        public async Task<List<Supplier>> GetActiveSuppliersAsync()
        {
            return await _context.Suppliers
                .Where(s => s.IsActive != null && s.IsActive.Value)
                .OrderBy(s => s.SupplierName)
                .ToListAsync();
        }

        /// <summary>
        /// Gets all suppliers (including inactive).
        /// </summary>
        public async Task<List<Supplier>> GetAllSuppliersAsync()
        {
            return await _context.Suppliers
                .OrderBy(s => s.SupplierName)
                .ToListAsync();
        }

        /// <summary>
        /// Gets a supplier by ID.
        /// </summary>
        public async Task<Supplier?> GetSupplierByIdAsync(int supplierId)
        {
            return await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierID == supplierId);
        }

        /// <summary>
        /// Gets a supplier by code.
        /// </summary>
        public async Task<Supplier?> GetSupplierByCodeAsync(string supplierCode)
        {
            return await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierCode == supplierCode);
        }

        /// <summary>
        /// Creates a new supplier.
        /// </summary>
        public async Task<Supplier> CreateSupplierAsync(Supplier supplier)
        {
            supplier.CreatedDate = DateTime.Now;
            supplier.IsActive = true;

            if (_dualWrite != null)
            {
                return await _dualWrite.ExecuteDualWriteAsync(async (context) =>
                {
                    context.Suppliers.Add(supplier);
                    await context.SaveChangesAsync();
                    return supplier;
                }, "Supplier", supplier);
            }
            else
            {
                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();
                return supplier;
            }
        }

        /// <summary>
        /// Updates an existing supplier.
        /// </summary>
        public async Task<bool> UpdateSupplierAsync(Supplier supplier)
        {
            if (_dualWrite != null)
            {
                return await _dualWrite.ExecuteDualWriteAsync(async (context) =>
                {
                    var existing = await context.Suppliers.FirstOrDefaultAsync(s => s.SupplierID == supplier.SupplierID);
                    if (existing == null) return false;

                    existing.SupplierCode = supplier.SupplierCode;
                    existing.SupplierName = supplier.SupplierName;
                    existing.ContactPerson = supplier.ContactPerson;
                    existing.ContactPhone = supplier.ContactPhone;
                    existing.ContactEmail = supplier.ContactEmail;
                    existing.Address = supplier.Address;
                    existing.SupplierType = supplier.SupplierType;
                    existing.UpdatedDate = DateTime.Now;

                    await context.SaveChangesAsync();
                    return true;
                }, "Supplier", supplier);
            }
            else
            {
                var existing = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierID == supplier.SupplierID);
                if (existing == null) return false;

                existing.SupplierCode = supplier.SupplierCode;
                existing.SupplierName = supplier.SupplierName;
                existing.ContactPerson = supplier.ContactPerson;
                existing.ContactPhone = supplier.ContactPhone;
                existing.ContactEmail = supplier.ContactEmail;
                existing.Address = supplier.Address;
                existing.SupplierType = supplier.SupplierType;
                existing.UpdatedDate = DateTime.Now;

                await _context.SaveChangesAsync();
                return true;
            }
        }

        /// <summary>
        /// Deactivates a supplier (soft delete).
        /// </summary>
        public async Task<bool> DeactivateSupplierAsync(int supplierId)
        {
            if (_dualWrite != null)
            {
                return await _dualWrite.ExecuteDualWriteAsync(async (context) =>
                {
                    var supplier = await context.Suppliers.FirstOrDefaultAsync(s => s.SupplierID == supplierId);
                    if (supplier == null) return false;

                    supplier.IsActive = false;
                    supplier.UpdatedDate = DateTime.Now;
                    await context.SaveChangesAsync();
                    return true;
                }, "Supplier", null);
            }
            else
            {
                var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierID == supplierId);
                if (supplier == null) return false;

                supplier.IsActive = false;
                supplier.UpdatedDate = DateTime.Now;
                await _context.SaveChangesAsync();
                return true;
            }
        }

        /// <summary>
        /// Reactivates a supplier.
        /// </summary>
        public async Task<bool> ReactivateSupplierAsync(int supplierId)
        {
            if (_dualWrite != null)
            {
                return await _dualWrite.ExecuteDualWriteAsync(async (context) =>
                {
                    var supplier = await context.Suppliers.FirstOrDefaultAsync(s => s.SupplierID == supplierId);
                    if (supplier == null) return false;

                    supplier.IsActive = true;
                    supplier.UpdatedDate = DateTime.Now;
                    await context.SaveChangesAsync();
                    return true;
                }, "Supplier", null);
            }
            else
            {
                var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierID == supplierId);
                if (supplier == null) return false;

                supplier.IsActive = true;
                supplier.UpdatedDate = DateTime.Now;
                await _context.SaveChangesAsync();
                return true;
            }
        }

        /// <summary>
        /// Gets count of items supplied by a supplier.
        /// </summary>
        public async Task<int> GetSupplierItemCountAsync(int supplierId)
        {
            return await _context.InventoryItems
                .CountAsync(i => i.SupplierID == supplierId && i.IsActive == true);
        }

        /// <summary>
        /// Gets total spend for a supplier (YTD).
        /// </summary>
        public async Task<decimal> GetSupplierTotalSpendAsync(int supplierId, int? year = null)
        {
            var targetYear = year ?? DateTime.Now.Year;
            return await _context.InventoryPurchases
                .Where(p => p.SupplierID == supplierId &&
                           p.PurchaseStatus == "RECEIVED" &&
                           p.ReceivedDate != null &&
                           p.ReceivedDate.Value.Year == targetYear)
                .SumAsync(p => p.TotalAmount ?? 0);
        }

        /// <summary>
        /// Generates a unique supplier code.
        /// </summary>
        public async Task<string> GenerateSupplierCodeAsync()
        {
            var lastSupplier = await _context.Suppliers
                .OrderByDescending(s => s.SupplierID)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastSupplier != null && lastSupplier.SupplierCode.StartsWith("SUP-"))
            {
                if (int.TryParse(lastSupplier.SupplierCode[4..], out int lastNum))
                {
                    nextNumber = lastNum + 1;
                }
            }

            return $"SUP-{nextNumber:D3}";
        }

        #endregion

        #region Purchase Order Operations

        /// <summary>
        /// Gets all purchase orders.
        /// </summary>
        public async Task<List<InventoryPurchase>> GetAllPurchasesAsync()
        {
            return await _context.InventoryPurchases
                .Include(p => p.InventoryItem)
                .Include(p => p.Supplier)
                .OrderByDescending(p => p.PurchaseDate)
                .ToListAsync();
        }

        /// <summary>
        /// Gets purchase orders by status.
        /// </summary>
        public async Task<List<InventoryPurchase>> GetPurchasesByStatusAsync(string status)
        {
            return await _context.InventoryPurchases
                .Include(p => p.InventoryItem)
                .Include(p => p.Supplier)
                .Where(p => p.PurchaseStatus == status)
                .OrderByDescending(p => p.PurchaseDate)
                .ToListAsync();
        }

        /// <summary>
        /// Gets a purchase order by ID.
        /// </summary>
        public async Task<InventoryPurchase?> GetPurchaseByIdAsync(int purchaseId)
        {
            return await _context.InventoryPurchases
                .Include(p => p.InventoryItem)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.PurchaseID == purchaseId);
        }

        /// <summary>
        /// Creates a new purchase order (returns the created purchase).
        /// </summary>
        public async Task<InventoryPurchase> CreatePurchaseOrderAsync(InventoryPurchase purchase)
        {
            purchase.PurchaseNumber = await GeneratePurchaseNumberAsync();
            purchase.CreatedDate = DateTime.Now;
            purchase.PurchaseDate ??= DateTime.Now;
            purchase.PurchaseStatus = "PENDING";
            purchase.CalculateTotal();

            if (_dualWrite != null)
            {
                return await _dualWrite.ExecuteDualWriteAsync(async (context) =>
                {
                    context.InventoryPurchases.Add(purchase);
                    await context.SaveChangesAsync();
                    return purchase;
                }, "InventoryPurchase", purchase);
            }
            else
            {
                _context.InventoryPurchases.Add(purchase);
                await _context.SaveChangesAsync();
                return purchase;
            }
        }

        /// <summary>
        /// Creates a new purchase order (returns bool for success/failure).
        /// </summary>
        public async Task<bool> CreatePurchaseAsync(InventoryPurchase purchase)
        {
            try
            {
                await CreatePurchaseOrderAsync(purchase);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Updates a purchase order.
        /// </summary>
        public async Task<bool> UpdatePurchaseAsync(InventoryPurchase purchase)
        {
            try
            {
                if (_dualWrite != null)
                {
                    return await _dualWrite.ExecuteDualWriteAsync(async (context) =>
                    {
                        var existing = await context.InventoryPurchases
                            .FirstOrDefaultAsync(p => p.PurchaseID == purchase.PurchaseID);
                        
                        if (existing == null) return false;

                        // Only allow editing if status is PENDING
                        if (existing.PurchaseStatus != "PENDING")
                        {
                            throw new InvalidOperationException("Only pending purchase orders can be edited.");
                        }

                        existing.ItemID = purchase.ItemID;
                        existing.SupplierID = purchase.SupplierID;
                        existing.Quantity = purchase.Quantity;
                        existing.UnitPrice = purchase.UnitPrice;
                        existing.TotalAmount = purchase.Quantity * purchase.UnitPrice;
                        existing.Notes = purchase.Notes;
                        existing.PurchaseDate = purchase.PurchaseDate;

                        await context.SaveChangesAsync();
                        return true;
                    }, "InventoryPurchase", purchase);
                }
                else
                {
                    var existing = await _context.InventoryPurchases
                        .FirstOrDefaultAsync(p => p.PurchaseID == purchase.PurchaseID);
                    
                    if (existing == null) return false;

                    // Only allow editing if status is PENDING
                    if (existing.PurchaseStatus != "PENDING")
                    {
                        throw new InvalidOperationException("Only pending purchase orders can be edited.");
                    }

                    existing.ItemID = purchase.ItemID;
                    existing.SupplierID = purchase.SupplierID;
                    existing.Quantity = purchase.Quantity;
                    existing.UnitPrice = purchase.UnitPrice;
                    existing.TotalAmount = purchase.Quantity * purchase.UnitPrice;
                    existing.Notes = purchase.Notes;
                    existing.PurchaseDate = purchase.PurchaseDate;

                    await _context.SaveChangesAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating purchase order: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Marks a purchase as received and updates inventory stock.
        /// </summary>
        public async Task<bool> ReceivePurchaseAsync(int purchaseId)
        {
            var purchase = await _context.InventoryPurchases
                .Include(p => p.InventoryItem)
                .FirstOrDefaultAsync(p => p.PurchaseID == purchaseId);

            if (purchase == null || purchase.PurchaseStatus != "PENDING")
                return false;

            // Update purchase status
            purchase.PurchaseStatus = "RECEIVED";
            purchase.ReceivedDate = DateTime.Now;

            // Update inventory stock
            if (purchase.InventoryItem != null)
            {
                purchase.InventoryItem.CurrentStock = (purchase.InventoryItem.CurrentStock ?? 0) + purchase.Quantity;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Marks a purchase as received with a specific quantity and updates inventory stock.
        /// </summary>
        public async Task<bool> ReceivePurchaseAsync(int purchaseId, int receivedQuantity)
        {
            var purchase = await _context.InventoryPurchases
                .Include(p => p.InventoryItem)
                .FirstOrDefaultAsync(p => p.PurchaseID == purchaseId);

            if (purchase == null || purchase.PurchaseStatus != "PENDING")
                return false;

            // Update purchase status
            purchase.PurchaseStatus = "RECEIVED";
            purchase.ReceivedDate = DateTime.Now;

            // Update inventory stock with received quantity
            if (purchase.InventoryItem != null)
            {
                purchase.InventoryItem.CurrentStock = (purchase.InventoryItem.CurrentStock ?? 0) + receivedQuantity;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Cancels a purchase order.
        /// </summary>
        public async Task<bool> CancelPurchaseAsync(int purchaseId)
        {
            var purchase = await _context.InventoryPurchases.FirstOrDefaultAsync(p => p.PurchaseID == purchaseId);
            if (purchase == null || purchase.PurchaseStatus != "PENDING")
                return false;

            purchase.PurchaseStatus = "CANCELLED";
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Deletes a purchase order (only if pending).
        /// </summary>
        public async Task<bool> DeletePurchaseAsync(int purchaseId)
        {
            var purchase = await _context.InventoryPurchases.FirstOrDefaultAsync(p => p.PurchaseID == purchaseId);
            if (purchase == null || purchase.PurchaseStatus != "PENDING")
                return false;

            _context.InventoryPurchases.Remove(purchase);
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Generates a unique purchase number in format PO-YYYY-XXXX.
        /// </summary>
        public async Task<string> GeneratePurchaseNumberAsync()
        {
            var year = DateTime.Now.Year;
            var count = await _context.InventoryPurchases
                .CountAsync(p => p.PurchaseDate != null && p.PurchaseDate.Value.Year == year);

            return $"PO-{year}-{(count + 1):D4}";
        }

        /// <summary>
        /// Gets pending purchases for low stock items.
        /// </summary>
        public async Task<List<InventoryItem>> GetItemsNeedingPurchaseAsync()
        {
            return await _context.InventoryItems
                .Include(i => i.Supplier)
                .Where(i => i.IsActive == true &&
                           (i.CurrentStock ?? 0) <= (i.ReorderPoint ?? 0))
                .OrderBy(i => i.CurrentStock)
                .ToListAsync();
        }

        #endregion

        #region Stock Operations

        /// <summary>
        /// Adjusts stock for an item (add, subtract, or set).
        /// </summary>
        public async Task<bool> AdjustStockAsync(int itemId, int quantity, StockAdjustmentType adjustmentType)
        {
            var item = await _context.InventoryItems.FirstOrDefaultAsync(i => i.ItemId == itemId);
            if (item == null) return false;

            var currentStock = item.CurrentStock ?? 0;
            item.CurrentStock = adjustmentType switch
            {
                StockAdjustmentType.Add => currentStock + quantity,
                StockAdjustmentType.Subtract => Math.Max(0, currentStock - quantity),
                StockAdjustmentType.Set => quantity,
                _ => currentStock
            };

            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Gets items that are at or below their reorder point.
        /// </summary>
        public async Task<List<InventoryItem>> GetLowStockItemsAsync()
        {
            return await _context.InventoryItems
                .Include(i => i.Supplier)
                .Where(i => i.IsActive != null && i.IsActive.Value && 
                           (i.CurrentStock ?? 0) <= (i.ReorderPoint ?? 0))
                .OrderBy(i => i.CurrentStock ?? 0)
                .ToListAsync();
        }

        /// <summary>
        /// Gets items that are out of stock.
        /// </summary>
        public async Task<List<InventoryItem>> GetOutOfStockItemsAsync()
        {
            return await _context.InventoryItems
                .Include(i => i.Supplier)
                .Where(i => i.IsActive != null && i.IsActive.Value && (i.CurrentStock ?? 0) == 0)
                .OrderBy(i => i.ItemName)
                .ToListAsync();
        }

        /// <summary>
        /// Gets the total value of all inventory.
        /// </summary>
        public async Task<decimal> GetTotalStockValueAsync()
        {
            return await _context.InventoryItems
                .Where(i => i.IsActive != null && i.IsActive.Value)
                .SumAsync(i => (i.CurrentStock ?? 0) * i.UnitCost);
        }

        /// <summary>
        /// Checks if there is sufficient stock for a given quantity.
        /// </summary>
        public async Task<bool> HasSufficientStockAsync(int itemId, decimal requiredQuantity)
        {
            var item = await _context.InventoryItems.FirstOrDefaultAsync(i => i.ItemId == itemId);
            return item != null && (item.CurrentStock ?? 0) >= requiredQuantity;
        }

        #endregion

        #region Service-Inventory Link Operations

        /// <summary>
        /// Gets all inventory items linked to a service.
        /// </summary>
        public async Task<List<ServiceInventory>> GetServiceInventoryLinksAsync(int serviceId)
        {
            return await _context.ServiceInventories
                .Include(si => si.InventoryItem)
                .Where(si => si.ServiceId == serviceId)
                .ToListAsync();
        }

        /// <summary>
        /// Links an inventory item to a service.
        /// </summary>
        public async Task<ServiceInventory> CreateServiceInventoryLinkAsync(int serviceId, int inventoryItemId, decimal quantityRequired)
        {
            // Check if link already exists
            var existing = await _context.ServiceInventories
                .FirstOrDefaultAsync(si => si.ServiceId == serviceId && si.InventoryItemId == inventoryItemId);
            
            if (existing != null)
                throw new InvalidOperationException("This item is already linked to the service.");

            var link = new ServiceInventory
            {
                ServiceId = serviceId,
                InventoryItemId = inventoryItemId,
                QuantityRequired = quantityRequired,
                CreatedDate = DateTime.Now
            };

            _context.ServiceInventories.Add(link);

            // Enable inventory tracking for the service
            var service = await _context.Services.FirstOrDefaultAsync(s => s.ServiceId == serviceId);
            if (service != null && (service.UsesInventory != true))
            {
                service.UsesInventory = true;
                service.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return link;
        }

        /// <summary>
        /// Removes a service-inventory link.
        /// </summary>
        public async Task<bool> RemoveServiceInventoryLinkAsync(int serviceInventoryId)
        {
            var link = await _context.ServiceInventories.FirstOrDefaultAsync(si => si.ServiceInventoryId == serviceInventoryId);
            if (link == null) return false;

            var serviceId = link.ServiceId;
            _context.ServiceInventories.Remove(link);
            await _context.SaveChangesAsync();

            // Check if service still has links, if not disable inventory tracking
            var hasOtherLinks = await _context.ServiceInventories.AnyAsync(si => si.ServiceId == serviceId);
            if (!hasOtherLinks)
            {
                var service = await _context.Services.FirstOrDefaultAsync(s => s.ServiceId == serviceId);
                if (service != null)
                {
                    service.UsesInventory = false;
                    service.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }

            return true;
        }

        /// <summary>
        /// Gets services that have inventory links.
        /// </summary>
        public async Task<List<HestiaLink.Models.Service>> GetServicesWithInventoryAsync()
        {
            var serviceIds = await _context.ServiceInventories
                .Select(si => si.ServiceId)
                .Distinct()
                .ToListAsync();

            return await _context.Services
                .Include(s => s.ServiceCategory)
                .Where(s => serviceIds.Contains(s.ServiceId) && s.IsActive != null && s.IsActive.Value)
                .ToListAsync();
        }

        #endregion

        #region Consumption Operations

        /// <summary>
        /// Records inventory consumption for a service transaction.
        /// </summary>
        public async Task<List<InventoryConsumption>> ConsumeInventoryForServiceAsync(
            int serviceTransactionId, 
            string? roomNumber = null)
        {
            var consumptions = new List<InventoryConsumption>();

            // Get the service transaction
            var transaction = await _context.ServiceTransactions
                .Include(st => st.Service)
                .FirstOrDefaultAsync(st => st.ServiceTransactionId == serviceTransactionId);

            if (transaction?.Service == null)
                return consumptions;

            // Get all inventory links for this service
            var links = await _context.ServiceInventories
                .Include(si => si.InventoryItem)
                .Where(si => si.ServiceId == transaction.ServiceId)
                .ToListAsync();

            // If no links exist, return empty list
            if (!links.Any())
                return consumptions;

            foreach (var link in links)
            {
                if (link.InventoryItem == null || link.InventoryItem.IsActive != true)
                    continue;

                // Calculate quantity to consume (multiply by transaction quantity)
                var transactionQty = (decimal)(transaction.Quantity ?? 1);
                var quantityToConsume = link.QuantityRequired * transactionQty;
                var currentStock = (decimal)(link.InventoryItem.CurrentStock ?? 0);

                // Skip if no stock available
                if (currentStock < quantityToConsume)
                    continue;

                // Create consumption record
                var consumption = new InventoryConsumption
                {
                    ServiceTransactionId = serviceTransactionId,
                    InventoryItemId = link.InventoryItemId,
                    QuantityConsumed = quantityToConsume,
                    ConsumptionDate = DateTime.Now,
                    RoomNumber = roomNumber
                };

                _context.InventoryConsumptions.Add(consumption);

                // Deduct from stock (convert decimal result to int)
                var newStock = Math.Max(0, currentStock - quantityToConsume);
                link.InventoryItem.CurrentStock = (int)Math.Round(newStock);

                consumptions.Add(consumption);
            }

            await _context.SaveChangesAsync();
            return consumptions;
        }

        /// <summary>
        /// Gets consumption history for an inventory item.
        /// </summary>
        public async Task<List<InventoryConsumption>> GetItemConsumptionHistoryAsync(int itemId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.InventoryConsumptions
                .Include(c => c.ServiceTransaction)
                    .ThenInclude(st => st!.Service)
                .Where(c => c.InventoryItemId == itemId);

            if (startDate.HasValue)
                query = query.Where(c => c.ConsumptionDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(c => c.ConsumptionDate <= endDate.Value);

            return await query
                .OrderByDescending(c => c.ConsumptionDate)
                .ToListAsync();
        }

        /// <summary>
        /// Gets all consumption records within a date range.
        /// </summary>
        public async Task<List<InventoryConsumption>> GetConsumptionReportAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.InventoryConsumptions
                .Include(c => c.InventoryItem)
                .Include(c => c.ServiceTransaction)
                    .ThenInclude(st => st!.Service)
                .Where(c => c.ConsumptionDate >= startDate && c.ConsumptionDate <= endDate)
                .OrderByDescending(c => c.ConsumptionDate)
                .ToListAsync();
        }

        /// <summary>
        /// Gets total consumption cost for a date range.
        /// </summary>
        public async Task<decimal> GetTotalConsumptionCostAsync(DateTime startDate, DateTime endDate)
        {
            var consumptions = await _context.InventoryConsumptions
                .Include(c => c.InventoryItem)
                .Where(c => c.ConsumptionDate >= startDate && c.ConsumptionDate <= endDate)
                .ToListAsync();

            return consumptions.Sum(c => (c.InventoryItem?.UnitCost ?? 0) * c.QuantityConsumed);
        }

        #endregion

        #region Dashboard & Statistics

        /// <summary>
        /// Gets inventory dashboard summary statistics.
        /// </summary>
        public async Task<InventoryDashboardSummary> GetDashboardSummaryAsync()
        {
            var items = await _context.InventoryItems
                .Where(i => i.IsActive != null && i.IsActive.Value)
                .ToListAsync();

            var supplierCount = await _context.Suppliers
                .CountAsync(s => s.IsActive == true);

            var pendingPurchases = await _context.InventoryPurchases
                .CountAsync(p => p.PurchaseStatus == "PENDING");

            return new InventoryDashboardSummary
            {
                TotalItems = items.Count,
                LowStockCount = items.Count(i => (i.CurrentStock ?? 0) <= (i.ReorderPoint ?? 0) && (i.CurrentStock ?? 0) > 0),
                OutOfStockCount = items.Count(i => (i.CurrentStock ?? 0) == 0),
                CategoryCount = items.Select(i => i.Category).Distinct().Count(),
                TotalStockValue = items.Sum(i => (i.CurrentStock ?? 0) * i.UnitCost),
                SupplierCount = supplierCount,
                PendingPurchaseCount = pendingPurchases
            };
        }

        /// <summary>
        /// Gets top consumed items for a date range.
        /// </summary>
        public async Task<List<TopConsumedItem>> GetTopConsumedItemsAsync(DateTime startDate, DateTime endDate, int top = 10)
        {
            var consumptions = await _context.InventoryConsumptions
                .Include(c => c.InventoryItem)
                .Where(c => c.ConsumptionDate >= startDate && c.ConsumptionDate <= endDate && c.InventoryItem != null)
                .ToListAsync();

            return consumptions
                .GroupBy(c => c.InventoryItemId)
                .Select(g => new TopConsumedItem
                {
                    ItemId = g.Key,
                    ItemName = g.First().InventoryItem!.ItemName,
                    ItemCode = g.First().InventoryItem!.ItemCode,
                    TotalQuantity = g.Sum(c => c.QuantityConsumed),
                    TotalCost = g.Sum(c => (g.First().InventoryItem?.UnitCost ?? 0) * c.QuantityConsumed)
                })
                .OrderByDescending(i => i.TotalQuantity)
                .Take(top)
                .ToList();
        }

        #endregion

        #region Additional Required Methods

        /// <summary>
        /// Checks if there is sufficient stock available for an item.
        /// </summary>
        public async Task<bool> CheckStockAvailabilityAsync(int itemId, decimal quantity)
        {
            return await HasSufficientStockAsync(itemId, quantity);
        }

        /// <summary>
        /// Deducts stock for a service transaction with room number tracking.
        /// </summary>
        public async Task<bool> DeductStockAsync(int itemId, decimal quantity, string? roomNumber, int? serviceTransactionId)
        {
            var item = await _context.InventoryItems.FirstOrDefaultAsync(i => i.ItemId == itemId);
            if (item == null || (item.CurrentStock ?? 0) < quantity)
                return false;

            // Deduct stock
            item.CurrentStock = (int)Math.Max(0, (item.CurrentStock ?? 0) - quantity);

            // Create consumption record if service transaction is provided
            if (serviceTransactionId.HasValue)
            {
                var consumption = new InventoryConsumption
                {
                    ServiceTransactionId = serviceTransactionId.Value,
                    InventoryItemId = itemId,
                    QuantityConsumed = quantity,
                    ConsumptionDate = DateTime.Now,
                    RoomNumber = roomNumber
                };
                _context.InventoryConsumptions.Add(consumption);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Adds stock to an item (typically from purchase order).
        /// </summary>
        public async Task<bool> AddStockAsync(int itemId, int quantity, int? purchaseId = null)
        {
            var item = await _context.InventoryItems.FirstOrDefaultAsync(i => i.ItemId == itemId);
            if (item == null) return false;

            item.CurrentStock = (item.CurrentStock ?? 0) + quantity;
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Gets inventory requirements for a service based on service category.
        /// </summary>
        public async Task<List<ServiceInventoryRequirement>> GetServiceInventoryRequirementsAsync(int serviceId)
        {
            var requirements = new List<ServiceInventoryRequirement>();
            
            var service = await _context.Services
                .Include(s => s.ServiceCategory)
                .FirstOrDefaultAsync(s => s.ServiceId == serviceId);
            
            if (service == null || service.ServiceCategoryId == null)
                return requirements;

            // Get inventory items for this service's category
            var inventoryItems = await _context.InventoryItems
                .Where(i => i.ServiceCategoryId == service.ServiceCategoryId 
                         && i.IsServiceItem == true 
                         && i.IsActive == true)
                .ToListAsync();

            foreach (var item in inventoryItems)
            {
                int quantityToDeduct = DetermineQuantityToDeduct(item, service);
                
                requirements.Add(new ServiceInventoryRequirement
                {
                    InventoryItemId = item.ItemId,
                    ItemName = item.ItemName,
                    ItemCode = item.ItemCode,
                    RequiredQuantity = quantityToDeduct,
                    AvailableQuantity = item.CurrentStock ?? 0,
                    UnitOfMeasure = item.UnitOfMeasure
                });
            }

            return requirements;
        }

        /// <summary>
        /// Checks if stock is available for a service.
        /// </summary>
        public async Task<bool> CheckStockForServiceAsync(int serviceId)
        {
            var requirements = await GetServiceInventoryRequirementsAsync(serviceId);
            return requirements.All(r => r.AvailableQuantity >= r.RequiredQuantity);
        }

        /// <summary>
        /// Deducts stock for a service based on service category.
        /// </summary>
        public async Task<bool> DeductStockForServiceAsync(int serviceId, string? roomNumber, int serviceTransactionId)
        {
            var service = await _context.Services
                .Include(s => s.ServiceCategory)
                .FirstOrDefaultAsync(s => s.ServiceId == serviceId);
            
            if (service == null || service.ServiceCategoryId == null)
                return true; // No category, nothing to deduct

            // Get inventory items for this service category
            var inventoryItems = await _context.InventoryItems
                .Where(i => i.ServiceCategoryId == service.ServiceCategoryId 
                         && i.IsServiceItem == true 
                         && i.IsActive == true)
                .ToListAsync();

            if (!inventoryItems.Any())
                return true; // No inventory for this category

            foreach (var item in inventoryItems)
            {
                int quantityToDeduct = DetermineQuantityToDeduct(item, service);
                
                // Skip if insufficient stock
                if ((item.CurrentStock ?? 0) < quantityToDeduct)
                    continue;

                // Create consumption record
                var consumption = new InventoryConsumption
                {
                    ServiceTransactionId = serviceTransactionId,
                    InventoryItemId = item.ItemId,
                    QuantityConsumed = quantityToDeduct,
                    ConsumptionDate = DateTime.Now,
                    RoomNumber = roomNumber
                };
                
                _context.InventoryConsumptions.Add(consumption);
                
                // Update stock
                item.CurrentStock = (item.CurrentStock ?? 0) - quantityToDeduct;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Determines quantity to deduct based on service category and item category.
        /// </summary>
        private int DetermineQuantityToDeduct(InventoryItem item, HestiaLink.Models.Service service)
        {
            // CUSTOMIZE THESE RULES FOR YOUR BUSINESS
            return service.ServiceCategory?.ServiceCategoryName?.ToUpper() switch
            {
                "AMENITIES" => item.Category.ToUpper() switch
                {
                    "AMENITIES" => 1,  // 1 set of amenities per service
                    _ => 1
                },
                "SPA & WELLNESS" => item.Category.ToUpper() switch
                {
                    "AMENITIES" => 2,  // Spa uses 2 towels, 2 robes, etc.
                    _ => 1
                },
                "HOUSEKEEPING" => item.Category.ToUpper() switch
                {
                    "CLEANING" => 1,   // 1 cleaning item per service
                    "AMENITIES" => 1,  // Replace amenities during cleaning
                    _ => 1
                },
                _ => 1  // Default
            };
        }

        /// <summary>
        /// Updates service-inventory item mapping.
        /// </summary>
        public async Task<bool> UpdateServiceItemMappingAsync(int serviceId, int itemId, decimal quantity)
        {
            var existing = await _context.ServiceInventories
                .FirstOrDefaultAsync(si => si.ServiceId == serviceId && si.InventoryItemId == itemId);
            
            if (existing == null)
            {
                // Create new link
                await CreateServiceInventoryLinkAsync(serviceId, itemId, quantity);
                return true;
            }
            else
            {
                // Update existing link
                existing.QuantityRequired = quantity;
                await _context.SaveChangesAsync();
                return true;
            }
        }

        /// <summary>
        /// Gets consumption report as a summary.
        /// </summary>
        public async Task<ConsumptionReportSummary> GetConsumptionReportSummaryAsync(DateTime startDate, DateTime endDate)
        {
            var consumptions = await GetConsumptionReportAsync(startDate, endDate);
            var totalCost = await GetTotalConsumptionCostAsync(startDate, endDate);

            return new ConsumptionReportSummary
            {
                TotalConsumptions = consumptions.Count,
                TotalQuantity = consumptions.Sum(c => c.QuantityConsumed),
                TotalCost = totalCost,
                UniqueItems = consumptions.Select(c => c.InventoryItemId).Distinct().Count(),
                UniqueServices = consumptions
                    .Where(c => c.ServiceTransaction != null && c.ServiceTransaction.Service != null)
                    .Select(c => c.ServiceTransaction!.Service!.ServiceId)
                    .Distinct()
                    .Count(),
                UniqueRooms = consumptions
                    .Where(c => !string.IsNullOrEmpty(c.RoomNumber))
                    .Select(c => c.RoomNumber!)
                    .Distinct()
                    .Count()
            };
        }

        /// <summary>
        /// Gets service items count (items marked as IsServiceItem).
        /// </summary>
        public async Task<int> GetServiceItemsCountAsync()
        {
            return await _context.InventoryItems
                .CountAsync(i => i.IsServiceItem == true && i.IsActive == true);
        }

        /// <summary>
        /// Gets today's consumption count.
        /// </summary>
        public async Task<int> GetTodaysConsumptionCountAsync()
        {
            var today = DateTime.Today;
            return await _context.InventoryConsumptions
                .Where(c => c.ConsumptionDate.Date == today)
                .CountAsync();
        }

        /// <summary>
        /// Gets recent consumptions for dashboard.
        /// </summary>
        public async Task<List<RecentConsumption>> GetRecentConsumptionsAsync(int count = 10)
        {
            return await _context.InventoryConsumptions
                .Include(c => c.InventoryItem)
                .Include(c => c.ServiceTransaction)
                    .ThenInclude(st => st!.Service)
                .OrderByDescending(c => c.ConsumptionDate)
                .Take(count)
                .Select(c => new RecentConsumption
                {
                    ItemName = c.InventoryItem != null ? c.InventoryItem.ItemName : "Unknown",
                    QuantityConsumed = c.QuantityConsumed,
                    RoomNumber = c.RoomNumber ?? "N/A",
                    ConsumptionDate = c.ConsumptionDate,
                    ServiceName = c.ServiceTransaction != null && c.ServiceTransaction.Service != null 
                        ? c.ServiceTransaction.Service.ServiceName 
                        : "N/A"
                })
                .ToListAsync();
        }

        #endregion
    }

    #region Supporting Types

    public enum StockAdjustmentType
    {
        Add,
        Subtract,
        Set
    }

    public class InventoryDashboardSummary
    {
        public int TotalItems { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public int CategoryCount { get; set; }
        public decimal TotalStockValue { get; set; }
        public int SupplierCount { get; set; }
        public int PendingPurchaseCount { get; set; }
    }

    public class TopConsumedItem
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public decimal TotalQuantity { get; set; }
        public decimal TotalCost { get; set; }
    }

    public class ServiceInventoryRequirement
    {
        public int InventoryItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public int RequiredQuantity { get; set; }
        public int AvailableQuantity { get; set; }
        public string UnitOfMeasure { get; set; } = string.Empty;
        public bool HasSufficientStock => AvailableQuantity >= RequiredQuantity;
    }

    public class ConsumptionReportSummary
    {
        public int TotalConsumptions { get; set; }
        public decimal TotalQuantity { get; set; }
        public decimal TotalCost { get; set; }
        public int UniqueItems { get; set; }
        public int UniqueServices { get; set; }
        public int UniqueRooms { get; set; }
    }

    public class RecentConsumption
    {
        public string ItemName { get; set; } = string.Empty;
        public decimal QuantityConsumed { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public DateTime ConsumptionDate { get; set; }
        public string ServiceName { get; set; } = string.Empty;
    }

    #endregion
}
