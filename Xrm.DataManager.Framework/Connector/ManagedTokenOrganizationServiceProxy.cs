using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Extensions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.IdentityModel.Tokens;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Security;

namespace Xrm.DataManager.Framework
{
    public class ManagedTokenOrganizationServiceProxy : IManagedTokenOrganizationServiceProxy
    {
        private string CrmConnectionString;

        private bool EnableAffinityCookie;

        public ServiceClient ServiceClient
        {
            get;
            set;
        }

        [Obsolete("Use ServiceClient instead.")]
        public ServiceClient CrmServiceClient => ServiceClient;

        private ServiceClient ParentServiceClient
        {
            get;
            set;
        }

        public Guid CallerId
        {
            get;
            set;
        }

        public string EndpointUrl
        {
            get;
            set;
        }

        public string ActiveAuthenticationType
        {
            get;
            set;
        }
        public string ConnectedOrgFriendlyName
        {
            get;
            set;
        }

        protected ILogger Logger
        {
            get; set;
        }

        private bool IsCloneAvailable()
        {
            if (ParentServiceClient == null)
            {
                return false;
            }

            return (ParentServiceClient.ActiveAuthenticationType == Microsoft.PowerPlatform.Dataverse.Client.AuthenticationType.OAuth
                          || ParentServiceClient.ActiveAuthenticationType == Microsoft.PowerPlatform.Dataverse.Client.AuthenticationType.ClientSecret
                          || ParentServiceClient.ActiveAuthenticationType == Microsoft.PowerPlatform.Dataverse.Client.AuthenticationType.Certificate);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="crmConnectionString"></param>
        /// <param name="logger"></param>
        /// <param name="callerId">Guid.Empty to resolve current user (WhoAmI)</param>
        /// <param name="parentClient">Connection to clone (OAuth, ClientSecret or Certificate)</param>
        /// <param name="enableAffinityCookie">Disable to spread parallel requests on all Dataverse nodes (bulk operations)</param>
        public ManagedTokenOrganizationServiceProxy(string crmConnectionString, ILogger logger, Guid callerId, ServiceClient parentClient = null, bool enableAffinityCookie = true)
        {
            CrmConnectionString = crmConnectionString;
            Logger = logger;
            CallerId = callerId;
            ParentServiceClient = parentClient;
            EnableAffinityCookie = enableAffinityCookie;
            InitializeClient();
        }

        private void InitializeClient(bool forceReconnect = false)
        {
            if (forceReconnect)
            {
                if (ParentServiceClient != null)
                {
                    ParentServiceClient = new ServiceClient(CrmConnectionString);
                }
            }

            if (IsCloneAvailable())
            {
                Retry(() =>
                {
                    ServiceClient = ParentServiceClient.Clone();
                    if (!ServiceClient.IsReady)
                    {
                        throw ServiceClient.LastException ?? new Exception(ServiceClient.LastError);
                    }
                    return true;
                });
            }
            else
            {
                Retry(() =>
                {
                    ServiceClient = new ServiceClient(CrmConnectionString);
                    if (!ServiceClient.IsReady)
                    {
                        throw ServiceClient.LastException ?? new Exception(ServiceClient.LastError);
                    }
                    return true;
                });
            }

            // Not copied by Clone()
            ServiceClient.EnableAffinityCookie = EnableAffinityCookie;

            if (CallerId == Guid.Empty)
            {
                CallerId = ServiceClient.GetMyUserId();
            }

            ActiveAuthenticationType = ServiceClient.ActiveAuthenticationType.ToString();
            ConnectedOrgFriendlyName = ServiceClient.ConnectedOrgFriendlyName;
            EndpointUrl = ServiceClient.ConnectedOrgPublishedEndpoints.Values.FirstOrDefault();
        }

        public Guid Create(Entity entity)
        {
            return Retry(() => { return ServiceClient.Create(entity); });
        }

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            return Retry(() => { return ServiceClient.Retrieve(entityName, id, columnSet); });
        }

        public void Update(Entity entity)
        {
            Retry(() => { ServiceClient.Update(entity); return true; });
        }

        public void Delete(string entityName, Guid id)
        {
            Retry(() => { ServiceClient.Delete(entityName, id); return true; });
        }

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            return Retry(() => { return ServiceClient.Execute(request); });
        }

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            Retry(() => { ServiceClient.Associate(entityName, entityId, relationship, relatedEntities); return true; });
        }

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            Retry(() => { ServiceClient.Disassociate(entityName, entityId, relationship, relatedEntities); return true; });
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            return Retry(() => { return ServiceClient.RetrieveMultiple(query); });
        }

        /// <summary>
        /// Run query expression with retry and pagination
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public EntityCollection RetrieveAll(QueryExpression query)
        {
            var results = new EntityCollection();
            query.PageInfo.PageNumber = 1;

            var moreRecords = true;
            while (moreRecords)
            {
                var pageResults = RetrieveMultiple(query);
                results.Entities.AddRange(pageResults.Entities);
                query.PageInfo.PagingCookie = pageResults.PagingCookie;
                query.PageInfo.PageNumber++;
                moreRecords = pageResults.MoreRecords;
            }
            return results;
        }

        public void Dispose() => ServiceClient.Dispose();

        private T Retry<T>(Func<T> action)
        {
            var tries = 3;
            while (true)
            {
                try
                {
                    var result = action();
                    return result;
                }
                catch (Exception ex) when (ex is SecurityTokenValidationException || ex is ExpiredSecurityTokenException || ex is SecurityAccessDeniedException || ex is SecurityNegotiationException)
                {
                    if (--tries == 0)
                    {
                        throw;
                    }
                    InitializeClient(true);
                }
                catch (FaultException<OrganizationServiceFault> e) when (TransientIssueManager.IsTransientError(e))
                {
                    if (--tries == 0)
                    {
                        throw;
                    }
                    TransientIssueManager.ApplyDelay(e, Logger);
                }
                catch (Exception ex) when (TransientIssueManager.IsTransientError(ex))
                {
                    if (--tries == 0)
                    {
                        throw;
                    }
                    TransientIssueManager.ApplyDelay(Logger);
                }
            }
        }
    }
}