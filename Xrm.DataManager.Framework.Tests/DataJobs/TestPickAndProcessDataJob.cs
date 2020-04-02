using Microsoft.Xrm.Sdk.Query;
using System;

namespace Xrm.DataManager.Framework.Tests
{
    public class TestPickAndProcessDataJob : PickAndProcessDataJobBase
    {
        public TestPickAndProcessDataJob(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters)
        {

        }

        private const string DomainExtension = ".com";

        public override string GetName() => "PickAndProcess Test - Replace contact email by @....fake";

        protected override int? OverrideThreadNumber => 1;

        public override QueryExpression GetQuery(Guid callerId)
        {
            var query = new QueryExpression("contact");
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("emailaddress1", ConditionOperator.EndsWith, DomainExtension);
            query.NoLock = true;
            return query;
        }

        public override void ProcessRecord(JobExecutionContext context)
        {
            var proxy = context.Proxy;
            var record = context.Record;
            
            var email = record.GetAttributeValue<string>("emailaddress1");
            context.PushMetric("Email avant", email);

            email = email.ToLowerInvariant();
            email = email.Replace(DomainExtension, ".fake");

            context.PushMetric("Email apres", email);

            record["emailaddress1"] = email;
            proxy.Update(context.Record);
        }
    }
}
