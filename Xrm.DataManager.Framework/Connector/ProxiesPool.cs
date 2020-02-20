using System;
using System.ServiceModel.Description;
using System.Threading;

namespace Xrm.DataManager.Framework
{
    public class ProxiesPool
    {
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

        public ProxiesPool(Uri instanceUri, string userName, string password)
        {
            InstanceUri = instanceUri;
            UserName = userName;
            Password = password;
        }

        public ManagedTokenOrganizationServiceProxy GetProxy(int retryCount = 0)
        {
            var credentials = new ClientCredentials
            {
                UserName = { UserName = this.UserName, Password = this.Password }
            };

            try
            {
                var proxy = new ManagedTokenOrganizationServiceProxy(InstanceUri, credentials);
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
