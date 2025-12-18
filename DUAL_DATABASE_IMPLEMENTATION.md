# Dual-Database System Implementation

## Complete Implementation Guide

This document provides the complete implementation of the dual-database system with offline/online sync for the hospitality system.

## Architecture Overview

```
┌─────────────────┐
│   User Action   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  SyncService    │
│  SaveDualAsync  │
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
    ▼         ▼
┌────────┐ ┌──────────┐
│ Local  │ │ Online  │
│  DB    │ │   DB    │
└────────┘ └──────────┘
    │         │
    │    ┌────┴────┐
    │    │ Online? │
    │    └────┬────┘
    │         │
    │    ┌────┴────┐
    │    │  Yes    │
    │    │  Save   │
    │    └─────────┘
    │
    └──────────────┐
                   │
                   ▼
         ┌─────────────────┐
         │ PendingChanges  │
         │    Tracker      │
         └─────────────────┘
                   │
                   ▼
         ┌─────────────────┐
         │ Background Sync │
         │  (Every 30 sec)  │
         └─────────────────┘
```

## Components

### 1. NetworkService.cs
- Checks internet connectivity
- Checks database server reachability
- Caches results for performance

### 2. SyncService.cs
- `SaveDualAsync<TEntity>(entity)` - Saves to both databases
- `UpdateDualAsync<TEntity>(entity)` - Updates in both databases
- `DeleteDualAsync<TEntity>(entity)` - Deletes from both databases
- `IsOnlineAsync()` - Checks if system is online
- `SyncPendingAsync()` - Syncs pending records when online

### 3. HestiaLinkContext.cs
- Supports factory pattern for dual contexts
- Configured via DI (no hardcoded connection strings)

### 4. MauiProgram.cs
- Local DbContext (scoped)
- Online DbContext (scoped)
- SyncService (scoped)
- NetworkService (singleton)
- PendingChangesTracker (singleton)

## Usage Examples

### Example 1: Using SyncService in a Service

```csharp
public class InventoryService
{
    private readonly HestiaLinkContext _localContext;
    private readonly SyncService _syncService;

    public InventoryService(
        HestiaLinkContext localContext,
        SyncService syncService)
    {
        _localContext = localContext;
        _syncService = syncService;
    }

    public async Task<InventoryItem> CreateItemAsync(InventoryItem item)
    {
        item.CreatedDate = DateTime.Now;
        item.IsActive = true;

        // Use dual-write service
        return await _syncService.SaveDualAsync(item);
    }

    public async Task<InventoryItem> UpdateItemAsync(InventoryItem item)
    {
        // Use dual-write service
        return await _syncService.UpdateDualAsync(item);
    }

    public async Task DeleteItemAsync(InventoryItem item)
    {
        // Use dual-write service
        await _syncService.DeleteDualAsync(item);
    }

    // Read operations use local context (local is source of truth)
    public async Task<List<InventoryItem>> GetItemsAsync()
    {
        return await _localContext.InventoryItems.ToListAsync();
    }
}
```

### Example 2: Using in a Blazor Component

```csharp
@inject SyncService SyncService
@inject IDbContextFactory<HestiaLinkContext> ContextFactory

@code {
    private InventoryItem CurrentItem = new();

    private async Task SaveItem()
    {
        try
        {
            // Save to both databases
            await SyncService.SaveDualAsync(CurrentItem);
            await JSRuntime.InvokeVoidAsync("alert", "Item saved successfully!");
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", $"Error: {ex.Message}");
        }
    }

    private async Task CheckOnlineStatus()
    {
        var isOnline = await SyncService.IsOnlineAsync();
        await JSRuntime.InvokeVoidAsync("alert", isOnline ? "System is online" : "System is offline");
    }
}
```

### Example 3: Manual Sync

```csharp
@inject SyncService SyncService

@code {
    private async Task SyncNow()
    {
        var result = await SyncService.SyncPendingAsync();
        
        if (result.Success)
        {
            await JSRuntime.InvokeVoidAsync("alert", 
                $"Synced {result.SyncedCount} of {result.TotalPending} changes");
        }
        else
        {
            await JSRuntime.InvokeVoidAsync("alert", 
                $"Sync failed: {result.Message}");
        }
    }
}
```

## Connection Strings

### Local Database
```
Data Source=MSI\SQLEXPRESS;Initial Catalog=IT13;Integrated Security=True;Connect Timeout=30;Encrypt=True;Trust Server Certificate=True;Application Intent=ReadWrite;Multi Subnet Failover=False;Command Timeout=30
```

### Online Database
```
Server=db35282.databaseasp.net;Database=db35282;User Id=db35282;Password=c@3E=4Akw#6H;Encrypt=False;MultipleActiveResultSets=True;TrustServerCertificate=True;Connection Timeout=30;
```

## Flow Diagram

### Save Operation Flow

```
1. User performs action (Create/Update/Delete)
   ↓
2. SyncService.SaveDualAsync() called
   ↓
3. Save to LOCAL database (always first)
   ├─ Success → Continue
   └─ Failure → Throw exception (stop)
   ↓
4. Check if ONLINE (NetworkService.IsOnlineAsync())
   ├─ YES → Save to ONLINE database
   │         ├─ Success → Done ✅
   │         └─ Failure → Queue for sync
   └─ NO → Queue for sync
   ↓
5. Background sync service periodically syncs queued changes
```

### Sync Operation Flow

```
1. Background service checks every 30 seconds
   ↓
2. NetworkService.IsOnlineAsync()
   ├─ NO → Skip sync
   └─ YES → Continue
   ↓
3. SyncService.SyncPendingAsync()
   ↓
4. For each pending change:
   ├─ CREATE → Add to online database
   ├─ UPDATE → Update in online database
   └─ DELETE → Delete from online database
   ↓
5. Mark as synced
   ↓
6. Cleanup old synced changes
```

## Best Practices

1. **Always use SyncService for writes** - Don't write directly to context
2. **Use local context for reads** - Local is always the source of truth
3. **Handle exceptions gracefully** - Local writes should always succeed
4. **Monitor sync status** - Check `SyncService.SyncPendingAsync()` results
5. **Test offline scenarios** - Ensure system works when offline

## Troubleshooting

### Sync Not Working
1. Check if service is running
2. Verify connection strings
3. Check logs for errors
4. Verify online database is accessible

### Connection Issues
- Verify local database is accessible
- Check online database credentials
- Ensure firewall allows connections
- Test with `NetworkService.IsOnlineAsync()`

