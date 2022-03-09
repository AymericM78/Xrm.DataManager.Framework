
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace Xrm.DataManager.Framework
{
    public abstract class FullProcessDataJobBase : DataJobBase
    {
        /// <summary>
        /// Get file path where progress information is stored
        /// </summary>
        public string ProgressFilePath => GetProgressFilePath();

        private const int DefaultProgressDisplayStep = 100;
        private int? overrideProgressDisplayStep;
        protected virtual int? OverrideProgressDisplayStep
        {
            get
            {
                return overrideProgressDisplayStep;
            }
            set
            {
                overrideProgressDisplayStep = value;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="jobSettings"></param>
        public FullProcessDataJobBase(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters)
        {
        }

        /// <summary>
        /// Get file path where progress information is stored
        /// </summary>
        /// <returns></returns>
        internal string GetProgressFilePath()
        {
            string fileName = $"{this.GetType().Name}.txt";
            return Path.Combine(Environment.CurrentDirectory, fileName);
        }

        /// <summary>
        /// Define QueryExpression to retrieve record collection that should be processed
        /// </summary>
        /// <param name="callerId"></param>
        /// <returns></returns>
        public virtual QueryExpression GetQuery(Guid callerId) => throw new NotImplementedException();
               
        /// <summary>
        /// Specify task to run after record retrieve operation in order to allow collection operation (ie grouping ...)
        /// </summary>
        /// <param name="records"></param>
        /// <returns></returns>
        public virtual IEnumerable<Entity> PrepareData(IEnumerable<Entity> records) => records;

        /// <summary>
        /// Apply record modification
        /// </summary>
        /// <param name="jobExecutionContext"></param>
        public virtual void ProcessRecord(JobExecutionContext jobExecutionContext) => throw new NotImplementedException();

        /// <summary>
        /// Run the job
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public override bool Run()
        {
            var progressWriter = new MultiThreadFileWriter(ProgressFilePath);

            Logger.LogInformation($"Checking {ProgressFilePath} existence...", base.ContextProperties);
            // Load already processed items from tracking file if exists
            var processedItems = new List<string>();
            if (File.Exists(ProgressFilePath))
            {
                Logger.LogInformation($"File {ProgressFilePath} detected! Continue process at it last state", base.ContextProperties);

                var lines = File.ReadAllLines(ProgressFilePath);
                processedItems = lines.ToList();
            }
            else
            {
                Logger.LogInformation($"File {ProgressFilePath} not detected! Start process from 0", base.ContextProperties);
            }

            var jobName = GetName();
            var query = GetQuery(CallerId);
            query.PageInfo.Count = JobSettings.QueryRecordLimit;
            query.NoLock = true;

            var results = ProxiesPool.MainProxy.RetrieveAll(query);
            Logger.LogInformation($"Retrieved {results.Entities.Count} records from CRM", base.ContextProperties);
            var processedItemCount = 0;
            var stopwatch = Stopwatch.StartNew();
            var data = PrepareData(results.Entities);
            var dataCount = data.Count();
            Logger.LogInformation($"{dataCount} records to process", base.ContextProperties);

            var threads = (this.OverrideThreadNumber.HasValue) ? this.OverrideThreadNumber : JobSettings.ThreadNumber;
            var progressDisplayStep = (this.OverrideProgressDisplayStep.HasValue) ? this.OverrideProgressDisplayStep.Value : DefaultProgressDisplayStep;
            Parallel.ForEach(data, 
            new ParallelOptions()
            {
                MaxDegreeOfParallelism = threads.Value
            },
            () =>
            {
                var proxy = ProxiesPool.GetProxy();
                return new
                {
                    Proxy = proxy
                };
            },
            (item, loopState, context) =>
            {
                var jobExecutionContext = new JobExecutionContext(context.Proxy, item);
                jobExecutionContext.PushMetrics(base.ContextProperties);

                // Increment progress index
                Interlocked.Increment(ref processedItemCount);

                // Increment progress bar every x records
                if (processedItemCount % progressDisplayStep == 0)
                {
                    Logger.LogInformation($"Processing record {processedItemCount} / {dataCount}", jobExecutionContext.DumpMetrics());
                }

                // Exit if record has already been processed
                if (processedItems.Contains(item.Id.ToString()))
                {
                    return context;
                }

                try
                {
                    ProcessRecord(jobExecutionContext);
                    Logger.LogSuccess("Record processed with success!", jobExecutionContext.DumpMetrics());

                    // Track job progress
                    progressWriter.Write(item.Id.ToString());
                }
                catch (FaultException<OrganizationServiceFault> faultException)
                {
                    var properties = jobExecutionContext.DumpMetrics().MergeWith(faultException.ExportProperties());
                    Logger.LogFailure(faultException, properties);
                }
                catch (Exception ex)
                {
                    Logger.LogFailure(ex, jobExecutionContext.DumpMetrics());
                }

                return context;
            },
            (context) =>
            {
                context.Proxy.Dispose();
            });

            stopwatch.Stop();
            var speed = Utilities.GetSpeed(stopwatch.Elapsed.TotalMilliseconds, results.Entities.Count);
            Logger.LogInformation($"{dataCount} records processed in {stopwatch.Elapsed.TotalSeconds} => {stopwatch.Elapsed:g} [Speed = {speed}]!", base.ContextProperties);

            if (File.Exists(ProgressFilePath))
            {
                File.Delete(ProgressFilePath);
                Logger.LogInformation($"Progress file {ProgressFilePath} removed!", base.ContextProperties);
            }

            return true;
        }
    }
}
