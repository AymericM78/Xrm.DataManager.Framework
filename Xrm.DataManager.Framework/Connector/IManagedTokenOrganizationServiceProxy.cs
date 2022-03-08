using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;
using System;

namespace Xrm.DataManager.Framework
{
    public interface IManagedTokenOrganizationServiceProxy : IOrganizationService, IDisposable
    {
        string ActiveAuthenticationType { get; set; }
        Guid CallerId { get; set; }
        string ConnectedOrgFriendlyName { get; set; }
        CrmServiceClient CrmServiceClient { get; set; }
        string EndpointUrl { get; set; }

        void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities);
        Guid Create(Entity entity);
        void Delete(string entityName, Guid id);
        void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities);
        void Dispose();
        OrganizationResponse Execute(OrganizationRequest request);
        Entity Retrieve(string entityName, Guid id, ColumnSet columnSet);
        EntityCollection RetrieveAll(QueryExpression query);
        EntityCollection RetrieveMultiple(QueryBase query);
        void Update(Entity entity);
    }
}