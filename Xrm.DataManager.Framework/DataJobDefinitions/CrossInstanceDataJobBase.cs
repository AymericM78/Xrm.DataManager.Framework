using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;

namespace Xrm.DataManager.Framework
{
    /// <summary>
    /// FullProcess pattern with records retrieved from source connection (Source.Connection.Name) and processed on target connection
    /// ProcessRecord : context.Record is the source record, context.Proxy is connected to target (ex : delete on target the records retrieved from source)
    /// </summary>
    public abstract class CrossInstanceDataJobBase : FullProcessDataJobBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="jobSettings"></param>
        public CrossInstanceDataJobBase(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters)
        {
        }

        /// <summary>
        /// Records to retrieve from source connection
        /// </summary>
        /// <param name="callerId">Source connection user id</param>
        /// <returns></returns>
        public abstract override QueryExpression GetQuery(Guid callerId);

        /// <summary>
        /// Process source record on target connection
        /// </summary>
        /// <param name="context">Record : source record / Proxy : target connection</param>
        public abstract override void ProcessRecord(JobExecutionContext context);

        /// <summary>
        /// connectionStrings entry to read records from (default : Source.Connection.Name setting)
        /// </summary>
        /// <returns></returns>
        public virtual string GetSourceConnectionName() => JobSettings.SourceConnectionName;

        /// <summary>
        /// Source connection (opened on first use)
        /// </summary>
        protected ProxiesPool SourceProxiesPool => GetSourceProxiesPool(GetSourceConnectionName());

        /// <summary>
        /// Retrieve GetQuery records from source connection
        /// Override to use another data source (ex : TdsClient with SourceProxiesPool.MainProxy)
        /// </summary>
        protected override List<Entity> RetrieveRecords() => RetrieveSourceRecords(GetQuery(SourceProxiesPool.MainProxy.CallerId));

        /// <summary>
        /// Retrieve records from source connection
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        protected List<Entity> RetrieveSourceRecords(QueryExpression query)
        {
            Logger.LogInformation($"Retrieving '{query.EntityName}' records from '{GetSourceConnectionName()}' to process on '{JobSettings.SelectedConnectionName}'", ContextProperties);
            return RetrieveAll(SourceProxiesPool, query);
        }
    }
}
