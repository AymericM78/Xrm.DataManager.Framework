using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Xrm.DataManager.Framework
{
    public abstract class DataJobBase
    {
        protected Dictionary<string, object> JobProperties { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, string> ContextProperties { get; set; } = new Dictionary<string, string>();

        protected JobSettings JobSettings
        {
            get;
        }

        /// <summary>
        /// Target connection (Target.Connection.Name)
        /// </summary>
        protected ProxiesPool ProxiesPool
        {
            get;
        }

        /// <summary>
        /// All connections by name (ex : Connections.GetProxiesPool("Prod") to read reference data from another instance)
        /// </summary>
        protected ProxiesPoolManager Connections
        {
            get;
        }

        protected ConsoleHelper ConsoleHelper
        {
            get;
        }

        protected ILogger Logger
        {
            get;
        }

        protected Guid CallerId
        {
            get;
        }

        private int? overrideThreadNumber;
        protected virtual int? OverrideThreadNumber
        {
            get => overrideThreadNumber;
            set => overrideThreadNumber = value;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="jobSettings"></param>
        public DataJobBase(JobSettings jobSettings, JobProcessParameters parameters)
        {
            JobSettings = jobSettings;
            ProxiesPool = parameters.ProxiesPool;
            Connections = parameters.Connections;
            Logger = parameters.Logger;
            CallerId = parameters.CallerId;
            ConsoleHelper = new ConsoleHelper(Logger);

            ContextProperties = Utilities.GetContextProperties(JobSettings);
        }

        /// <summary>
        /// Specify a custom job name for tracing
        /// </summary>
        /// <returns></returns>
        public abstract string GetName();

        /// <summary>
        /// Specify if this job could be run in production
        /// Set to false if testing are not fully completed
        /// </summary>
        public virtual bool IsAllowedToRunInProduction() => false;

        /// <summary>
        /// Specify if this job is visible and available for execution
        /// </summary>
        public virtual bool IsEnabled => true;

        /// <summary>
        /// Specify task to run before record processing
        /// </summary>
        public virtual void PreOperation(IManagedTokenOrganizationServiceProxy proxy)
        {
            // Nothing to do here
        }

        /// <summary>
        /// Specify task to run after record processing
        /// </summary>
        public virtual void PostOperation(IManagedTokenOrganizationServiceProxy proxy)
        {
            // Nothing to do here
        }

        public abstract bool Run();

        /// <summary>
        /// Retrieve all records of given query (pagination with Process.Query.RecordLimit)
        /// </summary>
        /// <param name="proxiesPool">Connection to query (ex : ProxiesPool or Connections.GetProxiesPool("Prod"))</param>
        /// <param name="query"></param>
        /// <returns></returns>
        protected List<Entity> RetrieveAll(ProxiesPool proxiesPool, QueryExpression query)
        {
            query.PageInfo.Count = JobSettings.QueryRecordLimit;
            query.NoLock = true;

            return proxiesPool.MainProxy.RetrieveAll(query).Entities.ToList();
        }

        /// <summary>
        /// Get connection to read records from (must be different from target connection)
        /// </summary>
        /// <param name="sourceConnectionName">connectionStrings entry name (ex : Source.Connection.Name setting)</param>
        /// <returns></returns>
        protected ProxiesPool GetSourceProxiesPool(string sourceConnectionName)
        {
            if (string.IsNullOrEmpty(sourceConnectionName))
            {
                throw new Exception("Source connection is not defined : add Source.Connection.Name setting!");
            }
            if (string.Equals(sourceConnectionName, JobSettings.SelectedConnectionName, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Source and target connections must be different! ('{sourceConnectionName}')");
            }
            return Connections.GetProxiesPool(sourceConnectionName);
        }
    }
}
