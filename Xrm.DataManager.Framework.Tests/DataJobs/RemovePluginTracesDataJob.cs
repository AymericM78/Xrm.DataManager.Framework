using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace Xrm.DataManager.Framework.Tests
{
    public class RemovePluginTracesDataJob : PickAndProcessDataJobBase//FullProcessDataJobBase//PickAndProcessDataJobBase
    {
        public RemovePluginTracesDataJob(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters)
        {

        }

        public override string GetName() => "RemovePluginTracesDataJob - Remove plugin logs";

        public override QueryExpression GetQuery(Guid callerId)
        {
            var query = new QueryExpression("plugintracelog");
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
