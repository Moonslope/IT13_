using Microsoft.AspNetCore.Mvc;
using CloudApp.Services;

namespace CloudApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncController : ControllerBase
    {
        private readonly SyncService _syncService;
        private readonly ConnectionStatusService _connectionStatusService;
        private readonly PendingChangesTracker _pendingChangesTracker;

        public SyncController(
            SyncService syncService,
            ConnectionStatusService connectionStatusService,
            PendingChangesTracker pendingChangesTracker)
        {
            _syncService = syncService;
            _connectionStatusService = connectionStatusService;
            _pendingChangesTracker = pendingChangesTracker;
        }

        /// <summary>
        /// Manually trigger sync of pending changes
        /// </summary>
        [HttpPost("sync")]
        public async Task<IActionResult> Sync()
        {
            var result = await _syncService.SyncPendingChangesAsync();
            return Ok(result);
        }

        /// <summary>
        /// Get sync status
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            var status = _syncService.GetSyncStatus();
            var isOnline = _connectionStatusService.IsInternetAvailable();
            
            return Ok(new
            {
                PendingCount = status.PendingCount,
                IsSyncing = status.IsSyncing,
                IsOnline = isOnline
            });
        }

        /// <summary>
        /// Get pending changes count
        /// </summary>
        [HttpGet("pending")]
        public IActionResult GetPending()
        {
            var count = _pendingChangesTracker.GetPendingCount();
            return Ok(new { PendingCount = count });
        }
    }
}

