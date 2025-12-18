using HestiaLink.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HestiaLink.Services
{
    /// <summary>
    /// Service for dual-write pattern and offline sync
    /// </summary>
    public class SyncService
    {
        private readonly HestiaLinkContext _localContext;
        private readonly HestiaLinkContext _onlineContext;
        private readonly NetworkService _networkService;
        private readonly PendingChangesTracker _pendingChangesTracker;
        private readonly ILogger<SyncService>? _logger;
        private bool _isSyncing = false;

        public SyncService(
            HestiaLinkContext localContext,
            HestiaLinkContext onlineContext,
            NetworkService networkService,
            PendingChangesTracker pendingChangesTracker,
            ILogger<SyncService>? logger = null)
        {
            _localContext = localContext;
            _onlineContext = onlineContext;
            _networkService = networkService;
            _pendingChangesTracker = pendingChangesTracker;
            _logger = logger;
        }

        /// <summary>
        /// Saves entity to both local and online databases (dual-write pattern)
        /// Always saves to local first, then attempts online save
        /// </summary>
        public async Task<TEntity> SaveDualAsync<TEntity>(TEntity entity) where TEntity : class
        {
            // Step 1: ALWAYS save to local database first
            try
            {
                _localContext.Set<TEntity>().Add(entity);
                await _localContext.SaveChangesAsync();
                _logger?.LogInformation($"Successfully saved {typeof(TEntity).Name} to local database");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to save {typeof(TEntity).Name} to local database");
                throw; // If local save fails, we don't proceed
            }

            // Step 2: Try to save to online database if available
            bool isOnline = await _networkService.IsOnlineAsync();
            
            if (isOnline)
            {
                try
                {
                    // Detach entity from local context to avoid tracking conflicts
                    _localContext.Entry(entity).State = EntityState.Detached;
                    
                    // Add to online context
                    _onlineContext.Set<TEntity>().Add(entity);
                    await _onlineContext.SaveChangesAsync();
                    
                    _logger?.LogInformation($"Successfully saved {typeof(TEntity).Name} to online database");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, $"Failed to save {typeof(TEntity).Name} to online database, will queue for sync");
                    
                    // Queue for later sync
                    var entityType = typeof(TEntity).Name;
                    _pendingChangesTracker.AddPendingChange(
                        entityType,
                        "Create",
                        entity,
                        GetEntityId(entity)
                    );
                }
            }
            else
            {
                _logger?.LogInformation($"System is offline, queuing {typeof(TEntity).Name} for sync");
                
                // Queue for later sync
                var entityType = typeof(TEntity).Name;
                _pendingChangesTracker.AddPendingChange(
                    entityType,
                    "Create",
                    entity,
                    GetEntityId(entity)
                );
            }

            return entity;
        }

        /// <summary>
        /// Updates entity in both local and online databases
        /// </summary>
        public async Task<TEntity> UpdateDualAsync<TEntity>(TEntity entity) where TEntity : class
        {
            // Step 1: ALWAYS update local database first
            try
            {
                _localContext.Set<TEntity>().Update(entity);
                await _localContext.SaveChangesAsync();
                _logger?.LogInformation($"Successfully updated {typeof(TEntity).Name} in local database");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to update {typeof(TEntity).Name} in local database");
                throw;
            }

            // Step 2: Try to update online database if available
            bool isOnline = await _networkService.IsOnlineAsync();
            
            if (isOnline)
            {
                try
                {
                    _localContext.Entry(entity).State = EntityState.Detached;
                    _onlineContext.Set<TEntity>().Update(entity);
                    await _onlineContext.SaveChangesAsync();
                    
                    _logger?.LogInformation($"Successfully updated {typeof(TEntity).Name} in online database");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, $"Failed to update {typeof(TEntity).Name} in online database, will queue for sync");
                    
                    var entityType = typeof(TEntity).Name;
                    _pendingChangesTracker.AddPendingChange(
                        entityType,
                        "Update",
                        entity,
                        GetEntityId(entity)
                    );
                }
            }
            else
            {
                _logger?.LogInformation($"System is offline, queuing {typeof(TEntity).Name} update for sync");
                
                var entityType = typeof(TEntity).Name;
                _pendingChangesTracker.AddPendingChange(
                    entityType,
                    "Update",
                    entity,
                    GetEntityId(entity)
                );
            }

            return entity;
        }

        /// <summary>
        /// Deletes entity from both local and online databases
        /// </summary>
        public async Task DeleteDualAsync<TEntity>(TEntity entity) where TEntity : class
        {
            // Step 1: ALWAYS delete from local database first
            try
            {
                _localContext.Set<TEntity>().Remove(entity);
                await _localContext.SaveChangesAsync();
                _logger?.LogInformation($"Successfully deleted {typeof(TEntity).Name} from local database");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to delete {typeof(TEntity).Name} from local database");
                throw;
            }

            // Step 2: Try to delete from online database if available
            bool isOnline = await _networkService.IsOnlineAsync();
            
            if (isOnline)
            {
                try
                {
                    _localContext.Entry(entity).State = EntityState.Detached;
                    _onlineContext.Set<TEntity>().Remove(entity);
                    await _onlineContext.SaveChangesAsync();
                    
                    _logger?.LogInformation($"Successfully deleted {typeof(TEntity).Name} from online database");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, $"Failed to delete {typeof(TEntity).Name} from online database, will queue for sync");
                    
                    var entityType = typeof(TEntity).Name;
                    _pendingChangesTracker.AddPendingChange(
                        entityType,
                        "Delete",
                        entity,
                        GetEntityId(entity)
                    );
                }
            }
            else
            {
                _logger?.LogInformation($"System is offline, queuing {typeof(TEntity).Name} delete for sync");
                
                var entityType = typeof(TEntity).Name;
                _pendingChangesTracker.AddPendingChange(
                    entityType,
                    "Delete",
                    entity,
                    GetEntityId(entity)
                );
            }
        }

        /// <summary>
        /// Checks if system is online
        /// </summary>
        public async Task<bool> IsOnlineAsync()
        {
            return await _networkService.IsOnlineAsync();
        }

        /// <summary>
        /// Syncs all pending changes to the online database
        /// </summary>
        public async Task<SyncResult> SyncPendingAsync()
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
                if (!await _networkService.IsOnlineAsync())
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

                foreach (var change in pendingChanges)
                {
                    try
                    {
                        await SyncChangeAsync(change);
                        _pendingChangesTracker.MarkAsSynced(change.Id);
                        result.SyncedCount++;
                        _logger?.LogInformation($"Synced {change.EntityType} {change.Operation} (ID: {change.Id})");
                    }
                    catch (Exception ex)
                    {
                        result.FailedCount++;
                        result.Errors.Add($"{change.EntityType} {change.Operation}: {ex.Message}");
                        _logger?.LogError(ex, $"Failed to sync {change.EntityType} {change.Operation} (ID: {change.Id})");
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

        private async Task SyncChangeAsync(PendingChange change)
        {
            var entityType = Type.GetType($"HestiaLink.Models.{change.EntityType}");
            if (entityType == null)
            {
                throw new InvalidOperationException($"Unknown entity type: {change.EntityType}");
            }

            switch (change.Operation.ToUpper())
            {
                case "CREATE":
                    await SyncCreateAsync(change, entityType);
                    break;
                case "UPDATE":
                    await SyncUpdateAsync(change, entityType);
                    break;
                case "DELETE":
                    await SyncDeleteAsync(change, entityType);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown operation: {change.Operation}");
            }
        }

        private async Task SyncCreateAsync(PendingChange change, Type entityType)
        {
            if (string.IsNullOrEmpty(change.EntityData)) return;

            var entity = JsonSerializer.Deserialize(change.EntityData, entityType);
            if (entity == null) return;

            _onlineContext.Entry(entity).State = EntityState.Detached;
            _onlineContext.Add(entity);
            await _onlineContext.SaveChangesAsync();
        }

        private async Task SyncUpdateAsync(PendingChange change, Type entityType)
        {
            if (!change.LocalId.HasValue) return;

            // Get the entity from local database to ensure we have the latest data
            var entityTypeName = entityType.Name;
            var dbSetProperty = _localContext.GetType().GetProperty($"{entityTypeName}s");
            if (dbSetProperty == null) return;

            var dbSet = dbSetProperty.GetValue(_localContext);
            if (dbSet == null) return;

            var findMethod = dbSet.GetType().GetMethod("Find", new[] { typeof(object[]) });
            if (findMethod == null) return;

            var localEntity = findMethod.Invoke(dbSet, new object[] { new object[] { change.LocalId.Value } });
            if (localEntity == null) return;

            _onlineContext.Entry(localEntity).State = EntityState.Detached;
            _onlineContext.Update(localEntity);
            await _onlineContext.SaveChangesAsync();
        }

        private async Task SyncDeleteAsync(PendingChange change, Type entityType)
        {
            if (!change.LocalId.HasValue) return;

            var entityTypeName = entityType.Name;
            var dbSetProperty = _onlineContext.GetType().GetProperty($"{entityTypeName}s");
            if (dbSetProperty == null) return;

            var dbSet = dbSetProperty.GetValue(_onlineContext);
            if (dbSet == null) return;

            var findMethod = dbSet.GetType().GetMethod("Find", new[] { typeof(object[]) });
            if (findMethod == null) return;

            var onlineEntity = findMethod.Invoke(dbSet, new object[] { new object[] { change.LocalId.Value } });
            if (onlineEntity == null) return;

            _onlineContext.Remove(onlineEntity);
            await _onlineContext.SaveChangesAsync();
        }

        private int? GetEntityId<TEntity>(TEntity entity)
        {
            try
            {
                var idProperty = typeof(TEntity).GetProperty("Id") 
                    ?? typeof(TEntity).GetProperty($"{typeof(TEntity).Name}Id")
                    ?? typeof(TEntity).GetProperty($"{typeof(TEntity).Name}ID");
                
                if (idProperty != null)
                {
                    var value = idProperty.GetValue(entity);
                    if (value is int intValue)
                        return intValue;
                }
            }
            catch
            {
                // Ignore errors
            }
            return null;
        }
    }

    /// <summary>
    /// Result of sync operation
    /// </summary>
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
}
