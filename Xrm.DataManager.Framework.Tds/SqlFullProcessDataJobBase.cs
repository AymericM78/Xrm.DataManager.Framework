using Microsoft.Xrm.Sdk;
using System.Collections.Generic;

namespace Xrm.DataManager.Framework
{
    /// <summary>
    /// FullProcess pattern with records retrieved by a SQL query (Dataverse TDS endpoint)
    /// Settings : Tds.Port (1433 or 5558), Tds.CommandTimeout.Seconds
    /// </summary>
    public abstract class SqlFullProcessDataJobBase : FullProcessDataJobBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="jobSettings"></param>
        public SqlFullProcessDataJobBase(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters)
        {
        }

        /// <summary>
        /// SQL query (single SELECT statement) retrieving records to process
        /// Primary key of the records to process is added if missing, other columns are available with context.Record.GetSqlValue&lt;T&gt;(column)
        /// </summary>
        /// <returns></returns>
        public abstract string GetSqlQuery();

        /// <summary>
        /// Table of the records to process (default : first table of FROM clause)
        /// </summary>
        /// <returns></returns>
        public virtual string GetSqlEntityName() => null;

        protected override List<Entity> RetrieveRecords()
        {
            var commandTimeout = JobSettings.GetOptionalParameter<int>("Tds.CommandTimeout.Seconds", TdsClient.DefaultCommandTimeoutSeconds);
            var port = JobSettings.GetOptionalParameter<int>("Tds.Port", 0);

            var tdsClient = new TdsClient(ProxiesPool.MainProxy, Logger, commandTimeout, port);
            return tdsClient.RetrieveRecords(GetSqlQuery(), GetSqlEntityName());
        }
    }
}
