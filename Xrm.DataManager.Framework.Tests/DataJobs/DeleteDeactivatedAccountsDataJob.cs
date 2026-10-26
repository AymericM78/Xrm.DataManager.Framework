using Microsoft.Xrm.Sdk.Query;
using System;

namespace Xrm.DataManager.Framework.Tests
{
    /// <summary>
    /// Delete on target instance the accounts deactivated on source instance (Source.Connection.Name)
    /// </summary>
    public class DeleteDeactivatedAccountsDataJob : CrossInstanceDataJobBase
    {
        public DeleteDeactivatedAccountsDataJob(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters)
        {

        }

        public override string GetName() => "CrossInstance - Delete accounts deactivated on source instance";

        // Executed on source instance
        public override QueryExpression GetQuery(Guid callerId)
        {
            var query = new QueryExpression("account");
            query.ColumnSet.AddColumn("name");
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 1);
            return query;
        }

        // Record : source account / Proxy : target instance
        public override void ProcessRecord(JobExecutionContext context)
        {
            context.Proxy.Delete(context.Record.LogicalName, context.Record.Id);
        }
    }
}
