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
            var query = new QueryExpression("asyncoperation")
            {
                ColumnSet = new ColumnSet()
            };
            query.Criteria.AddCondition("createdon", ConditionOperator.OlderThanXDays, 20);

            // Asynch operation is completed
            var filterStatus = query.Criteria.AddFilter(LogicalOperator.Or);
            filterStatus.AddCondition("statecode", ConditionOperator.Equal, 3);

            // Or asynch operation has failed and is waiting for nothing
            var filterWaitingInFailure = filterStatus.AddFilter(LogicalOperator.And);
            filterWaitingInFailure.AddCondition("statuscode", ConditionOperator.Equal, 10);
            filterWaitingInFailure.AddCondition("friendlymessage", ConditionOperator.NotNull);

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
