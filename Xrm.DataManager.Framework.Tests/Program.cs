using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xrm.DataManager.Framework.Tests
{
    class Program
    {
        private static void RunJob<T>()
        {
            var jobProcessor = new JobProcessor();

            var method = typeof(JobProcessor).GetMethod("Execute");
            var constructedMethod = method.MakeGenericMethod(typeof(T));
            constructedMethod.Invoke(jobProcessor, null);
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
