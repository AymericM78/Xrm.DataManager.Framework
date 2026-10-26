using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Threading;

namespace Xrm.DataManager.Framework
{
    public class ProxiesPool : IDisposable
    {
        // Dataverse connection string aliases for organization url
        private static readonly string[] UrlParameterNames = { "ServiceUri", "Service Uri", "Url", "Server" };

        protected string ConnectionString
        {
            get; set;
        }
        protected ILogger Logger
        {
            get; set;
        }
        public Uri InstanceUri
        {
            get; set;
        }

        protected bool EnableAffinityCookie
        {
            get; set;
        }

        public IManagedTokenOrganizationServiceProxy MainProxy
        {
            get;
            private set;
        }

        /// <summary>
        /// Proxies released by threads and ready to be reused
        /// </summary>
        private readonly ConcurrentBag<IManagedTokenOrganizationServiceProxy> AvailableProxies = new ConcurrentBag<IManagedTokenOrganizationServiceProxy>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="logger"></param>
        /// <param name="enableAffinityCookie">Keep requests on the same Dataverse node</param>
        /// <param name="applyConnectionOptimizations">Apply process wide connection settings (thread pool, .NET Framework connection limit...)</param>
        public ProxiesPool(string connectionString, ILogger logger, bool enableAffinityCookie = false, bool applyConnectionOptimizations = true)
        {
            ConnectionString = connectionString;
            Logger = logger;
            EnableAffinityCookie = enableAffinityCookie;
            InstanceUri = new Uri(ExtractUrlFromConnectionString(connectionString));

            // Must be applied before first connection to be effective
            if (applyConnectionOptimizations)
            {
                ApplyConnectionOptimizations();
            }
            InitializeMainProxy();
        }

        internal static string ExtractUrlFromConnectionString(string connectionString)
        {
            var maskedConnectionString = Utilities.MaskConnectionStringSecrets(connectionString);
            var builder = new DbConnectionStringBuilder();
            try
            {
                builder.ConnectionString = connectionString;
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Crm connectionstring is invalid : {ex.Message} '{maskedConnectionString}'");
            }

            foreach (var parameterName in UrlParameterNames)
            {
                if (builder.TryGetValue(parameterName, out var url) && !string.IsNullOrWhiteSpace(url?.ToString()))
                {
                    return url.ToString().Trim();
                }
            }
            throw new ArgumentException($"Crm connectionstring is invalid : url parameter is not provided! '{maskedConnectionString}'");
        }

        private void InitializeMainProxy()
        {
            try
            {
                // Proxy throws if connection fails and resolves CallerId (WhoAmI) when Guid.Empty is provided
                MainProxy = new ManagedTokenOrganizationServiceProxy(this.ConnectionString, this.Logger, Guid.Empty, enableAffinityCookie: this.EnableAffinityCookie);
            }
            catch (Exception ex)
            {
                Logger.LogInformation($"Failed to connect to Dataverse => {ex.Message}!");
                throw;
            }
        }

        /// <summary>
        /// Get a proxy from the pool or clone a new one from main proxy if none is available
        /// Call ReleaseProxy when work is done to allow its reuse
        /// </summary>
        /// <returns></returns>
        public IManagedTokenOrganizationServiceProxy GetProxy(int retryCount = 0)
        {
            while (AvailableProxies.TryTake(out var availableProxy))
            {
                if (availableProxy.ServiceClient?.IsReady == true)
                {
                    return availableProxy;
                }
                availableProxy.Dispose();
            }

            var proxy = new ManagedTokenOrganizationServiceProxy(this.ConnectionString, this.Logger, MainProxy.CallerId, this.MainProxy.ServiceClient, this.EnableAffinityCookie);
            return proxy;
        }

        /// <summary>
        /// Give back proxy to the pool for reuse
        /// </summary>
        /// <param name="proxy"></param>
        public void ReleaseProxy(IManagedTokenOrganizationServiceProxy proxy)
        {
            if (proxy != null)
            {
                AvailableProxies.Add(proxy);
            }
        }

        /// <summary>
        /// Close all pooled connections and main connection
        /// </summary>
        public void Dispose()
        {
            while (AvailableProxies.TryTake(out var proxy))
            {
                proxy.Dispose();
            }
            MainProxy?.Dispose();
        }

        /// <summary>
        /// Optimize connection performances
        /// </summary>
        protected void ApplyConnectionOptimizations()
        {
            // TLS version is not forced anymore : .NET Framework 4.7+ uses OS defaults (TLS 1.2 / 1.3)

            // Get the current settings for the Thread Pool
            ThreadPool.GetMinThreads(out int minWorker, out int minIOC);

            // The number of worker threads to make simultaneous requests (do not go over 64)
            const int numberRequests = 64;
            // without setting the thread pool up, there was enough of a delay to cause timeouts!
            ThreadPool.SetMinThreads(numberRequests, minIOC);

#if NETFRAMEWORK
            // ServicePointManager only applies to .NET Framework (HttpWebRequest), .NET uses HttpClient defaults

            // Change max connections from .NET to a remote service default: 2
            // MS Support max recommended value = 12 * logical processors
            ServicePointManager.DefaultConnectionLimit = 12 * minWorker;

            // Turn off the Expect 100 to continue message - 'true' will cause the caller to wait until it round-trip confirms a connection to the server
            ServicePointManager.Expect100Continue = false;

            // More info on Nagle at WikiPedia - can help perf (helps w/ conn reliability)
            ServicePointManager.UseNagleAlgorithm = false;

            //a new twist to existing connections
            var knownServicePointConnection = ServicePointManager.FindServicePoint(InstanceUri);
            if (knownServicePointConnection != null)
            {
                knownServicePointConnection.ConnectionLimit = ServicePointManager.DefaultConnectionLimit;
                knownServicePointConnection.Expect100Continue = ServicePointManager.Expect100Continue;
                knownServicePointConnection.UseNagleAlgorithm = ServicePointManager.UseNagleAlgorithm;
            }
#endif
        }
    }
}
