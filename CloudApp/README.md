# CloudApp - Dual-Write Service

## Overview

CloudApp is a separate web API service that handles dual-write operations between your local database and the online CloudApp database. It runs independently from your main MAUI application.

## Features

- ✅ **Dual-Write Pattern**: Writes to local database first, then online database
- ✅ **Offline Support**: Queues changes when offline, syncs when back online
- ✅ **Background Sync**: Automatically syncs pending changes every 30 seconds
- ✅ **REST API**: Provides endpoints for manual sync and status checks

## Configuration

### Connection Strings

Configured in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "LocalDatabase": "Data Source=MSI\\SQLEXPRESS;Initial Catalog=IT13;...",
    "OnlineDatabase": "Server=db35282.databaseasp.net;Database=db35282;..."
  }
}
```

### Services

- **ConnectionStatusService**: Checks online/offline status
- **PendingChangesTracker**: Tracks operations when offline
- **DualWriteService**: Handles dual-write operations
- **SyncService**: Syncs pending changes when online

## API Endpoints

### POST /api/sync/sync
Manually trigger sync of pending changes

**Response:**
```json
{
  "success": true,
  "message": "Synced 5 of 5 changes",
  "totalPending": 5,
  "syncedCount": 5,
  "failedCount": 0,
  "errors": []
}
```

### GET /api/sync/status
Get current sync status

**Response:**
```json
{
  "pendingCount": 3,
  "isSyncing": false,
  "isOnline": true
}
```

### GET /api/sync/pending
Get pending changes count

**Response:**
```json
{
  "pendingCount": 3
}
```

## Running CloudApp

1. **Build the project:**
   ```bash
   dotnet build CloudApp/CloudApp.csproj
   ```

2. **Run the service:**
   ```bash
   dotnet run --project CloudApp/CloudApp.csproj
   ```

3. **Access Swagger UI:**
   - Development: `https://localhost:5001/swagger`
   - Or check `Properties/launchSettings.json` for the configured port

## Background Sync

The service automatically:
- Starts 5 seconds after application startup
- Checks for pending changes every 30 seconds
- Only syncs when online database is reachable
- Logs all sync operations

## Integration with Main Application

Your main MAUI application can:
1. Call CloudApp API endpoints to trigger syncs
2. Check sync status
3. Monitor pending changes

## Pending Changes Storage

Pending changes are stored in:
```
%LocalAppData%\HestiaLink\pending_changes.json
```

This file is shared between CloudApp and your main application (if configured).

## Troubleshooting

### Sync Not Working

1. Check if service is running
2. Verify connection strings in `appsettings.json`
3. Check logs for errors
4. Use `/api/sync/status` endpoint to check status

### Connection Issues

- Verify local database is accessible
- Check online database credentials
- Ensure firewall allows connections
- Test with `/api/sync/status` endpoint

## Next Steps

1. Run CloudApp as a Windows Service or background process
2. Configure it to start automatically
3. Set up monitoring/alerting for sync failures
4. Integrate with your main application via HTTP calls

