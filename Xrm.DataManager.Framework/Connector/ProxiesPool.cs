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

        public IManagedTokenOrganizationServiceProxy MainProxy
        {
            get;
            private set;
        }

        public ProxiesPool(string connectionString, ILogger logger)
        {
            ConnectionString = connectionString;
            Logger = logger;
            InstanceUri = new Uri(ExtractUrlFromConnectionString(connectionString));

            InitializeMainProxy();
            ApplyConnectionOptimizations();
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
            MainProxy = new ManagedTokenOrganizationServiceProxy(this.ConnectionString, this.Logger, Guid.Empty);

            if (MainProxy.CrmServiceClient.LastCrmException != null)
            {
                Logger.LogInformation($"Failed to connect to CRM => {MainProxy.CrmServiceClient.LastCrmError}!");
                throw MainProxy.CrmServiceClient.LastCrmException;
            }

            MainProxy.CallerId = MainProxy.CrmServiceClient.GetMyCrmUserId();
        }

        public IManagedTokenOrganizationServiceProxy GetProxy(int retryCount = 0)
        {
            var proxy = new ManagedTokenOrganizationServiceProxy(this.ConnectionString, this.Logger, MainProxy.CallerId, this.MainProxy.CrmServiceClient);
            return proxy;
        }

        /// <summary>
        /// Optimize connection performances
        /// </summary>
        protected void ApplyConnectionOptimizations()
        {
            // If you're using an old version of .NET this will enable TLS 1.2
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Get the current settings for the Thread Pool
            ThreadPool.GetMinThreads(out int minWorker, out int minIOC);

            // The number of worker threads to make simultaneous requests (do not go over 64)
            const int numberRequests = 64;
            // without setting the thread pool up, there was enough of a delay to cause timeouts!
            ThreadPool.SetMinThreads(numberRequests, minIOC);

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
        }
    }
}
