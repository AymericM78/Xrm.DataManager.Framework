using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace Xrm.DataManager.Framework
{
    public interface IManagedTokenOrganizationServiceProxy : IOrganizationService, IDisposable
    {
        string ActiveAuthenticationType { get; set; }
        Guid CallerId { get; set; }
        string ConnectedOrgFriendlyName { get; set; }
        ServiceClient ServiceClient { get; set; }
        [Obsolete("Use ServiceClient instead.")]
        ServiceClient CrmServiceClient { get; }
        string EndpointUrl { get; set; }

        EntityCollection RetrieveAll(QueryExpression query);
    }
}