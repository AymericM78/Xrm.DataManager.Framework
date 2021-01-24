using Microsoft.Xrm.Sdk;
using System.Collections.Generic;

namespace Xrm.DataManager.Framework
{
    public class JobExecutionContext
    {
        public ManagedTokenOrganizationServiceProxy Proxy
        {
            get;
            internal set;
        }

        public Entity Record
        {
            get;
            internal set;
        }

        private Dictionary<string, string> Properties;

        public JobExecutionContext()
        {
            Properties = new Dictionary<string, string>();
        }
        public JobExecutionContext(ManagedTokenOrganizationServiceProxy proxy)
        {
            Proxy = proxy;
            Properties = new Dictionary<string, string>();

            DumpProxyToMetrics(proxy);
        }

        public JobExecutionContext(ManagedTokenOrganizationServiceProxy proxy, Entity record)
        {
            Proxy = proxy;
            Record = record;
            Properties = new Dictionary<string, string>();

            PushRecordToMetrics(record);
            DumpProxyToMetrics(proxy);
        }

        public void PushRecordToMetrics(Entity record)
        {
            foreach (var attribute in record.Attributes)
            {
                var key = attribute.Key;
                if (record.FormattedValues.Contains(key))
                {
                    PushMetric($"Crm.Record.{key}", record.FormattedValues[key]);
                }
                else
                {
                    var value = record[key];
                    if (value is EntityReference)
                    {
                        continue;
                    }
                    if (value is OptionSetValue)
                    {
                        continue;
                    }

                    PushMetric($"Crm.Record.{key}", value.ToString());
                }
            }
        }

        private void DumpProxyToMetrics(ManagedTokenOrganizationServiceProxy proxy)
        {
            PushMetric($"Crm.CallerId", proxy.CallerId.ToString());
            PushMetric($"Crm.Auth.Type", proxy.CrmServiceClient.ActiveAuthenticationType.ToString());
            PushMetric($"Crm.Instance.Url", proxy.EndpointUrl);;
            PushMetric($"Crm.Instance.DisplayName", proxy.CrmServiceClient.ConnectedOrgFriendlyName.ToString());
        }

        public void PushMetric(string key, string value) => Properties.Add(key, value);

        public void PushMetrics(Dictionary<string, string> properties)
        {
            foreach (var property in properties)
            {
                Properties.Add(property.Key, property.Value);
            }
        }

        public Dictionary<string, string> DumpMetrics() => Properties;
    }
}
