using HestiaLink.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using CloudApp.Services;
using CloudApp.Data;

namespace CloudApp.Services
{
    /// <summary>
    /// Service to sync pending changes from local database to online database
    /// </summary>
    public class SyncService
    {
        private readonly IDbContextFactory<HestiaLinkContext> _localContextFactory;
        private readonly OnlineDbContextFactory _onlineContextFactory;
        private readonly ConnectionStatusService _connectionStatusService;
        private readonly PendingChangesTracker _pendingChangesTracker;
        private readonly ILogger<SyncService>? _logger;
        private bool _isSyncing = false;

        public SyncService(
            IDbContextFactory<HestiaLinkContext> localContextFactory,
            OnlineDbContextFactory onlineContextFactory,
            ConnectionStatusService connectionStatusService,
            PendingChangesTracker pendingChangesTracker,
            ILogger<SyncService>? logger = null)
        {
            _localContextFactory = localContextFactory;
            _onlineContextFactory = onlineContextFactory;
            _connectionStatusService = connectionStatusService;
            _pendingChangesTracker = pendingChangesTracker;
            _logger = logger;
        }

        /// <summary>
        /// Syncs all pending changes to the online database
        /// </summary>
        public async Task<SyncResult> SyncPendingChangesAsync()
        {
            if (_isSyncing)
            {
                _logger?.LogWarning("Sync already in progress, skipping");
                return new SyncResult { IsRunning = true };
            }

            _isSyncing = true;
            var result = new SyncResult { StartedAt = DateTime.UtcNow };

            try
            {
                // Check if online
                if (!await _connectionStatusService.IsOnlineAsync())
                {
                    result.Message = "System is offline, cannot sync";
                    result.Success = false;
                    return result;
                }

                var pendingChanges = _pendingChangesTracker.GetPendingChanges();
                result.TotalPending = pendingChanges.Count;

                if (pendingChanges.Count == 0)
                {
                    result.Message = "No pending changes to sync";
                    result.Success = true;
                    return result;
                }

                _logger?.LogInformation($"Starting sync of {pendingChanges.Count} pending changes");

                using var localContext = _localContextFactory.CreateDbContext();
                using var onlineContext = _onlineContextFactory.CreateDbContext();

                foreach (var change in pendingChanges)
                {
                    try
                    {
                        await SyncChangeAsync(change, localContext, onlineContext);
                        _pendingChangesTracker.MarkAsSynced(change.Id);
                        result.SyncedCount++;
                        _logger?.LogInformation($"Synced {change.EntityType} {change.Operation} (ID: {change.Id})");
                    }
                    catch (Exception ex)
                    {
                        result.FailedCount++;
                        result.Errors.Add($"{change.EntityType} {change.Operation}: {ex.Message}");
                        _logger?.LogError(ex, $"Failed to sync {change.EntityType} {change.Operation} (ID: {change.Id})");
                        // Continue with next change
                    }
                }

                result.Success = result.FailedCount == 0;
                result.Message = $"Synced {result.SyncedCount} of {result.TotalPending} changes";
                result.CompletedAt = DateTime.UtcNow;

                // Cleanup old synced changes
                _pendingChangesTracker.CleanupSyncedChanges();

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Sync failed: {ex.Message}";
                result.CompletedAt = DateTime.UtcNow;
                _logger?.LogError(ex, "Sync operation failed");
                return result;
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private async Task SyncChangeAsync(
            PendingChange change,
            HestiaLinkContext localContext,
            HestiaLinkContext onlineContext)
        {
            // Deserialize the entity data
            var entityType = Type.GetType($"HestiaLink.Models.{change.EntityType}");
            if (entityType == null)
            {
                throw new InvalidOperationException($"Unknown entity type: {change.EntityType}");
            }

            object? entity = null;
            if (!string.IsNullOrEmpty(change.EntityData))
            {
                entity = JsonSerializer.Deserialize(change.EntityData, entityType);
            }

            // Execute the operation based on type
            switch (change.Operation.ToUpper())
            {
                case "CREATE":
                    if (entity != null)
                    {
                        await SyncCreateAsync(entity, entityType, onlineContext);
                    }
                    break;
                case "UPDATE":
                    await SyncUpdateAsync(entity!, entityType, change.LocalId, localContext, onlineContext);
                    break;
                case "DELETE":
                    await SyncDeleteAsync(entity!, entityType, change.LocalId, onlineContext);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown operation: {change.Operation}");
            }
        }

        private async Task SyncCreateAsync(object entity, Type entityType, HestiaLinkContext onlineContext)
        {
            var addMethod = typeof(DbContext).GetMethod("Add", new[] { typeof(object) });
            if (addMethod == null) return;

            addMethod.Invoke(onlineContext, new[] { entity });
            await onlineContext.SaveChangesAsync();
        }

        private async Task SyncUpdateAsync(
            object entity,
            Type entityType,
            int? localId,
            HestiaLinkContext localContext,
            HestiaLinkContext onlineContext)
        {
            if (!localId.HasValue) return;

            // Get the entity from local context to ensure we have the latest data
            var entityTypeName = entityType.Name;
            var dbSetProperty = localContext.GetType().GetProperty($"{entityTypeName}s");
            if (dbSetProperty == null) return;

            var dbSet = dbSetProperty.GetValue(localContext);
            if (dbSet == null) return;

            // Find the entity in local database
            var findMethod = dbSet.GetType().GetMethod("Find", new[] { typeof(object[]) });
            if (findMethod == null) return;

            var localEntity = findMethod.Invoke(dbSet, new object[] { new object[] { localId.Value } });
            if (localEntity == null) return;

            // Update online database
            var updateMethod = typeof(DbContext).GetMethod("Update", new[] { typeof(object) });
            if (updateMethod == null) return;

            updateMethod.Invoke(onlineContext, new[] { localEntity });
            await onlineContext.SaveChangesAsync();
        }

        private async Task SyncDeleteAsync(object entity, Type entityType, int? localId, HestiaLinkContext onlineContext)
        {
            if (!localId.HasValue) return;

            var entityTypeName = entityType.Name;
            var dbSetProperty = onlineContext.GetType().GetProperty($"{entityTypeName}s");
            if (dbSetProperty == null) return;

            var dbSet = dbSetProperty.GetValue(onlineContext);
            if (dbSet == null) return;

            var findMethod = dbSet.GetType().GetMethod("Find", new[] { typeof(object[]) });
            if (findMethod == null) return;

            var onlineEntity = findMethod.Invoke(dbSet, new object[] { new object[] { localId.Value } });
            if (onlineEntity == null) return;

            var removeMethod = typeof(DbContext).GetMethod("Remove", new[] { typeof(object) });
            if (removeMethod == null) return;

            removeMethod.Invoke(onlineContext, new[] { onlineEntity });
            await onlineContext.SaveChangesAsync();
        }

        /// <summary>
        /// Gets sync status
        /// </summary>
        public SyncStatus GetSyncStatus()
        {
            return new SyncStatus
            {
                PendingCount = _pendingChangesTracker.GetPendingCount(),
                IsSyncing = _isSyncing
            };
        }
    }

    public class SyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalPending { get; set; }
        public int SyncedCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsRunning { get; set; }
    }

    public class SyncStatus
    {
        public int PendingCount { get; set; }
        public bool IsSyncing { get; set; }
    }
}

