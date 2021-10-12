using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace Xrm.DataManager.Framework.Tests
{
    public class RemoveAsyncTasksDataJob : PickAndProcessDataJobBase//FullProcessDataJobBase//PickAndProcessDataJobBase
    {
        public RemoveAsyncTasksDataJob(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters)
        {

        }

        public override string GetName() => "RemoveAsyncTasksDataJob - Remove system jobs";

        public override QueryExpression GetQuery(Guid callerId)
        {
            var query = new QueryExpression("asyncoperation");
            query.ColumnSet.AddColumn("statecode");
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 3);
            query.Criteria.AddCondition("recurrencepattern", ConditionOperator.Null);
            query.AddOrder("createdon", OrderType.Descending);
            query.ColumnSet.AllColumns = false;
            query.NoLock = true;
            return query;
        }

        public override void ProcessRecord(JobExecutionContext context)
        {
            var proxy = context.Proxy;
            var record = context.Record;

            try
            {
                proxy.Delete(record.LogicalName, record.Id);
            }
            catch
            {

            }
        }
    }
}
