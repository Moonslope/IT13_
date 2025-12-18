using System.Net.NetworkInformation;

namespace CloudApp.Services
{
    /// <summary>
    /// Service to check online/offline status and database connectivity
    /// </summary>
    public class ConnectionStatusService
    {
        private readonly string _onlineDatabaseServer = "db35282.databaseasp.net";
        private bool? _lastOnlineStatus = null;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Checks if the system has general internet connectivity
        /// </summary>
        public bool IsInternetAvailable()
        {
            try
            {
                return NetworkInterface.GetIsNetworkAvailable();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the online database is reachable
        /// </summary>
        public async Task<bool> IsOnlineDatabaseAvailableAsync()
        {
            // Use cached result if checked recently
            if (_lastOnlineStatus.HasValue && DateTime.Now - _lastCheckTime < _cacheDuration)
            {
                return _lastOnlineStatus.Value;
            }

            try
            {
                // First check internet connectivity
                if (!IsInternetAvailable())
                {
                    _lastOnlineStatus = false;
                    _lastCheckTime = DateTime.Now;
                    return false;
                }

                // Try to ping the database server
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(_onlineDatabaseServer, 3000);
                
                bool isAvailable = reply.Status == IPStatus.Success;
                _lastOnlineStatus = isAvailable;
                _lastCheckTime = DateTime.Now;
                
                return isAvailable;
            }
            catch
            {
                _lastOnlineStatus = false;
                _lastCheckTime = DateTime.Now;
                return false;
            }
        }

        /// <summary>
        /// Checks if system is online (both internet and database available)
        /// </summary>
        public async Task<bool> IsOnlineAsync()
        {
            return await IsOnlineDatabaseAvailableAsync();
        }

        /// <summary>
        /// Clears the cached status (forces a fresh check)
        /// </summary>
        public void ClearCache()
        {
            _lastOnlineStatus = null;
            _lastCheckTime = DateTime.MinValue;
        }
    }
}

