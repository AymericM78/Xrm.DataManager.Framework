using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Xrm.DataManager.Framework
{
    public static class DataJobPicker
    {
        /// <summary>
        /// Retrieve all datajob class instances with IsEnabled = true
        /// </summary>
        /// <param name="jobSettings"></param>
        /// <returns></returns>
        public static List<DataJobBase> GetDataJobs(JobSettings jobSettings, JobProcessParameters parameters)
        {
            var dataJobs = new List<DataJobBase>();
            var dataJobClasses = Assembly.GetEntryAssembly().GetTypes().Where(t => typeof(DataJobBase).IsAssignableFrom(t) && !t.IsAbstract);

            foreach (var dataJobClass in dataJobClasses)
            {
                var constructor = dataJobClass.GetConstructor(new Type[] { typeof(JobSettings), typeof(JobProcessParameters) });
                var dataJobInstance = constructor?.Invoke(new object[] { jobSettings, parameters }) as DataJobBase;
                if (dataJobInstance?.IsEnabled == true)
                {
                    dataJobs.Add(dataJobInstance);
                }
            }
            return dataJobs;
        }
    }
}
