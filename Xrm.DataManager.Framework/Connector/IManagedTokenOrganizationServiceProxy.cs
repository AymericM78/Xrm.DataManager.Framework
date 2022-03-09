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

        EntityCollection RetrieveAll(QueryExpression query);
    }
}