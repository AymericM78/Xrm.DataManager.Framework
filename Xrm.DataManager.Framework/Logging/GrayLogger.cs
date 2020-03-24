using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Xrm.DataManager.Framework
{
    public class GrayLogger : ILogger
    {
        protected JobSettings JobSettings;
        protected string RunId;
        private HttpClient client;
        private GrayLogLevel LogLevel = GrayLogLevel.Error;

        private enum GrayLogLevel
        {
            Error,
            AllLogs
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public GrayLogger(JobSettings jobSettings, string runId)
        {
            JobSettings = jobSettings;
            RunId = runId;

            client = new HttpClient()
            {
                Timeout = new TimeSpan(0, 0, 30)
            };
            // Add HTTP headers
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.ConnectionClose = false;

            try
            {
                LogLevel = (GrayLogLevel)Enum.Parse(typeof(GrayLogLevel), JobSettings.GrayLogLevel.ToString());
            }
            catch
            {
                throw new Exception($"Incorrect log level in GrayLog configuration! (value = '{JobSettings.GrayLogLevel.ToString()}')");
            }
        }

        protected void LogInternal(string message)
        {
            LogInternal(message, "Jobname not provided");
        }

        protected void LogInternal(string message, string jobName)
        {
            var defaultProperties = Utilities.GetContextProperties(JobSettings, RunId);
            if (jobName != null)
            {
                defaultProperties.Add("Job Name", jobName);
            }
            LogInternal(message, defaultProperties);
        }

        protected void LogInternal(string message, Dictionary<string, string> properties)
        {
            LogInternal(message, false, properties);
        }

        protected void LogInternal(string message, bool isError, Dictionary<string, string> properties)
        {
            Console.WriteLine(message);
            if (LogLevel != GrayLogLevel.AllLogs && !isError)
            {
                return;
            }

            properties.Add("message", message);

            var newProperties = new Dictionary<string, string>();
            foreach (var property in properties)
            {
                var newKey = property.Key.Replace(" ", "_");
                newKey = newKey.Replace(":", "");
                newProperties.Add(newKey, property.Value);
            }

            try
            {
                var content = new StringContent(JsonConvert.SerializeObject(newProperties), Encoding.UTF8, "application/json");
                client.PostAsync(JobSettings.GrayLogUrl, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error occured: {ex.Message}!");
            }
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
            LogInternal(message, jobName);
        }

        /// <summary>
        /// Log message to console
        /// </summary>
        /// <param name="message"></param>
        public void LogDebug(string message)
        {
            LogInternal(message);
        }

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
            LogInternal(name, properties);
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
            var recordData = new Dictionary<string, string>()
            {
                { "Record : Crm Record Id", crmRecord.Id.ToString("D") },
                { "Record : Crm Record Type", crmRecord.LogicalName }
            };

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

            LogInternal(exception.Message, true, properties);
        }
    }
}
