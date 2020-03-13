
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
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
                    data,
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
                        try
                        {
                            ProcessRecord(context.Proxy, item);
                            Logger.LogSuccess("Record processed with success!", item, jobName);
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
                    }
                );

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
                data = PrepareData(results.Entities);
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
