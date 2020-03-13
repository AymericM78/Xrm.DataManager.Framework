
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Xrm.DataManager.Framework
{
    public abstract class InputFileProcessDataJobBase : DataJobBase
    {
        const string PivotUniqueMarker = "#PVT-TAG#";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="jobSettings"></param>
        public InputFileProcessDataJobBase(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters)
        {
        }

        /// <summary>
        /// Get source file path
        /// </summary>
        /// <returns></returns>
        public virtual string GetInputFilePath() => throw new NotImplementedException();

        public virtual string GetPivotFilePath()
        {
            string fileName = $"{this.GetType().Name}_Pivot.txt";
            return Path.Combine(Environment.CurrentDirectory, fileName);
        }

        public virtual char[] GetInputFileSeparator() => new[] { ',', ';' };

        public virtual void ProcessRecord(ManagedTokenOrganizationServiceProxy proxy, Entity record, string[] lineData) => throw new NotImplementedException();

        public virtual Entity SearchRecord(ManagedTokenOrganizationServiceProxy proxy, string[] lineData) => throw new NotImplementedException();

        /// <summary>
        /// Run the job
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public override bool Run()
        {
            var jobName = GetName();
            var defaultFileSeparator = GetInputFileSeparator().First();

            List<string> lines = new List<string>();
            // Check if pivot file exists
            if (File.Exists(GetPivotFilePath()))
            {
                var fileLines = File.ReadAllLines(GetPivotFilePath());
                lines = fileLines.ToList();

                Logger.LogMessage($"Retrieved {lines.Count} from file {GetPivotFilePath()}");
            }
            else
            {
                // Load file content
                var fileLines = File.ReadAllLines(GetInputFilePath());
                lines = fileLines.ToList();

                Logger.LogMessage($"Retrieved {lines.Count} from file {GetInputFilePath()}");

                // Create pivot file that track progress and outcome
                var header = lines.First();
                var pivotLines = new List<string>()
                {
                    string.Concat(header, defaultFileSeparator, PivotUniqueMarker, defaultFileSeparator, "RecordId", defaultFileSeparator, "Outcome", defaultFileSeparator, "Details")
                };
                File.WriteAllLines(GetPivotFilePath(), pivotLines, Encoding.UTF8);
            }
            var pivotFileWriter = new MultiThreadFileWriter(GetPivotFilePath());

            var processedItemCount = 0;
            var stopwatch = Stopwatch.StartNew();

            var threads = (this.OverrideThreadNumber.HasValue) ? this.OverrideThreadNumber : JobSettings.ThreadNumber;

            var linesToProcess = lines.Skip(1);

            Parallel.ForEach(
                linesToProcess,
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

                    var isPivotLine = item.Contains(PivotUniqueMarker);
                    if (isPivotLine)
                    {
                        // TODO : Handle already processed pivot lines to replay errors
                        return context;
                    }
                    Entity record = null;
                    try
                    {
                        var lineData = item.Split(GetInputFileSeparator(), StringSplitOptions.RemoveEmptyEntries);

                        // Retrieve CRM record based on current line
                        record = SearchRecord(context.Proxy, lineData);
                        ProcessRecord(context.Proxy, record, lineData);
                        Logger.LogSuccess("Record processed with success!", record, jobName);

                        // Track progress and outcome
                        var pivotLine = string.Concat(item,
                        defaultFileSeparator, PivotUniqueMarker,
                        defaultFileSeparator, record.Id.ToString() /* RecordId */,
                        defaultFileSeparator, "OK" /* Outcome */,
                        defaultFileSeparator, "Success" /*Details */);
                        pivotFileWriter.Write(pivotLine);

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
                        Logger.LogFailure(faultException, record, jobName);

                        // Track progress and outcome
                        var pivotLine = string.Concat(item,
                        defaultFileSeparator, PivotUniqueMarker,
                        defaultFileSeparator, record?.Id.ToString() /* RecordId */,
                        defaultFileSeparator, "KO" /* Outcome */,
                        defaultFileSeparator, faultException.Message /*Details */);
                        pivotFileWriter.Write(pivotLine);

                    }
                    catch (Exception ex)
                    {
                        Logger.LogFailure(ex, record, jobName);

                        // Track progress and outcome
                        var pivotLine = string.Concat(item,
                        defaultFileSeparator, PivotUniqueMarker,
                        defaultFileSeparator, record?.Id.ToString() /* RecordId */,
                        defaultFileSeparator, "KO" /* Outcome */,
                        defaultFileSeparator, ex.Message /*Details */);
                        pivotFileWriter.Write(pivotLine);
                    }

                    return context;
                },
                (context) =>
                {
                    context.Proxy.Dispose();
                });
            stopwatch.Stop();
            var speed = Utilities.GetSpeed(stopwatch.Elapsed.TotalMilliseconds, lines.Count);
            Logger.LogMessage($"{lines.Count} records processed in {stopwatch.Elapsed.TotalSeconds} => {stopwatch.Elapsed.ToString("g")} [Speed = {speed}]!");

            return true;
        }
    }
}
