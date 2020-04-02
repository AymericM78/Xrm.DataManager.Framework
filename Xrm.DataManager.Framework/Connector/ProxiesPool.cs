using Microsoft.Xrm.Client;
using System;
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
            InstanceUri = CrmConnection.Parse(ConnectionString).ServiceUri;

            InitializeMainProxy();
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
    }
}
