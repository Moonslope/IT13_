using HestiaLink.Data;
using HestiaLink.Models;
using Microsoft.EntityFrameworkCore;

namespace HestiaLink.Services
{
    /// <summary>
    /// Example service showing how to use SyncService for dual-write operations
    /// </summary>
    public class ExampleInventoryService
    {
        private readonly HestiaLinkContext _localContext;
        private readonly SyncService _syncService;

        public ExampleInventoryService(
            HestiaLinkContext localContext,
            SyncService syncService)
        {
            _localContext = localContext;
            _syncService = syncService;
        }

        /// <summary>
        /// Example: Create a new inventory item using dual-write
        /// </summary>
        public async Task<InventoryItem> CreateItemAsync(InventoryItem item)
        {
            // Set default values
            item.CreatedDate = DateTime.Now;
            item.IsActive = true;

            // Use SyncService.SaveDualAsync - saves to local first, then online if available
            // If offline, it will queue for sync automatically
            return await _syncService.SaveDualAsync(item);
        }

        /// <summary>
        /// Example: Update an inventory item using dual-write
        /// </summary>
        public async Task<InventoryItem> UpdateItemAsync(InventoryItem item)
        {
            // Use SyncService.UpdateDualAsync - updates local first, then online if available
            return await _syncService.UpdateDualAsync(item);
        }

        /// <summary>
        /// Example: Delete an inventory item using dual-write
        /// </summary>
        public async Task DeleteItemAsync(InventoryItem item)
        {
            // Use SyncService.DeleteDualAsync - deletes from local first, then online if available
            await _syncService.DeleteDualAsync(item);
        }

        /// <summary>
        /// Example: Read operations use local context (local is source of truth)
        /// </summary>
        public async Task<List<InventoryItem>> GetItemsAsync()
        {
            // Always read from local database
            return await _localContext.InventoryItems
                .Where(i => i.IsActive == true)
                .ToListAsync();
        }

        /// <summary>
        /// Example: Check online status
        /// </summary>
        public async Task<bool> IsSystemOnlineAsync()
        {
            return await _syncService.IsOnlineAsync();
        }

        /// <summary>
        /// Example: Manually trigger sync
        /// </summary>
        public async Task<SyncResult> SyncNowAsync()
        {
            return await _syncService.SyncPendingAsync();
        }
    }
}

