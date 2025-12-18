using System.Text.Json;

namespace CloudApp.Services
{
    /// <summary>
    /// Tracks pending changes that need to be synced to the online database
    /// </summary>
    public class PendingChangesTracker
    {
        private readonly string _pendingChangesFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HestiaLink",
            "pending_changes.json"
        );

        private List<PendingChange> _pendingChanges = new();

        public PendingChangesTracker()
        {
            LoadPendingChanges();
        }

        /// <summary>
        /// Adds a pending change to the queue
        /// </summary>
        public void AddPendingChange(string entityType, string operation, object entity, int? localId = null)
        {
            var change = new PendingChange
            {
                Id = Guid.NewGuid(),
                EntityType = entityType,
                Operation = operation, // "Create", "Update", "Delete"
                EntityData = JsonSerializer.Serialize(entity),
                LocalId = localId,
                CreatedAt = DateTime.UtcNow,
                Synced = false
            };

            _pendingChanges.Add(change);
            SavePendingChanges();
        }

        /// <summary>
        /// Gets all pending changes that haven't been synced
        /// </summary>
        public List<PendingChange> GetPendingChanges()
        {
            return _pendingChanges.Where(c => !c.Synced).OrderBy(c => c.CreatedAt).ToList();
        }

        /// <summary>
        /// Marks a change as synced
        /// </summary>
        public void MarkAsSynced(Guid changeId)
        {
            var change = _pendingChanges.FirstOrDefault(c => c.Id == changeId);
            if (change != null)
            {
                change.Synced = true;
                change.SyncedAt = DateTime.UtcNow;
                SavePendingChanges();
            }
        }

        /// <summary>
        /// Removes synced changes older than specified days
        /// </summary>
        public void CleanupSyncedChanges(int daysOld = 7)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
            _pendingChanges.RemoveAll(c => c.Synced && c.SyncedAt.HasValue && c.SyncedAt.Value < cutoffDate);
            SavePendingChanges();
        }

        /// <summary>
        /// Gets count of pending changes
        /// </summary>
        public int GetPendingCount()
        {
            return _pendingChanges.Count(c => !c.Synced);
        }

        private void LoadPendingChanges()
        {
            try
            {
                if (File.Exists(_pendingChangesFile))
                {
                    var json = File.ReadAllText(_pendingChangesFile);
                    _pendingChanges = JsonSerializer.Deserialize<List<PendingChange>>(json) ?? new List<PendingChange>();
                }
            }
            catch
            {
                _pendingChanges = new List<PendingChange>();
            }
        }

        private void SavePendingChanges()
        {
            try
            {
                var directory = Path.GetDirectoryName(_pendingChangesFile);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(_pendingChanges, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_pendingChangesFile, json);
            }
            catch
            {
                // Log error if needed
            }
        }
    }

    /// <summary>
    /// Represents a pending change that needs to be synced
    /// </summary>
    public class PendingChange
    {
        public Guid Id { get; set; }
        public string EntityType { get; set; } = string.Empty; // e.g., "InventoryItem", "Supplier"
        public string Operation { get; set; } = string.Empty; // "Create", "Update", "Delete"
        public string EntityData { get; set; } = string.Empty; // JSON serialized entity
        public int? LocalId { get; set; } // Local database ID
        public DateTime CreatedAt { get; set; }
        public bool Synced { get; set; }
        public DateTime? SyncedAt { get; set; }
    }
}

