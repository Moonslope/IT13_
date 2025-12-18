using HestiaLink.Data;
using HestiaLink.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace HestiaLink.Services
{
    /// <summary>
    /// Service class for housekeeping operations including task assignment, 
    /// room status management, and staff availability tracking.
    /// </summary>
    public class HousekeepingService
    {
        private readonly IDbContextFactory<HestiaLinkContext> _factory;

        public HousekeepingService(IDbContextFactory<HestiaLinkContext> factory)
        {
            _factory = factory;
        }

        #region Room Operations

        /// <summary>
        /// Gets all rooms that require cleaning (Status = 'For Cleaning' or legacy 'Dirty')
        /// </summary>
        public async Task<List<Room>> GetRoomsForCleaningAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            try
            {
                return await context.Rooms
                    .AsNoTracking()
                    .Include(r => r.RoomType)
                    .Where(r => r.Status == "For Cleaning" || r.Status == "Dirty")
                    .OrderBy(r => r.RoomNumber)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting rooms for cleaning: {ex.Message}");
                // Fallback to raw SQL if EF fails
                return await GetRoomsForCleaningRawAsync(context);
            }
        }

        private async Task<List<Room>> GetRoomsForCleaningRawAsync(HestiaLinkContext context)
        {
            var rooms = new List<Room>();
            try
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                  cmd.CommandText = @"
                      SELECT r.RoomID, r.RoomNumber, r.Floor, r.Status, r.RoomTypeID,
                          rt.TypeName
                      FROM Room r
                      LEFT JOIN RoomType rt ON r.RoomTypeID = rt.RoomTypeID
                      WHERE r.Status IN ('For Cleaning', 'Dirty')
                      ORDER BY r.RoomNumber";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var room = new Room
                    {
                        RoomID = reader.GetInt32(reader.GetOrdinal("RoomID")),
                        RoomNumber = reader.GetString(reader.GetOrdinal("RoomNumber")),
                        Floor = reader.IsDBNull(reader.GetOrdinal("Floor")) ? 1 : Convert.ToInt32(reader["Floor"]),
                        Status = reader.GetString(reader.GetOrdinal("Status")),
                        RoomTypeID = reader.IsDBNull(reader.GetOrdinal("RoomTypeID")) ? null : reader.GetInt32(reader.GetOrdinal("RoomTypeID"))
                    };

                    if (!reader.IsDBNull(reader.GetOrdinal("TypeName")))
                    {
                        room.RoomType = new RoomType { TypeName = reader.GetString(reader.GetOrdinal("TypeName")) };
                    }

                    rooms.Add(room);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetRoomsForCleaningRawAsync: {ex.Message}");
            }
            return rooms;
        }

        /// <summary>
        /// Gets rooms under maintenance
        /// </summary>
        public async Task<List<Room>> GetRoomsUnderMaintenanceAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            try
            {
                return await context.Rooms
                    .AsNoTracking()
                    .Include(r => r.RoomType)
                    .Where(r => r.Status == "Maintenance")
                    .OrderBy(r => r.RoomNumber)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting rooms under maintenance: {ex.Message}");
                return new List<Room>();
            }
        }

        /// <summary>
        /// Updates room status
        /// </summary>
        public async Task<bool> UpdateRoomStatusAsync(int roomId, string newStatus)
        {
            using var context = await _factory.CreateDbContextAsync();
            try
            {
                // First, try to find and detach any tracked instance of this room
                var trackedRoom = context.ChangeTracker.Entries<Room>()
                    .FirstOrDefault(e => e.Entity.RoomID == roomId);
                
                if (trackedRoom != null)
                {
                    context.Entry(trackedRoom.Entity).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                }

                // Use raw SQL to update
                var sql = "UPDATE Room SET Status = {0} WHERE RoomID = {1}";
                var result = await context.Database.ExecuteSqlRawAsync(sql, newStatus, roomId);
                
                return result > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating room status: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Staff Operations

        /// <summary>
        /// Gets all available housekeeping staff
        /// Falls back to getting all housekeeping staff if IsAvailable column doesn't exist
        /// </summary>
        public async Task<List<SystemUser>> GetAvailableHousekeepersAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            try
            {
                // First try raw SQL approach which is more reliable
                return await GetHousekeepingStaffRawAsync(context, availableOnly: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetAvailableHousekeepersAsync: {ex.Message}");
                // Last resort - return all housekeeping staff
                return await GetAllHousekeepingStaffRawAsync(context);
            }
        }

        private async Task<List<SystemUser>> GetHousekeepingStaffRawAsync(HestiaLinkContext context, bool availableOnly = true)
        {
            var users = new List<SystemUser>();
            try
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                
                // First check if IsAvailable column exists
                cmd.CommandText = @"
                    SELECT COLUMN_NAME 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'SystemUser' AND COLUMN_NAME = 'IsAvailable'";
                
                var hasIsAvailable = await cmd.ExecuteScalarAsync() != null;

                // Build query based on column existence and availability filter
                if (hasIsAvailable && availableOnly)
                {
                    cmd.CommandText = @"
                        SELECT UserID, EmployeeID, Username, Role, Status, IsAvailable
                        FROM SystemUser
                        WHERE Role = 'Housekeeping' AND Status = 'Active' AND IsAvailable = 1
                        ORDER BY Username";
                }
                else
                {
                    // Get all housekeeping staff if column doesn't exist or we want all
                    cmd.CommandText = @"
                        SELECT UserID, EmployeeID, Username, Role, Status
                        FROM SystemUser
                        WHERE Role = 'Housekeeping' AND Status = 'Active'
                        ORDER BY Username";
                }

                Console.WriteLine($"Executing query: {cmd.CommandText}");

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var user = new SystemUser
                    {
                        UserID = reader.GetInt32(reader.GetOrdinal("UserID")),
                        EmployeeID = reader.IsDBNull(reader.GetOrdinal("EmployeeID")) ? null : Convert.ToInt32(reader["EmployeeID"]),
                        Username = reader.GetString(reader.GetOrdinal("Username")),
                        Role = reader.GetString(reader.GetOrdinal("Role")),
                        Status = reader.GetString(reader.GetOrdinal("Status")),
                        IsAvailable = true // Default to available
                    };
                    users.Add(user);
                    Console.WriteLine($"Found housekeeping staff: {user.Username} (ID: {user.UserID})");
                }

                Console.WriteLine($"GetHousekeepingStaffRawAsync found {users.Count} staff members (availableOnly: {availableOnly})");

                // If no available staff found but we were filtering, try getting all
                if (users.Count == 0 && availableOnly)
                {
                    Console.WriteLine("No available staff found, getting all housekeeping staff...");
                    return await GetAllHousekeepingStaffRawAsync(context);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetHousekeepingStaffRawAsync: {ex.Message}");
            }
            return users;
        }

        private async Task<List<SystemUser>> GetAllHousekeepingStaffRawAsync(HestiaLinkContext context)
        {
            var users = new List<SystemUser>();
            try
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT UserID, EmployeeID, Username, Role, Status
                    FROM SystemUser
                    WHERE Role = 'Housekeeping' AND Status = 'Active'
                    ORDER BY Username";

                Console.WriteLine($"GetAllHousekeepingStaffRawAsync executing query...");

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var user = new SystemUser
                    {
                        UserID = reader.GetInt32(reader.GetOrdinal("UserID")),
                        EmployeeID = reader.IsDBNull(reader.GetOrdinal("EmployeeID")) ? null : Convert.ToInt32(reader["EmployeeID"]),
                        Username = reader.GetString(reader.GetOrdinal("Username")),
                        Role = reader.GetString(reader.GetOrdinal("Role")),
                        Status = reader.GetString(reader.GetOrdinal("Status")),
                        IsAvailable = true
                    };
                    users.Add(user);
                    Console.WriteLine($"Found staff: {user.Username} (ID: {user.UserID})");
                }

                Console.WriteLine($"GetAllHousekeepingStaffRawAsync found {users.Count} staff members");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetAllHousekeepingStaffRawAsync: {ex.Message}");
            }
            return users;
        }

        /// <summary>
        /// Gets all housekeeping staff regardless of availability
        /// </summary>
        public async Task<List<SystemUser>> GetAllHousekeepingStaffAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            try
            {
                return await GetAllHousekeepingStaffRawAsync(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetAllHousekeepingStaffAsync: {ex.Message}");
                return new List<SystemUser>();
            }
        }
        #endregion

        #region Task Operations

        /// <summary>
        /// Assigns a cleaning task to a housekeeper
        /// </summary>
        public async Task<(bool Success, string Message, CleaningTask? Task)> AssignTaskAsync(int roomId, int userId, string? notes = null)
        {
            using var context = await _factory.CreateDbContextAsync();
            try
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                using var transaction = conn.BeginTransaction();

                try
                {
                    // Validate room
                    using var roomCmd = conn.CreateCommand();
                    roomCmd.Transaction = transaction;
                    roomCmd.CommandText = "SELECT RoomID, RoomNumber, Status FROM Room WHERE RoomID = @RoomID";
                    var roomIdParam = roomCmd.CreateParameter();
                    roomIdParam.ParameterName = "@RoomID";
                    roomIdParam.Value = roomId;
                    roomCmd.Parameters.Add(roomIdParam);

                    string? roomNumber = null;
                    string roomStatus = string.Empty;
                    using (var reader = await roomCmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            transaction.Rollback();
                            return (false, "Room not found", null);
                        }

                        roomStatus = reader.GetString(reader.GetOrdinal("Status"));
                        roomNumber = reader.GetString(reader.GetOrdinal("RoomNumber"));
                    }

                    var isCleaningStatus = roomStatus == "For Cleaning" || roomStatus == "Dirty";
                    if (!isCleaningStatus)
                    {
                        transaction.Rollback();
                        return (false, "Room is not marked for cleaning", null);
                    }

                    if (roomStatus == "Dirty")
                    {
                        using var normalizeCmd = conn.CreateCommand();
                        normalizeCmd.Transaction = transaction;
                        normalizeCmd.CommandText = "UPDATE Room SET Status = 'For Cleaning' WHERE RoomID = @RoomID";
                        var normalizeParam = normalizeCmd.CreateParameter();
                        normalizeParam.ParameterName = "@RoomID";
                        normalizeParam.Value = roomId;
                        normalizeCmd.Parameters.Add(normalizeParam);
                        await normalizeCmd.ExecuteNonQueryAsync();
                    }

                    // Validate user
                    using var userCmd = conn.CreateCommand();
                    userCmd.Transaction = transaction;
                    userCmd.CommandText = "SELECT UserID, Username, Role FROM SystemUser WHERE UserID = @UserID AND Status = 'Active'";
                    var userIdParam = userCmd.CreateParameter();
                    userIdParam.ParameterName = "@UserID";
                    userIdParam.Value = userId;
                    userCmd.Parameters.Add(userIdParam);

                    string? username = null;
                    using (var reader = await userCmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            transaction.Rollback();
                            return (false, "User not found or inactive", null);
                        }
                        var role = reader.GetString(reader.GetOrdinal("Role"));
                        if (role != "Housekeeping")
                        {
                            transaction.Rollback();
                            return (false, "User is not housekeeping staff", null);
                        }
                        username = reader.GetString(reader.GetOrdinal("Username"));
                    }

                    // Check which columns exist in Task table
                    using var checkCmd = conn.CreateCommand();
                    checkCmd.Transaction = transaction;
                    checkCmd.CommandText = @"
                        SELECT COLUMN_NAME 
                        FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_NAME = 'Task'";
                    
                    var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using (var reader = await checkCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            existingColumns.Add(reader.GetString(0));
                        }
                    }

                    // Build insert query based on existing columns
                    var columns = new List<string> { "RoomID", "UserID" };
                    var values = new List<string> { "@RoomID", "@UserID" };

                    if (existingColumns.Contains("AssignedDate"))
                    {
                        columns.Add("AssignedDate");
                        values.Add("GETDATE()");
                    }
                    if (existingColumns.Contains("Status"))
                    {
                        columns.Add("Status");
                        values.Add("'Assigned'");
                    }
                    if (existingColumns.Contains("Notes") && !string.IsNullOrEmpty(notes))
                    {
                        columns.Add("Notes");
                        values.Add("@Notes");
                    }

                    // Insert task
                    using var insertCmd = conn.CreateCommand();
                    insertCmd.Transaction = transaction;
                    insertCmd.CommandText = $@"
                        INSERT INTO Task ({string.Join(", ", columns)})
                        VALUES ({string.Join(", ", values)});
                        SELECT SCOPE_IDENTITY();";
                    
                    var roomParam = insertCmd.CreateParameter();
                    roomParam.ParameterName = "@RoomID";
                    roomParam.Value = roomId;
                    insertCmd.Parameters.Add(roomParam);

                    var userParam = insertCmd.CreateParameter();
                    userParam.ParameterName = "@UserID";
                    userParam.Value = userId;
                    insertCmd.Parameters.Add(userParam);

                    if (existingColumns.Contains("Notes") && !string.IsNullOrEmpty(notes))
                    {
                        var notesParam = insertCmd.CreateParameter();
                        notesParam.ParameterName = "@Notes";
                        notesParam.Value = notes;
                        insertCmd.Parameters.Add(notesParam);
                    }

                    var taskIdResult = await insertCmd.ExecuteScalarAsync();
                    var taskId = Convert.ToInt32(taskIdResult);

                    // Update user availability if column exists
                    using var checkAvailCmd = conn.CreateCommand();
                    checkAvailCmd.Transaction = transaction;
                    checkAvailCmd.CommandText = @"
                        SELECT COLUMN_NAME 
                        FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_NAME = 'SystemUser' AND COLUMN_NAME = 'IsAvailable'";
                    
                    if (await checkAvailCmd.ExecuteScalarAsync() != null)
                    {
                        using var updateUserCmd = conn.CreateCommand();
                        updateUserCmd.Transaction = transaction;
                        updateUserCmd.CommandText = "UPDATE SystemUser SET IsAvailable = 0, UpdatedAt = GETDATE() WHERE UserID = @UserID";
                        var updateUserParam = updateUserCmd.CreateParameter();
                        updateUserParam.ParameterName = "@UserID";
                        updateUserParam.Value = userId;
                        updateUserCmd.Parameters.Add(updateUserParam);
                        await updateUserCmd.ExecuteNonQueryAsync();
                    }

                    transaction.Commit();

                    var task = new CleaningTask
                    {
                        TaskID = taskId,
                        RoomID = roomId,
                        UserID = userId,
                        AssignedDate = DateTime.Now,
                        Status = "Assigned",
                        Notes = notes ?? "Standard Cleaning",
                        Room = new Room { RoomID = roomId, RoomNumber = roomNumber ?? "" },
                        AssignedUser = new SystemUser { UserID = userId, Username = username ?? "" }
                    };

                    return (true, "Task assigned successfully", task);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new Exception($"Transaction failed: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error assigning task: {ex.Message}");
                return (false, $"Error assigning task: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Gets all active tasks (Assigned or In Progress)
        /// </summary>
        public async Task<List<CleaningTask>> GetActiveTasksAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            try
            {
                return await context.CleaningTasks
                    .AsNoTracking()
                    .Include(t => t.Room!)
                        .ThenInclude(r => r!.RoomType)
                    .Include(t => t.AssignedUser)
                    .Where(t => t.Status == "Assigned" || t.Status == "In Progress")
                    .OrderByDescending(t => t.AssignedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EF query failed for GetActiveTasksAsync: {ex.Message}");
                return await GetActiveTasksRawAsync(context);
            }
        }

        private async Task<List<CleaningTask>> GetActiveTasksRawAsync(HestiaLinkContext context)
        {
            var tasks = new List<CleaningTask>();
            try
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                // First check which columns exist
                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = @"
                    SELECT COLUMN_NAME 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'Task'";
                
                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = await checkCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        existingColumns.Add(reader.GetString(0));
                    }
                }

                using var cmd = conn.CreateCommand();
                
                // Build SELECT clause based on existing columns
                var selectColumns = new List<string> { "t.TaskID", "t.RoomID", "t.UserID" };
                if (existingColumns.Contains("AssignedDate")) selectColumns.Add("t.AssignedDate");
                if (existingColumns.Contains("Status")) selectColumns.Add("t.Status");
                if (existingColumns.Contains("Notes")) selectColumns.Add("t.Notes");
                if (existingColumns.Contains("CompletedDate")) selectColumns.Add("t.CompletedDate");
                if (existingColumns.Contains("CompletionStatus")) selectColumns.Add("t.CompletionStatus");

                var whereClause = existingColumns.Contains("Status") 
                    ? "WHERE t.Status IN ('Assigned', 'In Progress')" 
                    : "WHERE 1=1"; // Get all if no status column

                cmd.CommandText = $@"
                    SELECT {string.Join(", ", selectColumns)},
                           r.RoomID, r.RoomNumber, r.Floor, r.Status AS RoomStatus,
                           rt.TypeName,
                           u.UserID AS AssignedUserID, u.Username
                    FROM Task t
                    LEFT JOIN Room r ON t.RoomID = r.RoomID
                    LEFT JOIN RoomType rt ON r.RoomTypeID = rt.RoomTypeID
                    LEFT JOIN SystemUser u ON t.UserID = u.UserID
                    {whereClause}
                    ORDER BY t.TaskID DESC";

                using var reader2 = await cmd.ExecuteReaderAsync();
                while (await reader2.ReadAsync())
                {
                    var task = new CleaningTask
                    {
                        TaskID = reader2.GetInt32(reader2.GetOrdinal("TaskID")),
                        RoomID = reader2.GetInt32(reader2.GetOrdinal("RoomID")),
                        UserID = reader2.IsDBNull(reader2.GetOrdinal("UserID")) ? null : reader2.GetInt32(reader2.GetOrdinal("UserID"))
                    };

                    // Set optional properties if columns exist
                    if (existingColumns.Contains("AssignedDate") && !reader2.IsDBNull(reader2.GetOrdinal("AssignedDate")))
                        task.AssignedDate = reader2.GetDateTime(reader2.GetOrdinal("AssignedDate"));
                    else
                        task.AssignedDate = DateTime.Now;

                    if (existingColumns.Contains("Status") && !reader2.IsDBNull(reader2.GetOrdinal("Status")))
                        task.Status = reader2.GetString(reader2.GetOrdinal("Status"));
                    else
                        task.Status = "Assigned";

                    if (existingColumns.Contains("Notes") && !reader2.IsDBNull(reader2.GetOrdinal("Notes")))
                        task.Notes = reader2.GetString(reader2.GetOrdinal("Notes"));

                    // Set Room
                    if (!reader2.IsDBNull(reader2.GetOrdinal("RoomNumber")))
                    {
                        task.Room = new Room
                        {
                            RoomID = reader2.GetInt32(reader2.GetOrdinal("RoomID")),
                            RoomNumber = reader2.GetString(reader2.GetOrdinal("RoomNumber")),
                            Floor = reader2.IsDBNull(reader2.GetOrdinal("Floor")) ? 1 : Convert.ToInt32(reader2["Floor"]),
                            Status = reader2.IsDBNull(reader2.GetOrdinal("RoomStatus")) ? "" : reader2.GetString(reader2.GetOrdinal("RoomStatus"))
                        };

                        if (!reader2.IsDBNull(reader2.GetOrdinal("TypeName")))
                        {
                            task.Room.RoomType = new RoomType { TypeName = reader2.GetString(reader2.GetOrdinal("TypeName")) };
                        }
                    }

                    // Set User
                    if (!reader2.IsDBNull(reader2.GetOrdinal("AssignedUserID")))
                    {
                        task.AssignedUser = new SystemUser
                        {
                            UserID = reader2.GetInt32(reader2.GetOrdinal("AssignedUserID")),
                            Username = reader2.IsDBNull(reader2.GetOrdinal("Username")) ? "" : reader2.GetString(reader2.GetOrdinal("Username"))
                        };
                    }

                    tasks.Add(task);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetActiveTasksRawAsync: {ex.Message}");
            }
            return tasks;
        }

        /// <summary>
        /// Gets tasks for a specific user
        /// </summary>
        public async Task<List<CleaningTask>> GetUserTasksAsync(int userId, bool activeOnly = true)
        {
            using var context = await _factory.CreateDbContextAsync();
            try
            {
                var query = context.CleaningTasks
                    .AsNoTracking()
                    .Include(t => t.Room!)
                        .ThenInclude(r => r!.RoomType)
                    .Where(t => t.UserID == userId);

                if (activeOnly)
                {
                    query = query.Where(t => t.Status == "Assigned" || t.Status == "In Progress");
                }

                return await query
                    .OrderByDescending(t => t.AssignedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EF query failed for GetUserTasksAsync: {ex.Message}");
                // Fallback to raw SQL
                return await GetUserTasksRawAsync(context, userId, activeOnly);
            }
        }

        /// <summary>
        /// Fallback method to get user tasks using raw SQL
        /// </summary>
        private async Task<List<CleaningTask>> GetUserTasksRawAsync(HestiaLinkContext context, int userId, bool activeOnly)
        {
            var tasks = new List<CleaningTask>();
            try
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                // Check which columns exist in Task table
                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = @"
                    SELECT COLUMN_NAME 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'Task'";
                
                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = await checkCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        existingColumns.Add(reader.GetString(0));
                    }
                }

                // Build SELECT clause based on existing columns
                var selectColumns = new List<string> { "t.TaskID", "t.RoomID", "t.UserID" };
                if (existingColumns.Contains("AssignedDate")) selectColumns.Add("t.AssignedDate");
                if (existingColumns.Contains("Status")) selectColumns.Add("t.Status");
                if (existingColumns.Contains("Notes")) selectColumns.Add("t.Notes");
                if (existingColumns.Contains("CompletedDate")) selectColumns.Add("t.CompletedDate");
                if (existingColumns.Contains("CompletionStatus")) selectColumns.Add("t.CompletionStatus");

                // Build WHERE clause
                var whereClause = "WHERE t.UserID = @UserID";
                if (activeOnly && existingColumns.Contains("Status"))
                {
                    whereClause += " AND t.Status IN ('Assigned', 'In Progress')";
                }

                using var cmd = conn.CreateCommand();
                cmd.CommandText = $@"
                    SELECT {string.Join(", ", selectColumns)},
                           r.RoomID AS Room_RoomID, r.RoomNumber, r.Floor, r.Status AS RoomStatus,
                           rt.TypeName
                    FROM Task t
                    LEFT JOIN Room r ON t.RoomID = r.RoomID
                    LEFT JOIN RoomType rt ON r.RoomTypeID = rt.RoomTypeID
                    {whereClause}
                    ORDER BY t.TaskID DESC";

                var userIdParam = cmd.CreateParameter();
                userIdParam.ParameterName = "@UserID";
                userIdParam.Value = userId;
                cmd.Parameters.Add(userIdParam);

                using var dataReader = await cmd.ExecuteReaderAsync();
                while (await dataReader.ReadAsync())
                {
                    var task = new CleaningTask
                    {
                        TaskID = dataReader.GetInt32(dataReader.GetOrdinal("TaskID")),
                        RoomID = dataReader.GetInt32(dataReader.GetOrdinal("RoomID")),
                        UserID = dataReader.IsDBNull(dataReader.GetOrdinal("UserID")) ? null : dataReader.GetInt32(dataReader.GetOrdinal("UserID"))
                    };

                    // Set optional properties if columns exist
                    if (existingColumns.Contains("AssignedDate") && !dataReader.IsDBNull(dataReader.GetOrdinal("AssignedDate")))
                        task.AssignedDate = dataReader.GetDateTime(dataReader.GetOrdinal("AssignedDate"));
                    else
                        task.AssignedDate = DateTime.Now;

                    if (existingColumns.Contains("Status") && !dataReader.IsDBNull(dataReader.GetOrdinal("Status")))
                        task.Status = dataReader.GetString(dataReader.GetOrdinal("Status"));
                    else
                        task.Status = "Assigned";

                    if (existingColumns.Contains("Notes") && !dataReader.IsDBNull(dataReader.GetOrdinal("Notes")))
                        task.Notes = dataReader.GetString(dataReader.GetOrdinal("Notes"));

                    if (existingColumns.Contains("CompletedDate") && !dataReader.IsDBNull(dataReader.GetOrdinal("CompletedDate")))
                        task.CompletedDate = dataReader.GetDateTime(dataReader.GetOrdinal("CompletedDate"));

                    if (existingColumns.Contains("CompletionStatus") && !dataReader.IsDBNull(dataReader.GetOrdinal("CompletionStatus")))
                        task.CompletionStatus = dataReader.GetString(dataReader.GetOrdinal("CompletionStatus"));

                    // Set Room
                    if (!dataReader.IsDBNull(dataReader.GetOrdinal("RoomNumber")))
                    {
                        task.Room = new Room
                        {
                            RoomID = dataReader.GetInt32(dataReader.GetOrdinal("Room_RoomID")),
                            RoomNumber = dataReader.GetString(dataReader.GetOrdinal("RoomNumber")),
                            Floor = dataReader.IsDBNull(dataReader.GetOrdinal("Floor")) ? 1 : Convert.ToInt32(dataReader["Floor"]),
                            Status = dataReader.IsDBNull(dataReader.GetOrdinal("RoomStatus")) ? "" : dataReader.GetString(dataReader.GetOrdinal("RoomStatus"))
                        };

                        if (!dataReader.IsDBNull(dataReader.GetOrdinal("TypeName")))
                        {
                            task.Room.RoomType = new RoomType { TypeName = dataReader.GetString(dataReader.GetOrdinal("TypeName")) };
                        }
                    }

                    tasks.Add(task);
                }

                Console.WriteLine($"GetUserTasksRawAsync found {tasks.Count} tasks for user {userId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetUserTasksRawAsync: {ex.Message}");
            }
            return tasks;
        }

        /// <summary>
        /// Gets completed tasks for a user (task history)
        /// </summary>
        public async Task<List<CleaningTask>> GetUserTaskHistoryAsync(int userId)
        {
            using var context = await _factory.CreateDbContextAsync();
            try
            {
                return await context.CleaningTasks
                    .AsNoTracking()
                    .Include(t => t.Room!)
                        .ThenInclude(r => r!.RoomType)
                    .Where(t => t.UserID == userId && t.Status == "Completed")
                    .OrderByDescending(t => t.CompletedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EF query failed for GetUserTaskHistoryAsync: {ex.Message}");
                // Fallback to raw SQL
                return await GetUserTaskHistoryRawAsync(context, userId);
            }
        }

        /// <summary>
        /// Fallback method to get user task history using raw SQL
        /// </summary>
        private async Task<List<CleaningTask>> GetUserTaskHistoryRawAsync(HestiaLinkContext context, int userId)
        {
            var tasks = new List<CleaningTask>();
            try
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                // Check which columns exist in Task table
                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = @"
                    SELECT COLUMN_NAME 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'Task'";
                
                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = await checkCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        existingColumns.Add(reader.GetString(0));
                    }
                }

                // If Status column doesn't exist, return empty (no way to filter completed tasks)
                if (!existingColumns.Contains("Status"))
                {
                    return tasks;
                }

                // Build SELECT clause based on existing columns
                var selectColumns = new List<string> { "t.TaskID", "t.RoomID", "t.UserID", "t.Status" };
                if (existingColumns.Contains("AssignedDate")) selectColumns.Add("t.AssignedDate");
                if (existingColumns.Contains("Notes")) selectColumns.Add("t.Notes");
                if (existingColumns.Contains("CompletedDate")) selectColumns.Add("t.CompletedDate");
                if (existingColumns.Contains("CompletionStatus")) selectColumns.Add("t.CompletionStatus");

                var orderBy = existingColumns.Contains("CompletedDate") ? "t.CompletedDate DESC" : "t.TaskID DESC";

                using var cmd = conn.CreateCommand();
                cmd.CommandText = $@"
                    SELECT {string.Join(", ", selectColumns)},
                           r.RoomID AS Room_RoomID, r.RoomNumber, r.Floor, r.Status AS RoomStatus,
                           rt.TypeName
                    FROM Task t
                    LEFT JOIN Room r ON t.RoomID = r.RoomID
                    LEFT JOIN RoomType rt ON r.RoomTypeID = rt.RoomTypeID
                    WHERE t.UserID = @UserID AND t.Status = 'Completed'
                    ORDER BY {orderBy}";

                var userIdParam = cmd.CreateParameter();
                userIdParam.ParameterName = "@UserID";
                userIdParam.Value = userId;
                cmd.Parameters.Add(userIdParam);

                using var dataReader = await cmd.ExecuteReaderAsync();
                while (await dataReader.ReadAsync())
                {
                    var task = new CleaningTask
                    {
                        TaskID = dataReader.GetInt32(dataReader.GetOrdinal("TaskID")),
                        RoomID = dataReader.GetInt32(dataReader.GetOrdinal("RoomID")),
                        UserID = dataReader.IsDBNull(dataReader.GetOrdinal("UserID")) ? null : dataReader.GetInt32(dataReader.GetOrdinal("UserID")),
                        Status = dataReader.GetString(dataReader.GetOrdinal("Status"))
                    };

                    // Set optional properties if columns exist
                    if (existingColumns.Contains("AssignedDate") && !dataReader.IsDBNull(dataReader.GetOrdinal("AssignedDate")))
                        task.AssignedDate = dataReader.GetDateTime(dataReader.GetOrdinal("AssignedDate"));

                    if (existingColumns.Contains("Notes") && !dataReader.IsDBNull(dataReader.GetOrdinal("Notes")))
                        task.Notes = dataReader.GetString(dataReader.GetOrdinal("Notes"));

                    if (existingColumns.Contains("CompletedDate") && !dataReader.IsDBNull(dataReader.GetOrdinal("CompletedDate")))
                        task.CompletedDate = dataReader.GetDateTime(dataReader.GetOrdinal("CompletedDate"));

                    if (existingColumns.Contains("CompletionStatus") && !dataReader.IsDBNull(dataReader.GetOrdinal("CompletionStatus")))
                        task.CompletionStatus = dataReader.GetString(dataReader.GetOrdinal("CompletionStatus"));

                    // Set Room
                    if (!dataReader.IsDBNull(dataReader.GetOrdinal("RoomNumber")))
                    {
                        task.Room = new Room
                        {
                            RoomID = dataReader.GetInt32(dataReader.GetOrdinal("Room_RoomID")),
                            RoomNumber = dataReader.GetString(dataReader.GetOrdinal("RoomNumber")),
                            Floor = dataReader.IsDBNull(dataReader.GetOrdinal("Floor")) ? 1 : Convert.ToInt32(dataReader["Floor"]),
                            Status = dataReader.IsDBNull(dataReader.GetOrdinal("RoomStatus")) ? "" : dataReader.GetString(dataReader.GetOrdinal("RoomStatus"))
                        };

                        if (!dataReader.IsDBNull(dataReader.GetOrdinal("TypeName")))
                        {
                            task.Room.RoomType = new RoomType { TypeName = dataReader.GetString(dataReader.GetOrdinal("TypeName")) };
                        }
                    }

                    tasks.Add(task);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetUserTaskHistoryRawAsync: {ex.Message}");
            }
            return tasks;
        }

        /// <summary>
        /// Starts a task (changes status from Assigned to In Progress)
        /// </summary>
        public async Task<(bool Success, string Message)> StartTaskAsync(int taskId, int userId)
        {
            using var context = await _factory.CreateDbContextAsync();
            try
            {
                var sql = "UPDATE Task SET Status = 'In Progress' WHERE TaskID = {0} AND UserID = {1}";
                var result = await context.Database.ExecuteSqlRawAsync(sql, taskId, userId);
                return result > 0 ? (true, "Task started successfully") : (false, "Task not found or not assigned to you");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting task: {ex.Message}");
                return (false, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Completes a task with the specified completion type
        /// </summary>
        public async Task<(bool Success, string Message)> CompleteTaskAsync(int taskId, int userId, string completionType)
        {
            using var context = await _factory.CreateDbContextAsync();
            try
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                using var transaction = conn.BeginTransaction();

                try
                {
                    // Get task info
                    using var getCmd = conn.CreateCommand();
                    getCmd.Transaction = transaction;
                    getCmd.CommandText = "SELECT RoomID, UserID FROM Task WHERE TaskID = @TaskID";
                    var taskIdParam = getCmd.CreateParameter();
                    taskIdParam.ParameterName = "@TaskID";
                    taskIdParam.Value = taskId;
                    getCmd.Parameters.Add(taskIdParam);

                    int roomId = 0;
                    int taskUserId = 0;
                    using (var reader = await getCmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            transaction.Rollback();
                            return (false, "Task not found");
                        }
                        roomId = reader.GetInt32(0);
                        taskUserId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    }

                    // Check which columns exist
                    using var checkCmd = conn.CreateCommand();
                    checkCmd.Transaction = transaction;
                    checkCmd.CommandText = @"
                        SELECT COLUMN_NAME 
                        FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_NAME = 'Task'";
                    
                    var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using (var reader = await checkCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            existingColumns.Add(reader.GetString(0));
                        }
                    }

                    // Build UPDATE query based on existing columns
                    var setClauses = new List<string>();
                    if (existingColumns.Contains("Status")) setClauses.Add("Status = 'Completed'");
                    if (existingColumns.Contains("CompletedDate")) setClauses.Add("CompletedDate = GETDATE()");
                    if (existingColumns.Contains("CompletionStatus")) setClauses.Add("CompletionStatus = @CompletionStatus");

                    if (setClauses.Any())
                    {
                        using var updateCmd = conn.CreateCommand();
                        updateCmd.Transaction = transaction;
                        updateCmd.CommandText = $"UPDATE Task SET {string.Join(", ", setClauses)} WHERE TaskID = @TaskID";
                        
                        var taskParam = updateCmd.CreateParameter();
                        taskParam.ParameterName = "@TaskID";
                        taskParam.Value = taskId;
                        updateCmd.Parameters.Add(taskParam);

                        if (existingColumns.Contains("CompletionStatus"))
                        {
                            var compParam = updateCmd.CreateParameter();
                            compParam.ParameterName = "@CompletionStatus";
                            compParam.Value = completionType;
                            updateCmd.Parameters.Add(compParam);
                        }

                        await updateCmd.ExecuteNonQueryAsync();
                    }

                    // Update room status
                    string newRoomStatus = completionType == "Cleaned" ? "Available" : "Maintenance";
                    using var roomCmd = conn.CreateCommand();
                    roomCmd.Transaction = transaction;
                    roomCmd.CommandText = "UPDATE Room SET Status = @Status WHERE RoomID = @RoomID";
                    var statusParam = roomCmd.CreateParameter();
                    statusParam.ParameterName = "@Status";
                    statusParam.Value = newRoomStatus;
                    roomCmd.Parameters.Add(statusParam);
                    var roomIdParam = roomCmd.CreateParameter();
                    roomIdParam.ParameterName = "@RoomID";
                    roomIdParam.Value = roomId;
                    roomCmd.Parameters.Add(roomIdParam);
                    await roomCmd.ExecuteNonQueryAsync();

                    // Update user availability if column exists
                    using var checkAvailCmd = conn.CreateCommand();
                    checkAvailCmd.Transaction = transaction;
                    checkAvailCmd.CommandText = @"
                        SELECT COLUMN_NAME 
                        FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_NAME = 'SystemUser' AND COLUMN_NAME = 'IsAvailable'";
                    
                    if (await checkAvailCmd.ExecuteScalarAsync() != null && taskUserId > 0)
                    {
                        using var updateUserCmd = conn.CreateCommand();
                        updateUserCmd.Transaction = transaction;
                        updateUserCmd.CommandText = "UPDATE SystemUser SET IsAvailable = 1, UpdatedAt = GETDATE() WHERE UserID = @UserID";
                        var userIdParam = updateUserCmd.CreateParameter();
                        userIdParam.ParameterName = "@UserID";
                        userIdParam.Value = taskUserId;
                        updateUserCmd.Parameters.Add(userIdParam);
                        await updateUserCmd.ExecuteNonQueryAsync();
                    }

                    transaction.Commit();
                    return (true, $"Task completed. Room status: {newRoomStatus}");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new Exception($"Transaction failed: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error completing task: {ex.Message}");
                return (false, $"Error completing task: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets task statistics for dashboard display
        /// </summary>
        public async Task<TaskStatistics> GetTaskStatisticsAsync()
        {
            var stats = new TaskStatistics();
            using var context = await _factory.CreateDbContextAsync();
            
            try
            {
                // Use raw SQL to avoid column issues
                var conn = context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                // Check which columns exist
                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = @"
                    SELECT COLUMN_NAME 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'Task'";
                
                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = await checkCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        existingColumns.Add(reader.GetString(0));
                    }
                }

                // Get task counts if Status column exists
                if (existingColumns.Contains("Status"))
                {
                    using var countCmd = conn.CreateCommand();
                    countCmd.CommandText = @"
                        SELECT 
                            SUM(CASE WHEN Status = 'Assigned' THEN 1 ELSE 0 END) AS Pending,
                            SUM(CASE WHEN Status = 'In Progress' THEN 1 ELSE 0 END) AS InProgress
                        FROM Task";
                    
                    using var countReader = await countCmd.ExecuteReaderAsync();
                    if (await countReader.ReadAsync())
                    {
                        stats.TotalPendingTasks = countReader.IsDBNull(0) ? 0 : countReader.GetInt32(0);
                        stats.TotalInProgressTasks = countReader.IsDBNull(1) ? 0 : countReader.GetInt32(1);
                    }
                }

                // Get room counts
                using var roomCmd = conn.CreateCommand();
                roomCmd.CommandText = @"
                    SELECT 
                        SUM(CASE WHEN Status IN ('For Cleaning', 'Dirty') THEN 1 ELSE 0 END) AS ForCleaning,
                        SUM(CASE WHEN Status = 'Maintenance' THEN 1 ELSE 0 END) AS Maintenance
                    FROM Room";
                
                using var roomReader = await roomCmd.ExecuteReaderAsync();
                if (await roomReader.ReadAsync())
                {
                    stats.RoomsForCleaning = roomReader.IsDBNull(0) ? 0 : roomReader.GetInt32(0);
                    stats.RoomsUnderMaintenance = roomReader.IsDBNull(1) ? 0 : roomReader.GetInt32(1);
                }

                // Get available housekeepers count
                using var userCheckCmd = conn.CreateCommand();
                userCheckCmd.CommandText = @"
                    SELECT COLUMN_NAME 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'SystemUser' AND COLUMN_NAME = 'IsAvailable'";
                
                var hasIsAvailable = await userCheckCmd.ExecuteScalarAsync() != null;

                using var userCmd = conn.CreateCommand();
                if (hasIsAvailable)
                {
                    userCmd.CommandText = @"
                        SELECT COUNT(*) FROM SystemUser 
                        WHERE Role = 'Housekeeping' AND Status = 'Active' AND IsAvailable = 1";
                }
                else
                {
                    userCmd.CommandText = @"
                        SELECT COUNT(*) FROM SystemUser 
                        WHERE Role = 'Housekeeping' AND Status = 'Active'";
                }
                
                var userCountResult = await userCmd.ExecuteScalarAsync();
                stats.AvailableHousekeepers = userCountResult != null ? Convert.ToInt32(userCountResult) : 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting task statistics: {ex.Message}");
            }

            return stats;
        }

        #endregion

        #region Maintenance Operations

        /// <summary>
        /// Gets all active maintenance requests
        /// </summary>
        public async Task<List<MaintenanceRequest>> GetActiveMaintenanceRequestsAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            try
            {
                return await context.MaintenanceRequests
                    .AsNoTracking()
                    .Include(m => m.Room)
                    .Include(m => m.ReportedByUser)
                    .Where(m => m.Status == "Pending" || m.Status == "In Progress")
                    .OrderByDescending(m => m.ReportedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting maintenance requests: {ex.Message}");
                return new List<MaintenanceRequest>();
            }
        }

        /// <summary>
        /// Reports a maintenance issue from a cleaning task
        /// </summary>
        public async Task<(bool Success, string Message)> ReportMaintenanceIssueAsync(int taskId, int userId, string description)
        {
            using var context = await _factory.CreateDbContextAsync();
            try
            {
                // 1. Get Task details first
                var task = await context.CleaningTasks.FindAsync(taskId);
                if (task == null) return (false, "Task not found");
                int roomId = task.RoomID;

                // 2. Complete the task
                var (success, message) = await CompleteTaskAsync(taskId, userId, "Maintenance Required");
                if (!success) return (false, message);

                // 3. Create Maintenance Request
                var request = new MaintenanceRequest
                {
                    RoomID = roomId,
                    ReportedByUserID = userId,
                    Description = description,
                    Status = "Pending",
                    ReportedDate = DateTime.Now
                };

                context.MaintenanceRequests.Add(request);
                await context.SaveChangesAsync();

                return (true, "Issue reported successfully. Room is moved to maintenance.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reporting issue: {ex.Message}");
                return (false, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Resolves a maintenance issue
        /// </summary>
        public async Task<(bool Success, string Message)> ResolveMaintenanceIssueAsync(int roomId, string resolutionNotes)
        {
            using var context = await _factory.CreateDbContextAsync();
            try
            {
                // 1. Find the pending request for this room
                var request = await context.MaintenanceRequests
                    .Where(m => m.RoomID == roomId && (m.Status == "Pending" || m.Status == "In Progress"))
                    .OrderByDescending(m => m.ReportedDate)
                    .FirstOrDefaultAsync();

                if (request != null)
                {
                    request.Status = "Resolved";
                    request.ResolvedDate = DateTime.Now;
                    request.ResolutionNotes = resolutionNotes;
                }

                // 2. Update Room Status to Available
                var roomSuccess = await UpdateRoomStatusAsync(roomId, "Available");

                if (roomSuccess)
                {
                    await context.SaveChangesAsync();
                    return (true, "Maintenance issue resolved.");
                }

                return (false, "Failed to update room status.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error resolving issue: {ex.Message}");
                return (false, $"Error: {ex.Message}");
            }
        }

        #endregion

        #region Helper Classes

        public class TaskStatistics
        {
            public int TotalPendingTasks { get; set; }
            public int TotalInProgressTasks { get; set; }
            public int TotalCompletedToday { get; set; }
            public int RoomsForCleaning { get; set; }
            public int RoomsUnderMaintenance { get; set; }
            public int AvailableHousekeepers { get; set; }
        }

        #endregion
    }
}
