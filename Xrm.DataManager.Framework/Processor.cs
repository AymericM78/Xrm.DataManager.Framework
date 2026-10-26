using System;
using System.Collections.Generic;
using System.Linq;

namespace Xrm.DataManager.Framework
{
    public class Processor : ProcessorBase
    {
        /// <summary>
        /// Constructor with default logger (FileLogger)
        /// </summary>
        public Processor()
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="loggerFactory">ex : settings => new ApplicationInsightLogger(settings)</param>
        public Processor(Func<JobSettings, ILogger> loggerFactory) : base(loggerFactory)
        {
        }

        /// <summary>
        /// Run job execution
        /// Optional arguments : YourApp.exe 'ConnectionStringName' 'Job1,Job2'
        /// Example : YourApp.exe 'Dev' 'PluginTraceDeleteDataJob'
        /// </summary>
        public void Execute(string[] args)
        {
            if (args?.Length > 0)
            {
                // Target connectionStrings entry name
                JobSettings.SelectedConnectionName = args[0];
            }
            InitializeOrganizationServiceManager();

            var parameters = CreateJobProcessParameters();

            var selectDataJobs = new List<DataJobBase>();
            var dataJobs = DataJobPicker.GetDataJobs(JobSettings, parameters);

            if (JobSettings.TargetJobNamesDefined)
            {
                JobSettings.Jobs = JobSettings.TargetJobNames;
            }
            if (args?.Length > 1)
            {
                JobSettings.Jobs = args[1];
            }

            if (!string.IsNullOrEmpty(JobSettings.Jobs))
            {
                var jobNames = JobSettings.Jobs.Split(',');
                foreach (var jobName in jobNames)
                {
                    var job = dataJobs.FirstOrDefault(j => j.GetType().Name == jobName.Trim());
                    if (job == null)
                    {
                        throw new Exception($"Specified job '{jobName}' is invalid!");
                    }
                    selectDataJobs.Add(job);
                }
            }
            else
            {
                // Display data jobs selector
                DataJobBase selectedDataJob = ConsoleHelper.DataJobSelect(dataJobs);
                if (selectedDataJob == null)
                {
                    throw new Exception("Specified job is invalid!");
                }
                selectDataJobs.Add(selectedDataJob);
            }

            try
            {
                foreach (var job in selectDataJobs)
                {
                    RunJob(job);
                }
            }
            catch (Exception ex)
            {
                Logger.LogFailure(ex);
                Console.WriteLine($"Catastrophic error {ex.Message}!");
                throw;
            }
            finally
            {
                Logger.Flush();
                try
                {
                    Connections.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to dispose connections: {ex.Message}!");
                }
            }

#if DEBUG
            Console.WriteLine("Operation completed!");
            Console.WriteLine("Press any key to exit...");
            Console.Read();
#endif
        }
    }
}
