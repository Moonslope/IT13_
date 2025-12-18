# Dual-Write System Usage Guide

## Overview

The dual-write system ensures that all database operations are:
1. **Always written to local database first** (primary source of truth)
2. **Then written to online database** (if connection is available)
3. **Queued for sync** (if offline, synced automatically when connection is restored)

## How It Works

### Write Flow

```
User Action (Create/Update/Delete)
    ↓
Write to LOCAL database (always first)
    ↓
Check if ONLINE
    ├─ YES → Write to ONLINE database
    │         ├─ Success → Done
    │         └─ Failure → Queue for sync
    └─ NO → Queue for sync
    ↓
Background sync service periodically checks and syncs queued changes
```

### Read Operations

- **Always read from LOCAL database** (local is source of truth)
- This ensures fast, consistent reads even when offline

## Usage Examples

### Example 1: Using DualWriteService in a Service

```csharp
public class InventoryService
{
    private readonly HestiaLinkContext _context;
    private readonly DualWriteService _dualWrite;

    public InventoryService(
        HestiaLinkContext context,
        DualWriteService dualWrite)
    {
        _context = context;
        _dualWrite = dualWrite;
    }

    public async Task<InventoryItem> CreateItemAsync(InventoryItem item)
    {
        return await _dualWrite.ExecuteDualWriteAsync(async (context) =>
        {
            context.InventoryItems.Add(item);
            await context.SaveChangesAsync();
            return item;
        }, "InventoryItem", item);
    }

    public async Task UpdateItemAsync(InventoryItem item)
    {
        await _dualWrite.ExecuteDualWriteAsync(async (context) =>
        {
            context.InventoryItems.Update(item);
            await context.SaveChangesAsync();
        }, "InventoryItem", item);
    }

    // Read operations use local database
    public async Task<List<InventoryItem>> GetItemsAsync()
    {
        return await _dualWrite.ReadFromLocalAsync(async (context) =>
        {
            return await context.InventoryItems.ToListAsync();
        });
    }
}
```

### Example 2: Using in a Blazor Component

```csharp
@inject DualWriteService DualWrite
@inject IDbContextFactory<HestiaLinkContext> ContextFactory

@code {
    private async Task SaveItem()
    {
        await DualWrite.ExecuteDualWriteAsync(async (context) =>
        {
            context.InventoryItems.Add(CurrentItem);
            await context.SaveChangesAsync();
        }, "InventoryItem", CurrentItem);
    }
}
```

## Services

### ConnectionStatusService
- Checks internet connectivity
- Checks online database reachability
- Caches results for performance

### PendingChangesTracker
- Tracks operations when offline
- Stores pending changes in local file
- Automatically synced when online

### DualWriteService
- Handles dual-write operations
- Always writes to local first
- Queues changes when offline

### SyncService
- Syncs pending changes when online
- Runs automatically every 30 seconds
- Can be triggered manually

## Background Sync

The system automatically:
- Starts 5 seconds after application startup
- Checks for pending changes every 30 seconds
- Only syncs when online database is reachable
- Logs all sync operations

## Pending Changes Storage

Pending changes are stored in:
```
%LocalAppData%\HestiaLink\pending_changes.json
```

## Connection Strings

### Local Database
```
Data Source=MSI\SQLEXPRESS;Initial Catalog=IT13;...
```

### Online Database
```
Server=db35282.databaseasp.net;Database=db35282;User Id=db35282;Password=c@3E=4Akw#6H;Encrypt=False;MultipleActiveResultSets=True;TrustServerCertificate=True;Connection Timeout=30;
```

## Best Practices

1. **Always use DualWriteService for writes** - Don't write directly to context
2. **Use ReadFromLocalAsync for reads** - Ensures consistent data
3. **Handle exceptions gracefully** - Local writes should always succeed
4. **Monitor sync status** - Check `SyncService.GetSyncStatus()` if needed

## Troubleshooting

### Sync Not Working
1. Check if service is running
2. Verify connection strings in `appsettings.json`
3. Check logs for errors
4. Verify online database is accessible

### Connection Issues
- Verify local database is accessible
- Check online database credentials
- Ensure firewall allows connections
- Test with `ConnectionStatusService.IsOnlineAsync()`

