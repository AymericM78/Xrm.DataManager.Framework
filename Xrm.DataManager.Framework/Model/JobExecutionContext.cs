using Microsoft.Xrm.Sdk;
using System.Collections.Generic;

namespace Xrm.DataManager.Framework
{
    public class JobExecutionContext
    {
        public IManagedTokenOrganizationServiceProxy Proxy
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

        private bool LogRecordAttributes;

        public JobExecutionContext()
        {
            Properties = new Dictionary<string, string>();
        }
        public JobExecutionContext(IManagedTokenOrganizationServiceProxy proxy, bool logRecordAttributes = false)
        {
            Proxy = proxy;
            LogRecordAttributes = logRecordAttributes;
            Properties = new Dictionary<string, string>();

            DumpProxyToMetrics(proxy);
        }

        public JobExecutionContext(IManagedTokenOrganizationServiceProxy proxy, Entity record, bool logRecordAttributes = false)
        {
            Proxy = proxy;
            Record = record;
            LogRecordAttributes = logRecordAttributes;
            Properties = new Dictionary<string, string>();

            PushRecordToMetrics(record);
            DumpProxyToMetrics(proxy);
        }

        public void PushRecordToMetrics(Entity record)
        {
            PushMetric("Crm.Record.LogicalName", record.LogicalName);
            PushMetric("Crm.Record.Id", record.Id.ToString());

            // Record columns may contain personal data : only traced when explicitly enabled
            if (!LogRecordAttributes)
            {
                return;
            }

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
                    if (value == null)
                    {
                        continue;
                    }
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

            // Formatted values without attribute (ex : records retrieved with SQL)
            foreach (var formattedValue in record.FormattedValues)
            {
                if (!record.Attributes.ContainsKey(formattedValue.Key))
                {
                    PushMetric($"Crm.Record.{formattedValue.Key}", formattedValue.Value);
                }
            }
        }

        private void DumpProxyToMetrics(IManagedTokenOrganizationServiceProxy proxy)
        {
            PushMetric($"Crm.CallerId", proxy.CallerId.ToString());
            PushMetric($"Crm.Auth.Type", proxy.ActiveAuthenticationType);
            PushMetric($"Crm.Instance.Url", proxy.EndpointUrl);
            PushMetric($"Crm.Instance.DisplayName", proxy.ConnectedOrgFriendlyName);
        }

        /// <summary>
        /// Add metric or overwrite its value if already defined
        /// </summary>
        public void PushMetric(string key, string value) => Properties.AddOrUpdate(key, value);

        public void PushMetrics(Dictionary<string, string> properties)
        {
            foreach (var property in properties)
            {
                Properties.AddOrUpdate(property.Key, property.Value);
            }
        }

        public Dictionary<string, string> DumpMetrics() => Properties;
    }
}
