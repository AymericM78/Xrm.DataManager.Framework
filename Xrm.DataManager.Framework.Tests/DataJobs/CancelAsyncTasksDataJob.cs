using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace Xrm.DataManager.Framework.Tests
{
    public class CancelAsyncTasksDataJob : PickAndProcessDataJobBase//FullProcessDataJobBase//PickAndProcessDataJobBase
    {
        public CancelAsyncTasksDataJob(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters)
        {

        }

        public override string GetName() => "CancelAsyncTasksDataJob - Remove in progress system jobs";

        public override QueryExpression GetQuery(Guid callerId)
        {
            var query = new QueryExpression("asyncoperation");
            query.ColumnSet.AddColumn("statecode");
            query.Criteria.AddCondition("statecode", ConditionOperator.NotEqual, 3);
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

            var statusUpdate = new Entity(record.LogicalName, record.Id);
            statusUpdate["statecode"] = new OptionSetValue(3);
            statusUpdate["statuscode"] = new OptionSetValue(32);
            
            proxy.Update(statusUpdate);
        }
    }
}
