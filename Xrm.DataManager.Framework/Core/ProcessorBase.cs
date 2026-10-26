

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
        protected ProxiesPool ProxiesPool
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
        /// Initialize Dataverse connection from configuration :
        /// 1. connectionStrings entry named by JobSettings.SelectedInstanceName (command line) or Crm.Instance.Name setting
        /// 2. Crm.ConnectionString setting
        /// 3. connectionStrings entry (selected in console if several are defined)
        /// </summary>
        protected void InitializeOrganizationServiceManager()
        {
            var instanceName = string.IsNullOrEmpty(JobSettings.SelectedInstanceName) ? JobSettings.CrmInstanceName : JobSettings.SelectedInstanceName;
            var connection = ResolveConnectionString(instanceName, JobSettings.CrmConnectionString, JobSettings.GetConnectionStrings(), ConsoleHelper.ConnectionStringSelect);
            if (connection.Name != null)
            {
                JobSettings.SelectedInstanceName = connection.Name;
            }
            InitializeOrganizationServiceManager(connection.ConnectionString);
        }

        /// <summary>
        /// Resolve connection string to use (see InitializeOrganizationServiceManager)
        /// </summary>
        /// <param name="instanceName">Requested connectionStrings entry name</param>
        /// <param name="crmConnectionString">Crm.ConnectionString setting</param>
        /// <param name="connectionStrings">connectionStrings section entries</param>
        /// <param name="selector">Interactive selection when several entries are defined</param>
        /// <returns>connectionStrings entry name (null for Crm.ConnectionString setting) and connection string</returns>
        internal static (string Name, string ConnectionString) ResolveConnectionString(string instanceName, string crmConnectionString, Dictionary<string, string> connectionStrings, Func<List<string>, string> selector)
        {
            if (string.IsNullOrEmpty(instanceName))
            {
                if (!string.IsNullOrEmpty(crmConnectionString))
                {
                    return (null, crmConnectionString);
                }

                if (connectionStrings.Count == 0)
                {
                    throw new Exception("Dataverse connection is not defined : add Crm.ConnectionString setting or connectionStrings section in configuration file!");
                }

                // Display connection strings selector
                instanceName = (connectionStrings.Count == 1) ? connectionStrings.Keys.First() : selector(connectionStrings.Keys.ToList());
            }

            if (!connectionStrings.TryGetValue(instanceName, out var connectionString))
            {
                throw new Exception($"Connection string '{instanceName}' is not defined in connectionStrings section! (Available : {string.Join(", ", connectionStrings.Keys)})");
            }
            return (instanceName, connectionString);
        }

        /// <summary>
        /// Initialize Dataverse connection with given connection string
        /// </summary>
        /// <param name="connectionString"></param>
        protected void InitializeOrganizationServiceManager(string connectionString)
        {
            JobSettings.SelectedConnectionString = connectionString;
            ProxiesPool = new ProxiesPool(connectionString, this.Logger, JobSettings.AffinityCookieEnabled, JobSettings.ConnectionOptimizationsEnabled);
            CallerId = ProxiesPool.MainProxy.CallerId;
            Logger.LogInformation($"Organization service initialized to {ProxiesPool.InstanceUri} with user ID : {ProxiesPool.MainProxy.CallerId} !");
            Logger.LogInformation($"Server recommended degree of parallelism : {ProxiesPool.MainProxy.ServiceClient.RecommendedDegreesOfParallelism} (Process.Thread.Number = {JobSettings.ThreadNumber})");
        }

        /// <summary>
        /// Close Dataverse connections
        /// </summary>
        public void Dispose() => ProxiesPool?.Dispose();
    }
}
