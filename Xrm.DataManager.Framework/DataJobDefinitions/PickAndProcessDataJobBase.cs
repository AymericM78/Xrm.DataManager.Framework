
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Diagnostics;
using System.ServiceModel;
using System.Threading;
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
        /// <param name="context"></param>
        public abstract void ProcessRecord(JobExecutionContext context);

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

            var records = ProxiesPool.MainProxy.RetrieveMultiple(query).Entities;
            var entityName = query.EntityName;
            var startTime = DateTime.Now;

            // Initialize last result count to prevent infinite loop
            int lastRunCount = JobSettings.QueryRecordLimit;
            var threads = (this.OverrideThreadNumber.HasValue) ? this.OverrideThreadNumber : JobSettings.ThreadNumber;

            int totalProcessed = 0;
            int totalSuccess = 0;
            int totalFailures = 0;

            while (records.Count > 0)
            {
                var stopwatch = Stopwatch.StartNew();
                Logger.LogInformation($"Retrieved {records.Count} records from CRM (Entity : {entityName})");

                int currentProcessed = 0;
                int currentSuccess = 0;
                int currentFailures = 0;

                Parallel.ForEach(
                    records,
                    new ParallelOptions() { MaxDegreeOfParallelism = threads.Value },
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

                        try
                        {
                            Interlocked.Increment(ref totalProcessed);
                            Interlocked.Increment(ref currentProcessed);

                            ProcessRecord(jobExecutionContext);

                            Interlocked.Increment(ref totalSuccess);
                            Interlocked.Increment(ref currentSuccess);
                            Logger.LogSuccess($"Record processed with success! (Entity : {entityName})", jobExecutionContext.DumpMetrics());
                        }
                        catch (FaultException<OrganizationServiceFault> faultException)
                        {
                            var properties = jobExecutionContext.DumpMetrics().MergeWith(faultException.ExportProperties());
                            Interlocked.Increment(ref totalFailures);
                            Interlocked.Increment(ref currentFailures);
                            Logger.LogFailure(faultException, properties);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref totalFailures);
                            Interlocked.Increment(ref currentFailures);
                            Logger.LogFailure(ex, jobExecutionContext.DumpMetrics());
                        }
                        return context;
                    },
                    (context) =>
                    {
                        context.Proxy.Dispose();
                    }
                );

                stopwatch.Stop();
                var speed = Utilities.GetSpeed(stopwatch.Elapsed.TotalMilliseconds, records.Count);
                Logger.LogInformation($"{currentProcessed} records (Entity : {entityName}) processed in {stopwatch.Elapsed.TotalSeconds} => {stopwatch.Elapsed:g} [Speed = {speed} | Success = {currentSuccess} | Failures = {currentFailures}]!");

                var duration = (DateTime.Now - startTime);
                var globalSpeed = Utilities.GetSpeed(duration.TotalMilliseconds, totalProcessed);
                Logger.LogInformation($"Total = {totalProcessed} records processed (Entity : {entityName}) in {duration:g}! [Speed = {globalSpeed} | Success = {totalSuccess} | Failures = {totalFailures}]");

                // If we have the same number of record processed in this round than the previous one, 
                // that mean that we don't need to continue
                if (lastRunCount < JobSettings.QueryRecordLimit && lastRunCount == records.Count)
                {
                    Logger.LogInformation($"Operation completed! (Entity : {entityName} | Reason: Infinite loop detected)");
                    return false;
                }

                // If job duration is greater or equal to execution limit, we can stop the process
                if (duration.TotalHours >= JobSettings.MaxRunDurationInHour)
                {
                    Logger.LogInformation($"Operation completed! (Entity : {entityName} | Reason: Max duration reached)");
                    return false;
                }

                // If we have only errors, we must stop
                if (currentFailures == records.Count)
                {
                    Logger.LogInformation($"Operation failed! (Entity : {entityName} | Reason: Too many errors detected)");
                    return false;
                }

                lastRunCount = records.Count;

                // Retrieve records for next round
                records = ProxiesPool.MainProxy.RetrieveMultiple(query).Entities;
            }

            // If the query return nothing, we have finished!
            if (records.Count == 0)
            {
                Logger.LogInformation($"Operation completed! (Entity : {entityName} | Reason: No more data to process)");
                return true;
            }

            return false;
        }
    }
}
