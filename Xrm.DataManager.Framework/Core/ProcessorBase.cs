

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

            JobSettings.SelectedJobName = selectedDataJob.GetName();
            Logger.LogInformation($"Job start : {JobSettings.SelectedJobName}");

            // Pre Operation
            selectedDataJob.PreOperation(ProxiesPool.MainProxy);

            // Run the main process
            selectedDataJob.Run();

            // Post Operation
            selectedDataJob.PostOperation(ProxiesPool.MainProxy);
            Logger.LogInformation($"Job stop : {JobSettings.SelectedJobName}");
        }

        /// <summary>
        /// Initialize Organization client for D365 integration
        /// </summary>
        protected void InitializeOrganizationServiceManager(Instance instance = null)
        {
            if (JobSettings.ConnectionStringDefined)
            {
                ProxiesPool = new ProxiesPool(JobSettings.CrmConnectionString, this.Logger);
                Logger.LogInformation($"Organization service initialized to {ProxiesPool.InstanceUri} with user ID : {ProxiesPool.MainProxy.CallerId} !");
            }
            else
            {
                if (instance != null)
                {
                    JobSettings.SelectedInstanceName = instance.UniqueName;
                    if (!string.IsNullOrWhiteSpace(instance.ConnectionString))
                    {
                        ProxiesPool = new ProxiesPool(instance.ConnectionString, this.Logger);
                        Logger.LogInformation($"Organization service initialized to {instance.DisplayName} with user {JobSettings.CrmUserName} [ID : {ProxiesPool.MainProxy.CallerId} - Url : {ProxiesPool.MainProxy.EndpointUrl}]!");
                    }
                    else
                    {
                        throw new Exception("ConnectionString attribute is not defined in instances.xml!");
                    }
                }
            }
        }
    }
}
