using Microsoft.Xrm.Client;
using System;
using System.Collections.Concurrent;
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

        public ProxiesPool(string connectionString, ILogger logger)
        {
            ConnectionString = connectionString;
            Logger = logger;
            InstanceUri = CrmConnection.Parse(ConnectionString).ServiceUri;
        }

        // Maintain old auth mechanism for compatibility
        // TODO : Remove old auth mechanism
        public ProxiesPool(Uri instanceUri, string userName, string password)
        {
            InstanceUri = instanceUri;
            UserName = userName;
            Password = password;

            // Cleaning old URL
            var instanceUrl = InstanceUri.ToString();
            instanceUrl = instanceUrl.Replace(".api.", ".");
            if (instanceUrl.Contains("/XRMServices/2011/"))
            {
                instanceUrl = instanceUrl.Remove(instanceUrl.IndexOf("/XRMServices/2011/"));
            }

            this.ConnectionString = $"AuthType=Office365;Url={instanceUrl};Username={UserName};Password={Password};";
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
    }
}
