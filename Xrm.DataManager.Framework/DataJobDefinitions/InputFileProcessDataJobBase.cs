
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

        public virtual void ProcessRecord(JobExecutionContext context, string[] lineData) => throw new NotImplementedException();

        public virtual Entity SearchRecord(IManagedTokenOrganizationServiceProxy proxy, string[] lineData) => throw new NotImplementedException();

        /// <summary>
        /// Run the job
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public override bool Run()
        {
            var jobName = GetName();
            base.ContextProperties.Add("JobName", jobName);
            var defaultFileSeparator = GetInputFileSeparator().First();

            List<string> lines = new List<string>();
            // Check if pivot file exists
            if (File.Exists(GetPivotFilePath()))
            {
                var fileLines = File.ReadAllLines(GetPivotFilePath());
                lines = fileLines.ToList();

                Logger.LogInformation($"Retrieved {lines.Count} from file {GetPivotFilePath()}");
            }
            else
            {
                // Load file content
                var fileLines = File.ReadAllLines(GetInputFilePath());
                lines = fileLines.ToList();

                Logger.LogInformation($"Retrieved {lines.Count} from file {GetInputFilePath()}");

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
                (line, loopState, context) =>
                {
                    var jobExecutionContext = new JobExecutionContext(context.Proxy);
                    jobExecutionContext.PushMetrics(base.ContextProperties);

                    // Increment progress index
                    Interlocked.Increment(ref processedItemCount);

                    var isPivotLine = line.Contains(PivotUniqueMarker);
                    if (isPivotLine)
                    {
                        // TODO : Handle already processed pivot lines to replay errors
                        return context;
                    }
                    Entity record = null;
                    try
                    {
                        var lineData = line.Split(GetInputFileSeparator(), StringSplitOptions.RemoveEmptyEntries);

                        // Retrieve CRM record based on current line
                        record = SearchRecord(context.Proxy, lineData);
                        jobExecutionContext.PushRecordToMetrics(record);
                        ProcessRecord(jobExecutionContext, lineData);
                        Logger.LogSuccess("Record processed with success!", jobExecutionContext.DumpMetrics());

                        // Track progress and outcome
                        var pivotLine = string.Concat(line,
                        defaultFileSeparator, PivotUniqueMarker,
                        defaultFileSeparator, record.Id.ToString() /* RecordId */,
                        defaultFileSeparator, "OK" /* Outcome */,
                        defaultFileSeparator, "Success" /*Details */);
                        pivotFileWriter.Write(pivotLine);

                    }
                    catch (FaultException<OrganizationServiceFault> faultException)
                    {
                        var properties = jobExecutionContext.DumpMetrics().MergeWith(faultException.ExportProperties());
                        Logger.LogFailure(faultException, properties);

                        // Track progress and outcome
                        var pivotLine = string.Concat(line,
                        defaultFileSeparator, PivotUniqueMarker,
                        defaultFileSeparator, record?.Id.ToString() /* RecordId */,
                        defaultFileSeparator, "KO" /* Outcome */,
                        defaultFileSeparator, faultException.Message /*Details */);
                        pivotFileWriter.Write(pivotLine);

                    }
                    catch (Exception ex)
                    {
                        Logger.LogFailure(ex, jobExecutionContext.DumpMetrics());

                        // Track progress and outcome
                        var pivotLine = string.Concat(line,
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
            Logger.LogInformation($"{lines.Count} records processed in {stopwatch.Elapsed.TotalSeconds} => {stopwatch.Elapsed.ToString("g")} [Speed = {speed}]!");

            return true;
        }
    }
}
