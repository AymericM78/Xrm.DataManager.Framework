namespace Xrm.DataManager.Framework.Tests
{
    /// <summary>
    /// Records retrieved with SQL (TDS endpoint) : contactid is added to the query when missing
    /// </summary>
    public class TestSqlFullProcessDataJob : SqlFullProcessDataJobBase
    {
        private const string DomainExtension = ".com";

        public TestSqlFullProcessDataJob(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters)
        {

        }

        public override string GetName() => "SqlFullProcess Test - Replace contact email by @....fake";

        public override string GetSqlQuery() => $"SELECT TOP 1000 emailaddress1, createdby, createdbyname FROM contact WHERE emailaddress1 LIKE '%{DomainExtension}'";

        public override void ProcessRecord(JobExecutionContext context)
        {
            var proxy = context.Proxy;
            var record = context.Record;

            // SQL columns are read from FormattedValues
            var email = record.GetSqlValue<string>("emailaddress1");
            var creator = record.GetSqlEntityReference("createdby", "systemuser");
            context.PushMetric("Email avant", email);
            context.PushMetric("Créé par", creator?.Name);

            email = email.ToLowerInvariant();
            email = email.Replace(DomainExtension, ".fake");

            context.PushMetric("Email apres", email);

            // Record attributes are empty : only emailaddress1 is updated
            record["emailaddress1"] = email;
            proxy.Update(record);
        }
    }
}
