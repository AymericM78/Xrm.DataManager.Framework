
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
        /// Apply record modification
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="record"></param>
        public virtual void ProcessRecord(ManagedTokenOrganizationServiceProxy proxy, Entity record) => throw new NotImplementedException();

        /// <summary>
        /// Run the job
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public override bool Run()
        {
            var progressWriter = new MultiThreadFileWriter(ProgressFilePath);

            Logger.LogMessage($"Checking {ProgressFilePath} existence...");
            // Load already processed items from tracking file if exists
            var processedItems = new List<string>();
            if (File.Exists(ProgressFilePath))
            {
                Logger.LogMessage($"File {ProgressFilePath} detected! Continue process at it last state");

                var lines = File.ReadAllLines(ProgressFilePath);
                processedItems = lines.ToList();
            }
            else
            {
                Logger.LogMessage($"File {ProgressFilePath} not detected! Start process from 0");
            }

            var jobName = GetName();
            var query = GetQuery(CallerId);
            query.PageInfo.Count = JobSettings.QueryRecordLimit;
            query.NoLock = true;

            var results = Utilities.TryRetrieveAll(ProxiesPool, Logger, query);
            Logger.LogMessage($"Retrieved {results.Entities.Count} records from CRM");
            var processedItemCount = 0;
            var stopwatch = Stopwatch.StartNew();
            var data = PrepareData(results.Entities);

            var threads = (this.OverrideThreadNumber.HasValue) ? this.OverrideThreadNumber : JobSettings.ThreadNumber;

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
                // Increment progress index
                Interlocked.Increment(ref processedItemCount);

                // Increment progress bar every 50 records
                if (processedItemCount % 50 == 0)
                {
                    Logger.LogMessage($"Processing record {processedItemCount} / {results.Entities.Count}");
                }

                // Exit if record has already been processed
                if (processedItems.Contains(item.Id.ToString()))
                {
                    return context;
                }

                try
                {
                    ProcessRecord(context.Proxy, item);
                    Logger.LogSuccess("Record processed with success!", item, jobName);

                    // Track job progress
                    progressWriter.Write(item.Id.ToString());
                }
                catch (FaultException<OrganizationServiceFault> faultException)
                {
                    var exceptionDetails = new Dictionary<string, string>
                    {
                        { "Crm Exception : Activity Id", faultException.Detail.ActivityId.ToString() },
                        { "Crm Exception : Error Code", faultException.Detail.ErrorCode.ToString() },
                        { "Crm Exception : Message", faultException.Detail.Message?.ToString() },
                        { "Crm Exception : Timestamp", faultException.Detail.Timestamp.ToString() },
                        { "Crm Exception : Trace Text", faultException.Detail.TraceText?.ToString() }
                    };
                    Logger.LogException(faultException, exceptionDetails);
                    Logger.LogFailure(faultException, item, jobName);
                }
                catch (Exception ex)
                {
                    Logger.LogFailure(ex, item, jobName);
                }

                return context;
            },
            (context) =>
            {
                context.Proxy.Dispose();
            });

            stopwatch.Stop();
            var speed = Utilities.GetSpeed(stopwatch.Elapsed.TotalMilliseconds, results.Entities.Count);
            Logger.LogMessage($"{results.Entities.Count} records processed in {stopwatch.Elapsed.TotalSeconds} => {stopwatch.Elapsed.ToString("g")} [Speed = {speed}]!");

            if (File.Exists(ProgressFilePath))
            {
                File.Delete(ProgressFilePath);
                Logger.LogMessage($"Progress file {ProgressFilePath} removed!");
            }

            return true;
        }
    }
}
