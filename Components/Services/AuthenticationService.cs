using HestiaLink.Data;
using HestiaLink.Models;
using Microsoft.EntityFrameworkCore;

namespace HestiaLink.Services
{
    public class AuthenticationService
    {
        private readonly HestiaLinkContext _context;

        public AuthenticationService(HestiaLinkContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Validates user credentials against the SystemUser table
        /// </summary>
        public async Task<(bool IsValid, SystemUser? User, string Message)> AuthenticateAsync(string username, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    return (false, null, "Username and password are required");
                }

                // Try to find the user - use raw SQL if EF query fails due to schema mismatch
                SystemUser? user = null;
                
                try
                {
                    // First try with EF Core
                    user = await _context.SystemUsers
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower() && u.Status == "Active");
                }
                catch (Exception efEx)
                {
                    // If EF fails (possibly due to missing IsAvailable column), try raw SQL
                    Console.WriteLine($"EF Query failed: {efEx.Message}. Trying raw SQL...");
                    
                    try
                    {
                        var conn = _context.Database.GetDbConnection();
                        if (conn.State != System.Data.ConnectionState.Open)
                            await conn.OpenAsync();

                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = @"
                            SELECT UserID, EmployeeID, Username, Password, Role, CreatedAt, UpdatedAt, Status
                            FROM SystemUser 
                            WHERE LOWER(Username) = LOWER(@username) AND Status = 'Active'";
                        
                        var param = cmd.CreateParameter();
                        param.ParameterName = "@username";
                        param.Value = username;
                        cmd.Parameters.Add(param);

                        using var reader = await cmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            user = new SystemUser
                            {
                                UserID = reader.GetInt32(reader.GetOrdinal("UserID")),
                                EmployeeID = reader.IsDBNull(reader.GetOrdinal("EmployeeID")) ? null : reader.GetInt32(reader.GetOrdinal("EmployeeID")),
                                Username = reader.GetString(reader.GetOrdinal("Username")),
                                Password = reader.GetString(reader.GetOrdinal("Password")),
                                Role = reader.GetString(reader.GetOrdinal("Role")),
                                CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                                Status = reader.GetString(reader.GetOrdinal("Status")),
                                IsAvailable = true // Default value
                            };
                        }
                    }
                    catch (Exception sqlEx)
                    {
                        Console.WriteLine($"Raw SQL also failed: {sqlEx.Message}");
                        return (false, null, $"Database connection error. Please check your connection.");
                    }
                }

                if (user == null)
                {
                    return (false, null, "Invalid username or password");
                }

                // For production, implement proper password hashing (e.g., bcrypt)
                // For now, comparing plain text (NOT RECOMMENDED FOR PRODUCTION)
                if (user.Password != password)
                {
                    return (false, null, "Invalid username or password");
                }

                return (true, user, "Authentication successful");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Authentication error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return (false, null, $"An error occurred during authentication: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the appropriate landing page based on user role
        /// </summary>
        public string GetLandingPageForRole(string role)
        {
            return role?.ToLower() switch
            {
                "administrator" => "/dashboard",
                "manager" => "/dashboard",
                "frontdesk" => "/reservations/booking-history",
                "housekeeping" => "/housekeeping/dashboard",
                "inventory" => "/inventory/stock-management",
                "hr" => "/hr/employee-management",
                "finance" => "/finance/payment-processing",
                _ => "/dashboard"
            };
        }
    }
}
