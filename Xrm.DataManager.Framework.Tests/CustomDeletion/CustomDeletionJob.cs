using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace Xrm.DataManager.Framework.Tests
{
    public class CustomDeletionJob : PickAndProcessDataJobBase
    {
        private string EntityName { get; set; }
        private int RetentionDays { get; set; }

        public CustomDeletionJob(JobSettings jobSettings, JobProcessParameters parameters, string entityName, int retentionDays) : base(jobSettings, parameters)
        {
            EntityName = entityName;
            RetentionDays = retentionDays;
        }

        public override string GetName()
        {
            return $"{this.EntityName} Deletion Job";
        }

        public override QueryExpression GetQuery(Guid callerId)
        {
            var query = new QueryExpression(this.EntityName);
            query.Criteria.AddCondition("createdon", ConditionOperator.OlderThanXDays, RetentionDays);
            query.NoLock = true;
            return query;
        }

        public override void ProcessRecord(JobExecutionContext context)
        {
            var request = new DeleteRequest
            {
                Target = context.Record.ToEntityReference()
            };
            request.Parameters.Add("BypassCustomPluginExecution", true);
            context.Proxy.Execute(request);
        }
    }
}
