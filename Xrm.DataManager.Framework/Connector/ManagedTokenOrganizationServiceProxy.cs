using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;
using System;
using System.IdentityModel.Tokens;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Security;

namespace Xrm.DataManager.Framework
{
    public class ManagedTokenOrganizationServiceProxy : IOrganizationService, IDisposable
    {
        private string CrmConnectionString;

        public CrmServiceClient CrmServiceClient
        {
            get;
            set;
        }

        private CrmServiceClient ParentCrmServiceClient
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
            if (ParentCrmServiceClient == null)
            {
                return false;
            }

            return (ParentCrmServiceClient.ActiveAuthenticationType == Microsoft.Xrm.Tooling.Connector.AuthenticationType.OAuth
                          || ParentCrmServiceClient.ActiveAuthenticationType == Microsoft.Xrm.Tooling.Connector.AuthenticationType.ClientSecret
                          || ParentCrmServiceClient.ActiveAuthenticationType == Microsoft.Xrm.Tooling.Connector.AuthenticationType.Certificate);
        }

        public ManagedTokenOrganizationServiceProxy(string crmConnectionString, ILogger logger, Guid callerId, CrmServiceClient parentClient = null)
        {
            CrmConnectionString = crmConnectionString;
            Logger = logger;
            CallerId = callerId;
            ParentCrmServiceClient = parentClient;
            InitializeClient();
        }

        private void InitializeClient(bool forceReconnect = false)
        {
            if (forceReconnect)
            {
                if (ParentCrmServiceClient != null)
                {
                    ParentCrmServiceClient = new CrmServiceClient(CrmConnectionString);
                }
            }

            if (IsCloneAvailable())
            {
                Retry(() =>
                {
                    CrmServiceClient = ParentCrmServiceClient.Clone();
                    if (CrmServiceClient.LastCrmException != null)
                    {
                        throw CrmServiceClient.LastCrmException;
                    }
                    return true;
                });
            }
            else
            {
                Retry(() =>
                {
                    CrmServiceClient = new CrmServiceClient(CrmConnectionString);
                    if (CrmServiceClient.LastCrmException != null)
                    {
                        throw CrmServiceClient.LastCrmException;
                    }
                    return true;
                });
            }

            if (CallerId == null)
            {
                CallerId = CrmServiceClient.GetMyCrmUserId();
            }

            ActiveAuthenticationType = CrmServiceClient.ActiveAuthenticationType.ToString();
            ConnectedOrgFriendlyName = CrmServiceClient.ConnectedOrgFriendlyName;
            EndpointUrl = CrmServiceClient.ConnectedOrgPublishedEndpoints.Values.FirstOrDefault();
        }

        public Guid Create(Entity entity)
        {
            return Retry(() => { return CrmServiceClient.Create(entity); });
        }

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            return Retry(() => { return CrmServiceClient.Retrieve(entityName, id, columnSet); });
        }

        public void Update(Entity entity)
        {
            Retry(() => { CrmServiceClient.Update(entity); return true; });
        }

        public void Delete(string entityName, Guid id)
        {
            Retry(() => { CrmServiceClient.Delete(entityName, id); return true; });
        }

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            return Retry(() => { return CrmServiceClient.Execute(request); });
        }

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            Retry(() => { CrmServiceClient.Associate(entityName, entityId, relationship, relatedEntities); return true; });
        }

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            Retry(() => { CrmServiceClient.Disassociate(entityName, entityId, relationship, relatedEntities); return true; });
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            return Retry(() => { return CrmServiceClient.RetrieveMultiple(query); });
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

        public void Dispose() => CrmServiceClient.Dispose();

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