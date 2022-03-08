using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace Xrm.DataManager.Framework.Tests
{
    public class CancelAndRemoveAsyncTasksDataJob : PickAndProcessDataJobBase//FullProcessDataJobBase//PickAndProcessDataJobBase
    {
        public CancelAndRemoveAsyncTasksDataJob(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters)
        {

        }

        public override string GetName() => "CancelAndRemoveAsyncTasksDataJob - Remove in progress system jobs";

        public override QueryExpression GetQuery(Guid callerId)
        {
            var query = new QueryExpression("asyncoperation");
            query.ColumnSet.AddColumn("statecode");
            query.Criteria.AddCondition("statecode", ConditionOperator.NotEqual, 3);
            //query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 3);
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

            //var currentState = record.GetAttributeValue<OptionSetValue>("statecode");
            //if (currentState.Value == 3)
            //{
            //    return;
            //}

            var statusUpdate = new Entity(record.LogicalName, record.Id);
            statusUpdate["statecode"] = new OptionSetValue(3);
            statusUpdate["statuscode"] = new OptionSetValue(32);
            try
            {
                proxy.Update(statusUpdate);
            }
            catch
            {

            }
            return;
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
