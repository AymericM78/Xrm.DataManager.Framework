using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Xrm.DataManager.Framework.Tests
{
    /// <summary>
    /// Copy active accounts from Source.Connection.Name instance to Target.Connection.Name instance
    /// </summary>
    public class SyncAccountsDataJob : SyncDataJobBase
    {
        public SyncAccountsDataJob(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters)
        {

        }

        public override string GetName() => "Sync - Copy active accounts from source instance";

        public override QueryExpression GetQuery(Guid callerId)
        {
            var query = new QueryExpression("account");
            query.ColumnSet.AllColumns = true;
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            return query;
        }

        // Contacts are not synchronized : primary contact lookup is excluded
        public override IEnumerable<string> GetExcludedAttributes() => base.GetExcludedAttributes().Concat(new[] { "primarycontactid" });

        // Parent accounts are synchronized by the same job : lookups are set in a second pass
        protected override bool TwoPassDependencies => true;
    }
}
