using System;
using System.Collections.Generic;
using System.Linq;

namespace Xrm.DataManager.Framework
{
    public class Processor : ProcessorBase
    {
        /// <summary>
        /// Run job execution
        /// </summary>
        public void Execute(string[] args)
        {
            var instances = InstancePicker.GetInstances();
            Instance selectedInstance = null;
            if (instances == null)
            {
                if (!JobSettings.ConnectionStringDefined)
                {
                    throw new Exception("Connection string is not defined!");
                }
                InitializeOrganizationServiceManager(JobSettings.CrmConnectionString);
            }
            else
            {
                if (JobSettings.CrmInstanceNameDefined)
                {
                    // Load instance from config file
                    selectedInstance = instances.FirstOrDefault(i => i.Name == JobSettings.CrmInstanceName);
                    if (selectedInstance == null)
                    {
                        throw new Exception($"Instance uniquename '{JobSettings.CrmInstanceName}' doesn't match known instances list!");
                    }
                }
                else
                {
                    // Display instances selector           
                    selectedInstance = ConsoleHelper.InstanceSelect(instances);
                }

                if (selectedInstance == null)
                {
                    if (args.Length != 2)
                    {
                        throw new Exception("Usage : YourApp.exe 'InstanceName' 'Job1,Job2'\r\nExample : YourApp.exe 'dynamicsinstance1' 'PluginTraceDeleteDataJob'");
                    }
                    else
                    {
                        JobSettings.SelectedInstanceName = args[0];
                        JobSettings.Jobs = args[1];

                        selectedInstance = instances.FirstOrDefault(i => i.Name == JobSettings.SelectedInstanceName);
                    }
                }

                if (selectedInstance == null)
                {
                    throw new Exception("Instance not specified!");
                }
                InitializeOrganizationServiceManager(selectedInstance);
            }

            var parameters = new JobProcessParameters { ProxiesPool = ProxiesPool, Logger = Logger, CallerId = CallerId };

            var selectDataJobs = new List<DataJobBase>();
            var dataJobs = DataJobPicker.GetDataJobs(JobSettings, parameters);

            if (JobSettings.JobNamesDefined)
            {
                JobSettings.Jobs = JobSettings.JobNames;
            }

            if (!string.IsNullOrEmpty(JobSettings.Jobs))
            {
                var jobNames = JobSettings.Jobs.Split(',');
                foreach (var jobName in jobNames)
                {
                    var job = dataJobs.FirstOrDefault(j => j.GetType().Name == jobName);
                    if (job == null)
                    {
                        throw new Exception("Specified job is invalid!");
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
                throw ex;
            }
            finally
            {
                try
                {
                    ProxiesPool.MainProxy.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to dispose MainProxy: {ex.Message}!");
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
