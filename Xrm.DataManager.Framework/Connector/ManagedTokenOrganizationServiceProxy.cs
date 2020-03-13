using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;
using System;
using System.IdentityModel.Tokens;
using System.ServiceModel;
using System.ServiceModel.Security;

namespace Xrm.DataManager.Framework
{
    public class ManagedTokenOrganizationServiceProxy : IOrganizationService, IDisposable
    {
        private CrmServiceClient CrmServiceClient;
        private string CrmConnectionString;

        protected ILogger Logger
        {
            get; set;
        }

        public ManagedTokenOrganizationServiceProxy(string crmConnectionString, ILogger logger)
        {
            CrmConnectionString = crmConnectionString;
            Logger = logger;
            InitializeClient();
        }

        private void InitializeClient()
        {
            CrmServiceClient = new CrmServiceClient(CrmConnectionString);
            if (CrmServiceClient.LastCrmException != null)
            {
                Logger.LogMessage(CrmServiceClient.LastCrmError);
                throw CrmServiceClient.LastCrmException;
            }
        }

        public Guid Create(Entity entity)
        {
            try
            {
                return CrmServiceClient.Create(entity);
            }
            catch (Exception ex) when (ex is SecurityTokenValidationException || ex is ExpiredSecurityTokenException || ex is SecurityAccessDeniedException || ex is SecurityNegotiationException)
            {
                InitializeClient();
                return CrmServiceClient.Create(entity);
            }
            catch (FaultException<OrganizationServiceFault> e) when (TransientIssueManager.IsTransientError(e))
            {
                TransientIssueManager.ApplyDelay(e, Logger);
                return CrmServiceClient.Create(entity);
            }
        }

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            try
            {
                return CrmServiceClient.Retrieve(entityName, id, columnSet);
            }
            catch (Exception ex) when (ex is SecurityTokenValidationException || ex is ExpiredSecurityTokenException || ex is SecurityAccessDeniedException || ex is SecurityNegotiationException)
            {
                InitializeClient();
                return CrmServiceClient.Retrieve(entityName, id, columnSet);
            }
            catch (FaultException<OrganizationServiceFault> e) when (TransientIssueManager.IsTransientError(e))
            {
                TransientIssueManager.ApplyDelay(e, Logger);
                return CrmServiceClient.Retrieve(entityName, id, columnSet);
            }
        }

        public void Update(Entity entity)
        {
            try
            {
                CrmServiceClient.Update(entity);
            }
            catch (Exception ex) when (ex is SecurityTokenValidationException || ex is ExpiredSecurityTokenException || ex is SecurityAccessDeniedException || ex is SecurityNegotiationException)
            {
                InitializeClient();
                CrmServiceClient.Update(entity);
            }
            catch (FaultException<OrganizationServiceFault> e) when (TransientIssueManager.IsTransientError(e))
            {
                TransientIssueManager.ApplyDelay(e, Logger);
                CrmServiceClient.Update(entity);
            }
        }

        public void Delete(string entityName, Guid id)
        {
            try
            {
                CrmServiceClient.Delete(entityName, id);
            }
            catch (Exception ex) when (ex is SecurityTokenValidationException || ex is ExpiredSecurityTokenException || ex is SecurityAccessDeniedException || ex is SecurityNegotiationException)
            {
                InitializeClient();
                CrmServiceClient.Delete(entityName, id);
            }
            catch (FaultException<OrganizationServiceFault> e) when (TransientIssueManager.IsTransientError(e))
            {
                TransientIssueManager.ApplyDelay(e, Logger);
                CrmServiceClient.Delete(entityName, id);
            }
        }
        
        public OrganizationResponse Execute(OrganizationRequest request)
        {
            try
            {
                return CrmServiceClient.Execute(request);
            }
            catch (Exception ex) when (ex is SecurityTokenValidationException || ex is ExpiredSecurityTokenException || ex is SecurityAccessDeniedException || ex is SecurityNegotiationException)
            {
                InitializeClient();
                return CrmServiceClient.Execute(request);
            }
            catch (FaultException<OrganizationServiceFault> e) when (TransientIssueManager.IsTransientError(e))
            {
                TransientIssueManager.ApplyDelay(e, Logger);
                return CrmServiceClient.Execute(request);
            }
        }

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            try
            {
                CrmServiceClient.Associate(entityName, entityId, relationship, relatedEntities);
            }
            catch (Exception ex) when (ex is SecurityTokenValidationException || ex is ExpiredSecurityTokenException || ex is SecurityAccessDeniedException || ex is SecurityNegotiationException)
            {
                InitializeClient();
                CrmServiceClient.Associate(entityName, entityId, relationship, relatedEntities);
            }
            catch (FaultException<OrganizationServiceFault> e) when (TransientIssueManager.IsTransientError(e))
            {
                TransientIssueManager.ApplyDelay(e, Logger);
                CrmServiceClient.Associate(entityName, entityId, relationship, relatedEntities);
            }
        }

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            try
            {
                CrmServiceClient.Disassociate(entityName, entityId, relationship, relatedEntities);
            }
            catch (Exception ex) when (ex is SecurityTokenValidationException || ex is ExpiredSecurityTokenException || ex is SecurityAccessDeniedException || ex is SecurityNegotiationException)
            {
                InitializeClient();
                CrmServiceClient.Disassociate(entityName, entityId, relationship, relatedEntities);
            }
            catch (FaultException<OrganizationServiceFault> e) when (TransientIssueManager.IsTransientError(e))
            {
                TransientIssueManager.ApplyDelay(e, Logger);
                CrmServiceClient.Disassociate(entityName, entityId, relationship, relatedEntities);
            }
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            try
            {
                return CrmServiceClient.RetrieveMultiple(query);
            }
            catch (Exception ex) when (ex is SecurityTokenValidationException || ex is ExpiredSecurityTokenException || ex is SecurityAccessDeniedException || ex is SecurityNegotiationException)
            {
                InitializeClient();
                return CrmServiceClient.RetrieveMultiple(query);
            }
            catch (FaultException<OrganizationServiceFault> e) when (TransientIssueManager.IsTransientError(e))
            {
                TransientIssueManager.ApplyDelay(e, Logger);
                return CrmServiceClient.RetrieveMultiple(query);
            }
        }

        public void Dispose() => CrmServiceClient.Dispose();
    }
}