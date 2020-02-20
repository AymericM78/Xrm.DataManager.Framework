
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using ShellProgressBar;
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

            var options = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Yellow,
                ForegroundColorDone = ConsoleColor.DarkGreen,
                BackgroundColor = ConsoleColor.DarkGray,
                BackgroundCharacter = '\u2593'
            };

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
#if DEBUG
            using (var pbar = new ProgressBar(results.Entities.Count, "Processing CRM records", options))
            {
#endif
                Parallel.ForEach(data, //these are your items to process - should be many many thousands in here 
                new ParallelOptions()
                {
                    MaxDegreeOfParallelism = threads.Value
                },
                () =>
                {
                    // partition initialize // localInit - called once per Task.
                    // get the proxy to use in the thread parition - this creates ONE proxy "per thread"
                    // that proxy is then re-used inside of that ONE thread 
                    var threadLocalProxy = ProxiesPool.GetProxy();

                    // Re-set CallerId as the value is not defined by default
                    threadLocalProxy.CallerId = CallerId;

                    // you can log thread parition being opened/created 
                    // HOWEVER use appinsights or something like ent lib for threadsafety
                    // do not log to text otherwise it *will* slow you down a lot 

                    // return the context so the thread Body can use the context 
                    return new
                    {
                        threadLocalProxy
                    };
                },
                (item, loopState, context) =>
                {
                    // partition body - put the 'guts' of your operation in here 
                    // ensure this method is one-off and all it's own 'thing' and doesn't share resources 

                    // Increment progress index
                    Interlocked.Increment(ref processedItemCount);

                    // Increment progress bar every 50 records
                    if (processedItemCount % 50 == 0)
                    {
                        Logger.LogMessage($"Processing record {processedItemCount} / {results.Entities.Count}");
#if DEBUG
                        pbar.Tick(processedItemCount, $"Processing record {processedItemCount} / {results.Entities.Count}");
#endif
                    }

                    // Exit if record has already been processed
                    if (processedItems.Contains(item.Id.ToString()))
                    {
                        return context;
                    }

                    // any and all current or downstream logging *must* be threadsafe and multi-thread optimized 
                    // use appinsights or ent lib to log so that it doesn't block any other threads 
                    // if you hit thread contention in logging it will slow down your execution greatly 
                    try
                    {
                        ProcessRecord(context.threadLocalProxy, item);
                        Logger.LogSuccess("Record processed with success!", item, jobName);

                        // Track job progress
                        progressWriter.Write(item.Id.ToString());
                    }
                    catch (FaultException<OrganizationServiceFault> e) when (TransientIssueManager.IsTransientError(e))
                    {
                        TransientIssueManager.ApplyDelay(e, Logger);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogFailure(ex, item, jobName);
                    }

                    // return the context to be re-used or to be 'closed' 
                    return context;
                },
                (context) =>
                {
                    // final method per parition / task
                    // this is only called when the thread partition is being shut down / closed / completed
                    context.threadLocalProxy.Dispose();
                });
#if DEBUG
                pbar.Tick(processedItemCount, $"{processedItemCount} / {results.Entities.Count} records processed!");
#endif
#if DEBUG
            }
#endif
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
