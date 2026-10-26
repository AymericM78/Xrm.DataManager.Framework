using System;
using System.Collections.Generic;

namespace Xrm.DataManager.Framework
{
    /// <summary>
    /// Dataverse connections by name (connectionStrings entries) : each connection is opened on first use
    /// </summary>
    public class ProxiesPoolManager : IDisposable
    {
        private readonly Dictionary<string, string> ConnectionStrings;
        private readonly Dictionary<string, ProxiesPool> ProxiesPools = new Dictionary<string, ProxiesPool>(StringComparer.OrdinalIgnoreCase);
        private readonly object LockObject = new object();

        protected ILogger Logger
        {
            get; set;
        }

        protected bool EnableAffinityCookie
        {
            get; set;
        }

        protected bool ApplyConnectionOptimizations
        {
            get; set;
        }

        /// <summary>
        /// Available connection names
        /// </summary>
        public IEnumerable<string> ConnectionNames => ConnectionStrings.Keys;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="connectionStrings">Connection strings by name</param>
        /// <param name="logger"></param>
        /// <param name="enableAffinityCookie">Keep requests on the same Dataverse node</param>
        /// <param name="applyConnectionOptimizations">Apply process wide connection settings (thread pool, .NET Framework connection limit...)</param>
        public ProxiesPoolManager(Dictionary<string, string> connectionStrings, ILogger logger, bool enableAffinityCookie = false, bool applyConnectionOptimizations = true)
        {
            ConnectionStrings = new Dictionary<string, string>(connectionStrings, StringComparer.OrdinalIgnoreCase);
            Logger = logger;
            EnableAffinityCookie = enableAffinityCookie;
            ApplyConnectionOptimizations = applyConnectionOptimizations;
        }

        /// <summary>
        /// Get connection string by name
        /// </summary>
        /// <param name="connectionName">connectionStrings entry name (case insensitive)</param>
        /// <returns></returns>
        public string GetConnectionString(string connectionName)
        {
            if (string.IsNullOrEmpty(connectionName) || !ConnectionStrings.TryGetValue(connectionName, out var connectionString))
            {
                throw new Exception($"Connection string '{connectionName}' is not defined in connectionStrings section! (Available : {string.Join(", ", ConnectionStrings.Keys)})");
            }
            return connectionString;
        }

        /// <summary>
        /// Get connection by name (connected on first call)
        /// </summary>
        /// <param name="connectionName">connectionStrings entry name (case insensitive)</param>
        /// <returns></returns>
        public ProxiesPool GetProxiesPool(string connectionName)
        {
            var connectionString = GetConnectionString(connectionName);
            lock (LockObject)
            {
                if (!ProxiesPools.TryGetValue(connectionName, out var proxiesPool))
                {
                    Logger.LogInformation($"Connecting to '{connectionName}'...");
                    proxiesPool = new ProxiesPool(connectionString, Logger, EnableAffinityCookie, ApplyConnectionOptimizations);
                    ProxiesPools[connectionName] = proxiesPool;
                }
                return proxiesPool;
            }
        }

        /// <summary>
        /// Close all opened connections
        /// </summary>
        public void Dispose()
        {
            lock (LockObject)
            {
                foreach (var proxiesPool in ProxiesPools.Values)
                {
                    proxiesPool.Dispose();
                }
                ProxiesPools.Clear();
            }
        }
    }
}
