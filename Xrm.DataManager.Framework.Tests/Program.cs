using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xrm.DataManager.Framework.Tests
{
    class Program
    {
        /// <summary>
        /// Application Insights logger is provided by Xrm.DataManager.Framework.ApplicationInsights package
        /// </summary>
        private static ILogger CreateLogger(JobSettings settings)
        {
            if (settings.ApplicationInsightsEnabled)
            {
                return new ApplicationInsightLogger(settings);
            }
            return new FileLogger(settings);
        }

        private static void RunJob<T>() where T : DataJobBase
        {
            // Dispose closes Dataverse connections
            using (var jobProcessor = new JobProcessor(CreateLogger))
            {
                jobProcessor.Execute<T>();
            }
        }

        static void Main(string[] args)
        {
            RunJob<CancelAsyncTasksDataJob>();
            RunJob<RemoveAsyncTasksDataJob>();
            RunJob<RemovePluginTracesDataJob>();

            //var process = new Processor(CreateLogger);
            //process.Execute(args);

            //var process = new CustomJobProcessor();
            //process.Execute();
        }
    }
}
