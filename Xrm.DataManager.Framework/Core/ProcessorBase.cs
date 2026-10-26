

using System;
using System.Collections.Generic;
using System.Linq;

namespace Xrm.DataManager.Framework
{
    public abstract class ProcessorBase : IDisposable
    {
        protected JobSettings JobSettings
        {
            get;
        }
        /// <summary>
        /// Target connection
        /// </summary>
        protected ProxiesPool ProxiesPool
        {
            get; private set;
        }
        /// <summary>
        /// All connections (connectionStrings entries), opened on first use
        /// </summary>
        protected ProxiesPoolManager Connections
        {
            get; private set;
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
            get; private set;
        }

        /// <summary>
        /// Constructor with default logger (FileLogger)
        /// </summary>
        public ProcessorBase() : this(null)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggerFactory">ex : settings => new ApplicationInsightLogger(settings)</param>
        public ProcessorBase(Func<JobSettings, ILogger> loggerFactory)
        {
            JobSettings = new JobSettings();
            Logger = loggerFactory?.Invoke(JobSettings) ?? new FileLogger(JobSettings);
            ConsoleHelper = new ConsoleHelper(Logger);

            Utilities.OutputContextInformation(JobSettings, Logger);

            if (loggerFactory == null && JobSettings.ApplicationInsightsEnabled)
            {
                Logger.LogInformation("Application Insights is configured but default FileLogger is used : add Xrm.DataManager.Framework.ApplicationInsights package and provide ApplicationInsightLogger to processor constructor!");
            }
        }

        /// <summary>
        /// Execute given job
        /// </summary>
        /// <param name="selectedDataJob"></param>
        protected void RunJob(DataJobBase selectedDataJob)
        {
            // Prevent connection to prod
            // TODO : Handle production instance definition
            //if (selectedDataJob.IsAllowedToRunInProduction() == false)
            //{
            //    throw new Exception("Execution is not allowed on production");
            //}

            Logger.LogInformation($"Job start : {JobSettings.SelectedJobName}", selectedDataJob.ContextProperties);

            try
            {
                // Pre Operation
                selectedDataJob.PreOperation(ProxiesPool.MainProxy);

                // Run the main process
                selectedDataJob.Run();

                // Post Operation
                selectedDataJob.PostOperation(ProxiesPool.MainProxy);
                Logger.LogInformation($"Job stop : {JobSettings.SelectedJobName}", selectedDataJob.ContextProperties);
            }
            finally
            {
                // Send buffered telemetry
                Logger.Flush();
            }
        }

        /// <summary>
        /// Initialize Dataverse connections from connectionStrings section, target connection is :
        /// 1. Entry named by JobSettings.SelectedConnectionName (command line) or Target.Connection.Name setting
        /// 2. Single entry, or entry selected in console if several are defined
        /// Other entries are available with Connections (opened on first use)
        /// </summary>
        protected void InitializeOrganizationServiceManager()
        {
            var connectionStrings = JobSettings.GetDataverseConnectionStrings();
            var requestedConnectionName = string.IsNullOrEmpty(JobSettings.SelectedConnectionName) ? JobSettings.TargetConnectionName : JobSettings.SelectedConnectionName;
            var connectionName = ResolveConnectionName(requestedConnectionName, connectionStrings, ConsoleHelper.ConnectionStringSelect);

            Connections = new ProxiesPoolManager(connectionStrings, Logger, JobSettings.AffinityCookieEnabled, JobSettings.ConnectionOptimizationsEnabled);
            InitializeTargetConnection(connectionName);
        }

        /// <summary>
        /// Resolve target connection name (see InitializeOrganizationServiceManager)
        /// </summary>
        /// <param name="requestedConnectionName">Requested connectionStrings entry name</param>
        /// <param name="connectionStrings">Dataverse connectionStrings entries</param>
        /// <param name="selector">Interactive selection when several entries are defined</param>
        /// <returns>connectionStrings entry name</returns>
        internal static string ResolveConnectionName(string requestedConnectionName, Dictionary<string, string> connectionStrings, Func<List<string>, string> selector)
        {
            if (connectionStrings.Count == 0)
            {
                throw new Exception("Dataverse connection is not defined : add connectionStrings section in configuration file!");
            }

            if (string.IsNullOrEmpty(requestedConnectionName))
            {
                // Display connection strings selector
                return (connectionStrings.Count == 1) ? connectionStrings.Keys.First() : selector(connectionStrings.Keys.ToList());
            }

            var connectionName = connectionStrings.Keys.FirstOrDefault(k => string.Equals(k, requestedConnectionName, StringComparison.OrdinalIgnoreCase));
            if (connectionName == null)
            {
                throw new Exception($"Connection string '{requestedConnectionName}' is not defined in connectionStrings section! (Available : {string.Join(", ", connectionStrings.Keys)})");
            }
            return connectionName;
        }

        /// <summary>
        /// Initialize Dataverse connection with given connection string (ex : retrieved from a vault)
        /// </summary>
        /// <param name="connectionString"></param>
        protected void InitializeOrganizationServiceManager(string connectionString)
        {
            const string connectionName = "Target";
            var connectionStrings = new Dictionary<string, string> { { connectionName, connectionString } };
            Connections = new ProxiesPoolManager(connectionStrings, Logger, JobSettings.AffinityCookieEnabled, JobSettings.ConnectionOptimizationsEnabled);
            InitializeTargetConnection(connectionName);
        }

        private void InitializeTargetConnection(string connectionName)
        {
            JobSettings.SelectedConnectionName = connectionName;
            JobSettings.SelectedConnectionString = Connections.GetConnectionString(connectionName);
            ProxiesPool = Connections.GetProxiesPool(connectionName);
            CallerId = ProxiesPool.MainProxy.CallerId;
            Logger.LogInformation($"Organization service initialized to {ProxiesPool.InstanceUri} ({connectionName}) with user ID : {ProxiesPool.MainProxy.CallerId} !");
            Logger.LogInformation($"Server recommended degree of parallelism : {ProxiesPool.MainProxy.ServiceClient.RecommendedDegreesOfParallelism} (Process.Thread.Number = {JobSettings.ThreadNumber})");
        }

        /// <summary>
        /// Parameters provided to data jobs (connections, logger...)
        /// </summary>
        /// <returns></returns>
        protected JobProcessParameters CreateJobProcessParameters()
        {
            return new JobProcessParameters
            {
                ProxiesPool = ProxiesPool,
                Connections = Connections,
                Logger = Logger,
                CallerId = CallerId
            };
        }

        /// <summary>
        /// Close Dataverse connections
        /// </summary>
        public void Dispose() => Connections?.Dispose();
    }
}
