using Microsoft.Xrm.Sdk.Query;
using System;

namespace Xrm.DataManager.Framework.Tests
{
    /// <summary>
    /// Compare active contacts between source instance (Source.Connection.Name) and target instance : CSV report only
    /// </summary>
    public class CompareContactsDataJob : CompareDataJobBase
    {
        public CompareContactsDataJob(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters)
        {

        }

        public override string GetName() => "Compare - Contacts between source and target instances";

        // Executed on both instances (override GetTargetQuery for a different target query)
        public override QueryExpression GetQuery(Guid callerId)
        {
            var query = new QueryExpression("contact");
            query.ColumnSet.AddColumns("fullname", "emailaddress1", "telephone1", "parentcustomerid", "statuscode");
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            return query;
        }
    }
}
