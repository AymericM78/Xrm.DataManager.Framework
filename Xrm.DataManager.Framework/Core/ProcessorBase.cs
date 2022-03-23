

using System;

namespace Xrm.DataManager.Framework
{
    public abstract class ProcessorBase
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

        public ProcessorBase()
        {
            JobSettings = new JobSettings();
            if (JobSettings.ApplicationInsightsEnabled)
            {
                Logger = new ApplicationInsightLogger(JobSettings);
            }
            else if (JobSettings.GrayLogEnabled)
            {
                Logger = new GrayLogger(JobSettings);
            }
            else
            {
                Logger = new FileLogger(JobSettings);
            }
            ConsoleHelper = new ConsoleHelper(Logger);

            Utilities.OutputContextInformation(JobSettings, Logger);
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

            // Pre Operation
            selectedDataJob.PreOperation(ProxiesPool.MainProxy);

            // Run the main process
            selectedDataJob.Run();

            // Post Operation
            selectedDataJob.PostOperation(ProxiesPool.MainProxy);
            Logger.LogInformation($"Job stop : {JobSettings.SelectedJobName}", selectedDataJob.ContextProperties);
        }

        /// <summary>
        /// Initialize Organization client for D365 integration
        /// </summary>
        protected void InitializeOrganizationServiceManager(Instance instance = null)
        {
            if (JobSettings.ConnectionStringDefined)
            {
                InitializeOrganizationServiceManager(instance.ConnectionString);
            }
            else
            {
                if (instance != null)
                {
                    JobSettings.SelectedInstanceName = instance.UniqueName;
                    if (!string.IsNullOrWhiteSpace(instance.ConnectionString))
                    {
                        ProxiesPool = new ProxiesPool(instance.ConnectionString, this.Logger);
                        InitializeOrganizationServiceManager(instance.ConnectionString);
                    }
                    else
                    {
                        throw new Exception("ConnectionString attribute is not defined in instances.xml!");
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionString"></param>
        protected void InitializeOrganizationServiceManager(string connectionString)
        {
            ProxiesPool = new ProxiesPool(connectionString, this.Logger);
            Logger.LogInformation($"Organization service initialized to {ProxiesPool.InstanceUri} with user ID : {ProxiesPool.MainProxy.CallerId} !");            
        }
    }
}
