using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Xrm.DataManager.Framework;
using System;

namespace Xrm.DataManager.Framework.Tests
{
    public class TestPickAndProcessDataJob : PickAndProcessDataJobBase
    {
        public TestPickAndProcessDataJob(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters)
        {

        }

        public override string GetName() => "PickAndProcess Test";

        public override QueryExpression GetQuery(Guid callerId)
        {
            var query = new QueryExpression("plugintracelog")
            {
                ColumnSet = new ColumnSet()
            };
            query.Criteria.AddCondition("createdon", ConditionOperator.OlderThanXDays, 1);

            query.AddOrder("createdon", OrderType.Ascending);
            query.NoLock = true;
            return query;
        }

        public override void ProcessRecord(ManagedTokenOrganizationServiceProxy proxy, Entity record)
        {
            proxy.Delete(record.LogicalName, record.Id);
        }
    }
}
