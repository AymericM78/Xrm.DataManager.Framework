

using System;
using System.Net;
using System.Threading;

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

            // Pre Operation
            selectedDataJob.PreOperation(ProxiesPool.MainProxy);

            // Run the main process
            selectedDataJob.Run();

            // Post Operation
            selectedDataJob.PostOperation(ProxiesPool.MainProxy);
        }

        /// <summary>
        /// Initialize Organization client for D365 integration
        /// </summary>
        protected void InitializeOrganizationServiceManager(Instance instance)
        {
            JobSettings.SelectedInstanceName = instance.UniqueName;

            if (!string.IsNullOrWhiteSpace(instance.ConnectionString))
            {
                ProxiesPool = new ProxiesPool(instance.ConnectionString, this.Logger);
            }
            else
            {
                throw new Exception("ConnectionString attribute is not defined in instances.xml!");
            }
            ApplyConnectionOptimizations();
            Logger.LogInformation($"Organization service initialized to {instance.DisplayName} with user {JobSettings.CrmUserName} [ID : {ProxiesPool.MainProxy.CallerId} - Url : {ProxiesPool.MainProxy.EndpointUrl}]!");
        }

        /// <summary>
        /// Optimize connection performances
        /// </summary>
        protected void ApplyConnectionOptimizations()
        {
            // If you're using an old version of .NET this will enable TLS 1.2
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Change max connections from .NET to a remote service default: 2
            ServicePointManager.DefaultConnectionLimit = 85;

            // Bump up the min threads reserved for this app to ramp connections faster - minWorkerThreads defaults to 4, minIOCP defaults to 4 
            ThreadPool.SetMinThreads(10, 10);

            // Turn off the Expect 100 to continue message - 'true' will cause the caller to wait until it round-trip confirms a connection to the server 
            ServicePointManager.Expect100Continue = false;

            // More info on Nagle at WikiPedia - can help perf (helps w/ conn reliability)
            ServicePointManager.UseNagleAlgorithm = false;

            //a new twist to existing connections
            var knownServicePointConnection = ServicePointManager.FindServicePoint(ProxiesPool.InstanceUri);
            if (knownServicePointConnection != null)
            {
                knownServicePointConnection.ConnectionLimit = ServicePointManager.DefaultConnectionLimit;
                knownServicePointConnection.Expect100Continue = ServicePointManager.Expect100Continue;
                knownServicePointConnection.UseNagleAlgorithm = ServicePointManager.UseNagleAlgorithm;
            }
        }
    }
}
