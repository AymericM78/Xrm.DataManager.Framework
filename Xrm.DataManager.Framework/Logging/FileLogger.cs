using Microsoft.Xrm.Sdk;
using Serilog;
using System;
using System.Collections.Generic;

namespace Xrm.DataManager.Framework
{
    public class FileLogger : ILogger
    {
        protected JobSettings JobSettings;
        protected string RunId;

        /// <summary>
        /// Constructor
        /// </summary>
        public FileLogger(JobSettings jobSettings, string runId)
        {
            JobSettings = jobSettings;
            RunId = runId;

            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(Serilog.Events.LogEventLevel.Verbose)
                .WriteTo.File($"{RunId}.log");

            Log.Logger = loggerConfiguration.CreateLogger();
        }

        /// <summary>
        /// Output information to console
        /// </summary>
        public void LogMessage(string message, string jobName = null)
        {
            if (jobName != null)
            {
                message = $"{jobName} - {message}";
            }
            Log.Information(message);
        }

        /// <summary>
        /// Log message to console
        /// </summary>
        /// <param name="message"></param>
        public void LogDebug(string message) => Log.Debug(message);

        /// <summary>
        /// Log custom event to App Insights
        /// </summary>
        /// <param name="name"></param>
        /// <param name="properties"></param>
        /// <param name="jobName"></param>
        public void LogEvent(string name, Dictionary<string, string> properties, string jobName = null)
        {
            var defaultProperties = Utilities.GetContextProperties(JobSettings, RunId);
            foreach (var property in defaultProperties)
            {
                properties.Add(property.Key, property.Value);
            }
            if (jobName != null)
            {
                properties.Add("Job Name", jobName);
            }

            Log.Verbose(name, properties);
        }

        /// <summary>
        /// Log event with record details
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="crmRecord"></param>
        /// <param name="jobName"></param>
        public void LogSuccess(string eventName, Entity crmRecord, string jobName)
        {
            var recordData = new Dictionary<string, string>()
            {
                { "Record : Crm Record Id", crmRecord.Id.ToString("D") },
                { "Record : Crm Record Type", crmRecord.LogicalName }
            };

            LogEvent(eventName, recordData, jobName);
        }

        /// <summary>
        /// Log exception with record details
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="crmRecord"></param>
        /// <param name="jobName"></param>
        public void LogFailure(Exception ex, Entity crmRecord, string jobName)
        {
            var recordData = new Dictionary<string, string>();
            if (crmRecord != null)
            {
                recordData = new Dictionary<string, string>()
                {
                    { "Record : Crm Record Id", crmRecord.Id.ToString("D") },
                    { "Record : Crm Record Type", crmRecord.LogicalName }
                };
            }
            else
            {
                recordData = new Dictionary<string, string>()
                {
                    { "Record : Crm Record Id", "N/A" },
                    { "Record : Crm Record Type", "N/A" }
                };
            }


            LogException(ex, recordData, jobName);
        }

        /// <summary>
        /// Log exception to App Insights
        /// </summary>
        /// <param name="exception"></param>
        /// <param name="properties"></param>
        /// <param name="jobName"></param>
        public void LogException(Exception exception, Dictionary<string, string> properties, string jobName = null)
        {
            var defaultProperties = Utilities.GetContextProperties(JobSettings, RunId);
            foreach (var property in defaultProperties)
            {
                properties.Add(property.Key, property.Value);
            }
            if (jobName != null)
            {
                properties.Add("Job Name", jobName);
            }

            Log.Error(exception, "Failure", properties);
        }
    }
}
