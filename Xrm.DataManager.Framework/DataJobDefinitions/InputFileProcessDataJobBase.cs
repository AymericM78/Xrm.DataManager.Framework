
using Microsoft.Xrm.Sdk;
using ShellProgressBar;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            var options = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Yellow,
                ForegroundColorDone = ConsoleColor.DarkGreen,
                BackgroundColor = ConsoleColor.DarkGray,
                BackgroundCharacter = '\u2593'
            };

            var threads = (this.OverrideThreadNumber.HasValue) ? this.OverrideThreadNumber : JobSettings.ThreadNumber;
            using (var pbar = new ProgressBar(lines.Count, "Processing CRM records", options))
            {
                var linesToProcess = lines.Skip(1);
                // Parallel processing 
                Parallel.ForEach(
                    linesToProcess, //these are your items to process - should be many many thousands in here 
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
                        // Increment progress index
                        Interlocked.Increment(ref processedItemCount);

                        // Increment progress bar every 50 records
                        if (processedItemCount % 50 == 0)
                        {
                            pbar.Tick(processedItemCount, $"Processing record {processedItemCount} / {lines.Count}");
                        }
                        // partition body - put the 'guts' of your operation in here 
                        // ensure this method is one-off and all it's own 'thing' and doesn't share resources 

                        // any and all current or downstream logging *must* be threadsafe and multi-thread optimized 
                        // use appinsights or ent lib to log so that it doesn't block any other threads 
                        // if you hit thread contention in logging it will slow down your execution greatly 

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
                            record = SearchRecord(context.threadLocalProxy, lineData);
                            ProcessRecord(context.threadLocalProxy, record, lineData);
                            Logger.LogSuccess("Record processed with success!", record, jobName);

                            // Track progress and outcome
                            var pivotLine = string.Concat(item,
                                defaultFileSeparator, PivotUniqueMarker,
                                defaultFileSeparator, record.Id.ToString() /* RecordId */,
                                defaultFileSeparator, "OK" /* Outcome */,
                                defaultFileSeparator, "Success" /*Details */);
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

                        // return the context to be re-used or to be 'closed' 
                        return context;
                    },
                    (context) =>
                    {
                        // final method per parition / task
                        // this is only called when the thread partition is being shut down / closed / completed
                        context.threadLocalProxy.Dispose();
                    });
                pbar.Tick(lines.Count, $"Processing record {lines.Count} / {lines.Count}");
            }
            stopwatch.Stop();
            var speed = Utilities.GetSpeed(stopwatch.Elapsed.TotalMilliseconds, lines.Count);
            Logger.LogMessage($"{lines.Count} records processed in {stopwatch.Elapsed.TotalSeconds} => {stopwatch.Elapsed.ToString("g")} [Speed = {speed}]!");

            return true;
        }
    }
}
