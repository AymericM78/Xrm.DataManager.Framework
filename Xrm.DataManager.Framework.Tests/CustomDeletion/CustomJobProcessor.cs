using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xrm.DataManager.Framework.Tests
{
    class CustomJobProcessor : ProcessorBase
    {
        public class BulkDeleteConfiguration
        {
            [JsonPropertyName("items")]
            public List<BulkDeleteConfigurationItem> Items { get; set; }
        }

        public class BulkDeleteConfigurationItem
        {
            [JsonPropertyName("entityName")]
            public string EntityName { get; set; }

            [JsonPropertyName("retentionDays")]
            public int RetentionDays { get; set; }
        }

        private BulkDeleteConfiguration BulkDeleteConfigurationData { get; set; }

        public CustomJobProcessor()
        {
            InitializeOrganizationServiceManager();

            var config = JobSettings.GetOptionalParameter<string>("DeletionConfig");
            config = config.Replace('\'', '"');
            BulkDeleteConfigurationData = JsonSerializer.Deserialize<BulkDeleteConfiguration>(config);
        }

        public void Execute()
        {
            var parameters = new JobProcessParameters
            {
                ProxiesPool = ProxiesPool,
                Logger = Logger,
                CallerId = CallerId
            };

            foreach (var item in BulkDeleteConfigurationData.Items)
            {
                var job = new CustomDeletionJob(JobSettings, parameters, item.EntityName, item.RetentionDays);
                RunJob(job);
            }
        }
    }
}
