using HestiaLink.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CloudApp.Data;

namespace CloudApp.Services
{
    /// <summary>
    /// Service for dual-write pattern: writes to local database first, then online database
    /// </summary>
    public class DualWriteService
    {
        private readonly IDbContextFactory<HestiaLinkContext> _localContextFactory;
        private readonly OnlineDbContextFactory _onlineContextFactory;
        private readonly ConnectionStatusService _connectionStatusService;
        private readonly PendingChangesTracker _pendingChangesTracker;
        private readonly ILogger<DualWriteService>? _logger;

        public DualWriteService(
            IDbContextFactory<HestiaLinkContext> localContextFactory,
            OnlineDbContextFactory onlineContextFactory,
            ConnectionStatusService connectionStatusService,
            PendingChangesTracker pendingChangesTracker,
            ILogger<DualWriteService>? logger = null)
        {
            _localContextFactory = localContextFactory;
            _onlineContextFactory = onlineContextFactory;
            _connectionStatusService = connectionStatusService;
            _pendingChangesTracker = pendingChangesTracker;
            _logger = logger;
        }

        /// <summary>
        /// Executes a write operation with dual-write pattern
        /// </summary>
        public async Task<TResult> ExecuteDualWriteAsync<TResult>(
            Func<HestiaLinkContext, Task<TResult>> operation,
            string entityType,
            object? entityForTracking = null)
        {
            TResult? localResult = default;
            Exception? onlineException = null;

            // Step 1: Always write to local database first
            try
            {
                using var localContext = _localContextFactory.CreateDbContext();
                localResult = await operation(localContext);
                await localContext.SaveChangesAsync();
                _logger?.LogInformation($"Successfully wrote {entityType} to local database");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to write {entityType} to local database");
                throw; // If local write fails, we don't proceed
            }

            // Step 2: Try to write to online database if available
            bool isOnline = await _connectionStatusService.IsOnlineAsync();
            
            if (isOnline)
            {
                try
                {
                    using var onlineContext = _onlineContextFactory.CreateDbContext();
                    
                    // Re-execute the operation for online database
                    // Note: This assumes the operation can be safely re-executed
                    // For updates, you might need to reload the entity first
                    var onlineResult = await operation(onlineContext);
                    await onlineContext.SaveChangesAsync();
                    
                    _logger?.LogInformation($"Successfully wrote {entityType} to online database");
                }
                catch (Exception ex)
                {
                    onlineException = ex;
                    _logger?.LogWarning(ex, $"Failed to write {entityType} to online database, will queue for sync");
                    
                    // Queue for later sync if entity is provided
                    if (entityForTracking != null)
                    {
                        _pendingChangesTracker.AddPendingChange(
                            entityType,
                            "Create", // or "Update" - you may need to detect this
                            entityForTracking
                        );
                    }
                }
            }
            else
            {
                _logger?.LogInformation($"System is offline, queuing {entityType} for sync");
                
                // Queue for later sync
                if (entityForTracking != null)
                {
                    _pendingChangesTracker.AddPendingChange(
                        entityType,
                        "Create", // or "Update" - you may need to detect this
                        entityForTracking
                    );
                }
            }

            // Return local result (local is always the source of truth)
            return localResult!;
        }

        /// <summary>
        /// Executes a write operation without return value
        /// </summary>
        public async Task ExecuteDualWriteAsync(
            Func<HestiaLinkContext, Task> operation,
            string entityType,
            object? entityForTracking = null)
        {
            await ExecuteDualWriteAsync<object?>(async (context) =>
            {
                await operation(context);
                return null;
            }, entityType, entityForTracking);
        }

        /// <summary>
        /// Reads from local database (local is always the source of truth for reads)
        /// </summary>
        public async Task<TResult> ReadFromLocalAsync<TResult>(
            Func<HestiaLinkContext, Task<TResult>> operation)
        {
            using var localContext = _localContextFactory.CreateDbContext();
            return await operation(localContext);
        }
    }
}

