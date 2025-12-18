using System.Net.NetworkInformation;

namespace HestiaLink.Services
{
    /// <summary>
    /// Service to check internet and database connectivity
    /// </summary>
    public class NetworkService
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
        /// Checks if the online database server is reachable
        /// </summary>
        public async Task<bool> IsDatabaseReachableAsync()
        {
            try
            {
                // First check internet connectivity
                if (!IsInternetAvailable())
                {
                    return false;
                }

                // Try to ping the database server
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(_onlineDatabaseServer, 3000);
                
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if system is online (both internet and database available)
        /// </summary>
        public async Task<bool> IsOnlineAsync()
        {
            // Use cached result if checked recently
            if (_lastOnlineStatus.HasValue && DateTime.Now - _lastCheckTime < _cacheDuration)
            {
                return _lastOnlineStatus.Value;
            }

            var isOnline = await IsDatabaseReachableAsync();
            _lastOnlineStatus = isOnline;
            _lastCheckTime = DateTime.Now;
            
            return isOnline;
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

