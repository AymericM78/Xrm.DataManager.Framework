
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
            var startTime = DateTime.Now;
            int totalProcessed = 0;

            // Initialize last result count to prevent infinite loop
            int lastRunCount = JobSettings.QueryRecordLimit;
            var threads = (this.OverrideThreadNumber.HasValue) ? this.OverrideThreadNumber : JobSettings.ThreadNumber;
            while (records.Count > 0)
            {
                var stopwatch = Stopwatch.StartNew();
                Logger.LogInformation($"Retrieved {records.Count} records from CRM");

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
                            ProcessRecord(jobExecutionContext);

                            Logger.LogSuccess("Record processed with success!", jobExecutionContext.DumpMetrics());
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
                    }
                );

                stopwatch.Stop();
                var speed = Utilities.GetSpeed(stopwatch.Elapsed.TotalMilliseconds, records.Count);
                Logger.LogInformation($"{records.Count} records processed in {stopwatch.Elapsed.TotalSeconds} => {stopwatch.Elapsed.ToString("g")} [Speed = {speed}]!");

                totalProcessed += records.Count;
                var duration = (DateTime.Now - startTime);
                Logger.LogInformation($"Total = {totalProcessed} records processed in {duration.ToString("g")}!");

                // If we have the same number of record processed in this round than the previous one, 
                // that mean that we don't need to continue
                if (lastRunCount < JobSettings.QueryRecordLimit && lastRunCount == records.Count)
                {
                    Logger.LogInformation("Operation completed! (Reason: Infinite loop detected)");
                    return true;
                }

                // If job duration is greater or equal to execution limit, we can stop the process
                if (duration.TotalHours >= JobSettings.MaxRunDurationInHour)
                {
                    Logger.LogInformation("Operation completed! (Reason: Max duration reached)");
                    return true;
                }

                lastRunCount = records.Count;

                // Retrieve records for next round
                records = ProxiesPool.MainProxy.RetrieveMultiple(query).Entities;
            }

            // If the query return nothing, we have finished!
            if (records.Count == 0)
            {
                Logger.LogInformation("Operation completed! (Reason: No more data to process)");
                return true;
            }

            return false;
        }
    }
}
