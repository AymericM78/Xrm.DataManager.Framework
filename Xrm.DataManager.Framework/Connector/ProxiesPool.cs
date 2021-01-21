using System;
using System.Net;
using System.Threading;

namespace Xrm.DataManager.Framework
{
    public class ProxiesPool
    {
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

        protected string UserName
        {
            get; set;
        }

        protected string Password
        {
            get; set;
        }

        public ManagedTokenOrganizationServiceProxy MainProxy
        {
            get;
            private set;
        }

        public ProxiesPool(string connectionString, ILogger logger)
        {
            ConnectionString = connectionString;
            Logger = logger;
            InstanceUri = new Uri(ExtractUrlFromConnectionString(connectionString));

            ApplyConnectionOptimizations();
            InitializeMainProxy();
        }

        private string ExtractUrlFromConnectionString(string connectionString)
        {
            if (!connectionString.Contains("Url="))
            {
                throw new ArgumentException($"Crm connectionstring is invalid : url parameter is not provided! '{connectionString}'");
            }

            var parameters = connectionString.Split(';');
            foreach (var parameter in parameters)
            {
                var keyValue = parameter.Split('=');
                if (string.IsNullOrEmpty(keyValue[0]))
                    continue;

                var paramKey = keyValue[0].Trim();
                if (paramKey == "Url")
                {
                    return keyValue[1].Trim();
                }
            }
            return null;
        }

        private void InitializeMainProxy()
        {
            MainProxy = GetProxy();
        }

        public ManagedTokenOrganizationServiceProxy GetProxy(int retryCount = 0)
        {
            try
            {
                var proxy = new ManagedTokenOrganizationServiceProxy(this.ConnectionString, this.Logger);
                return proxy;
            }
            catch (Exception ex)
            {
                retryCount++;
                Thread.Sleep(2000 * retryCount);

                if (retryCount > 5)
                {
                    throw ex;
                }
                return GetProxy(retryCount);
            }
        }

        /// <summary>
        /// Optimize connection performances
        /// </summary>
        protected void ApplyConnectionOptimizations()
        {
            // If you're using an old version of .NET this will enable TLS 1.2
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Change max connections from .NET to a remote service default: 2
            ServicePointManager.DefaultConnectionLimit = 85;

            // Bump up the min threads reserved for this app to ramp connections faster - minWorkerThreads defaults to 4, minIOCP defaults to 4 
            ThreadPool.SetMinThreads(10, 10);

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
        }
    }
}
