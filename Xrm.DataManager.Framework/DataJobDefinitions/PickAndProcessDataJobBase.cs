
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Xrm.DataManager.Framework
{
    public abstract class PickAndProcessDataJobBase : DataJobBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="jobSettings"></param>
        public PickAndProcessDataJobBase(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters)
        {
        }

        /// <summary>
        /// Define QueryExpression to retrieve record collection that should be processed
        /// </summary>
        /// <param name="callerId"></param>
        /// <returns></returns>
        public abstract QueryExpression GetQuery(Guid callerId);

        /// <summary>
        /// Apply record modification
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="record"></param>
        public abstract void ProcessRecord(ManagedTokenOrganizationServiceProxy proxy, Entity record);

        /// <summary>
        /// Run the job
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public override bool Run()
        {
            var jobName = GetName();
            var query = GetQuery(CallerId);
            query.TopCount = JobSettings.QueryRecordLimit;
            query.NoLock = true;
            query.PageInfo = null;

            var results = Utilities.TryRetrieveMultiple(ProxiesPool, Logger, query);
            DateTime startTime = DateTime.Now;
            int totalProcessed = 0;

            var data = PrepareData(results.Entities);

            // Initialize last result count to prevent infinite loop
            int lastRunCount = JobSettings.QueryRecordLimit;
            var threads = (this.OverrideThreadNumber.HasValue) ? this.OverrideThreadNumber : JobSettings.ThreadNumber;
            while (data.Count() > 0)
            {
                var stopwatch = Stopwatch.StartNew();
                Logger.LogMessage($"Retrieved {results.Entities.Count} records from CRM");

                Parallel.ForEach(
                data, //these are your items to process - should be many many thousands in here 
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

                    // any and all current or downstream logging *must* be threadsafe and multi-thread optimized 
                    // use appinsights or ent lib to log so that it doesn't block any other threads 
                    // if you hit thread contention in logging it will slow down your execution greatly 
                    try
                    {
                        ProcessRecord(context.threadLocalProxy, item);
                        Logger.LogSuccess("Record processed with success!", item, jobName);
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

                stopwatch.Stop();
                var speed = Utilities.GetSpeed(stopwatch.Elapsed.TotalMilliseconds, results.Entities.Count);
                Logger.LogMessage($"{results.Entities.Count} records processed in {stopwatch.Elapsed.TotalSeconds} => {stopwatch.Elapsed.ToString("g")} [Speed = {speed}]!");

                totalProcessed += results.Entities.Count;
                var duration = (DateTime.Now - startTime);
                Logger.LogMessage($"Total = {totalProcessed} records processed in {duration.ToString("g")}!");

                // If we have the same number of record processed in this round than the previous one, 
                // that mean that we don't need to continue
                if (lastRunCount < JobSettings.QueryRecordLimit && lastRunCount == results.Entities.Count)
                {
                    Logger.LogMessage("Operation completed! (Reason: Infinite loop detected)");
                    return true;
                }

                // If job duration is greater or equal to execution limit, we can stop the process
                if (duration.TotalHours >= JobSettings.MaxRunDurationInHour)
                {
                    Logger.LogMessage("Operation completed! (Reason: Max duration reached)");
                    return true;
                }

                lastRunCount = results.Entities.Count;

                // Retrieve records for next round
                results = Utilities.TryRetrieveMultiple(ProxiesPool, Logger, query);
            }

            // If the query return nothing, we have finished!
            if (results.Entities.Count == 0)
            {
                Logger.LogMessage("Operation completed! (Reason: No more data to process)");
                return true;
            }

            return false;
        }
    }
}
