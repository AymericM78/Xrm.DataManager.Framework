using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xrm.DataManager.Framework.Tests
{
    class Program
    {
        private static void RunJob<T>() where T : DataJobBase
        {
            // Dispose closes Dataverse connections
            using (var jobProcessor = new JobProcessor())
            {
                jobProcessor.Execute<T>();
            }
        }

        static void Main(string[] args)
        {
            RunJob<CancelAsyncTasksDataJob>();
            RunJob<RemoveAsyncTasksDataJob>();
            RunJob<RemovePluginTracesDataJob>();

            //var process = new Processor();
            //process.Execute(args);

            //var process = new CustomJobProcessor();
            //process.Execute();
        }
    }
}
