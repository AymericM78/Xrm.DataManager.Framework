
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Text.RegularExpressions;

namespace Xrm.DataManager.Framework
{
    public static class Utilities
    {
        private static readonly Regex ConnectionStringSecretsRegex = new Regex(@"(^|;)\s*(Password|ClientSecret|Secret)\s*=\s*('[^']*'|""[^""]*""|[^;]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Hide credentials (password, client secret) from connection string before display or tracing
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public static string MaskConnectionStringSecrets(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                return connectionString;
            }
            return ConnectionStringSecretsRegex.Replace(connectionString, "$1$2=********");
        }

        /// <summary>
        /// Get unique identifier for this application execution
        /// </summary>
        /// <returns></returns>
        public static string GetRunId()
        {
            var now = DateTime.Now;
            return $"Run-{now:yyyy-MM-dd--HH-mm-ss}";
        }

        /// <summary>
        /// Show context information in console
        /// </summary>
        /// <param name="jobSettings"></param>
        /// <param name="runId"></param>
        /// <param name="logger"></param>
        public static void OutputContextInformation(JobSettings jobSettings, ILogger logger)
        {
            var contextProperties = GetContextProperties(jobSettings);

            logger.LogDisplay($"Job parameters : ");
            foreach (var property in contextProperties)
            {
                var key = property.Key;
                key = key.Replace("Context", "");
                key = key.Replace(".", " ");
                logger.LogDisplay($" - {key} : {property.Value}");
            }
            logger.LogDisplay("");
        }

        /// <summary>
        /// Define context properties based on configuration for App Insights logging
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string> GetContextProperties(JobSettings jobSettings)
        {
            var contextProperties = new Dictionary<string, string>()
            {
                { "Context.Crm.ConnectionString", MaskConnectionStringSecrets(jobSettings.CrmConnectionString) },
                { "Context.Crm.Instance.Name", jobSettings.SelectedInstanceName },
                { "Context.Crm.User.Name", jobSettings.CrmUserName },
                { "Context.Job.Name", jobSettings.SelectedJobName },
                { "Context.Job.Run.Id", jobSettings.RunId },

                { "Context.Process.Version", typeof(Utilities).Assembly.GetName().Version.ToString() },
                { "Context.Process.MaxDuration.Hours", jobSettings.MaxRunDurationInHour.ToString() },
                { "Context.Process.Query.RecordLimit", jobSettings.QueryRecordLimit.ToString() },
                { "Context.Process.Thread.Number", jobSettings.ThreadNumber.ToString() },
                { "Context.Crm.AffinityCookie.Enabled", jobSettings.AffinityCookieEnabled.ToString() },

                { "Context.Logging.Level", jobSettings.LogLevel.ToString() },
                { "Context.Logging.AppInsight.Enabled", jobSettings.ApplicationInsightsEnabled.ToString() },
                { "Context.Logging.AppInsight.ConnectionString", jobSettings.AppInsightsConnectionString },
                { "Context.Logging.Record.Attributes", jobSettings.LogRecordAttributes.ToString() },

                { "Context.Computer.name", Environment.MachineName },
                { "Context.Computer.OS.Version", Environment.OSVersion.Version.ToString() },
                { "Context.Computer.Local.Username", Environment.UserName },
                { "Context.Computer.CurrentDirectory", Environment.CurrentDirectory }
            };

            if (jobSettings.JobNamesDefined)
            {
                contextProperties.Add("Job.Names", jobSettings.JobNames);
            }

            return contextProperties;
        }

        /// <summary>
        /// Extract record per second speed
        /// </summary>
        /// <returns></returns>
        public static string GetSpeed(double elapsedMilliseconds, int numberOfOperations) => Math.Round((numberOfOperations * 1.0 / elapsedMilliseconds * 1000), 2).ToString() + " rec/sec";

        /// <summary>
        /// Merge two dictionnary, second one overwrite first one
        /// </summary>
        /// <param name="thisDictionnary"></param>
        /// <param name="anotherDictionnary"></param>
        /// <returns></returns>
        public static Dictionary<string, string> MergeWith(this Dictionary<string, string> thisDictionnary, Dictionary<string, string> anotherDictionnary)
        {
            var newDictionnary = new Dictionary<string, string>();
            foreach (var property in thisDictionnary)
            {
                newDictionnary.Add(property.Key, property.Value);
            }
            foreach (var property in anotherDictionnary)
            {
                newDictionnary.AddOrUpdate(property.Key, property.Value);
            }
            return newDictionnary;
        }

        public static void AddOrUpdate(this Dictionary<string, string> thisDictionnary, string key, string value)
        {
            if (thisDictionnary.ContainsKey(key))
            {
                thisDictionnary[key] = value;
            }
            else
            {
                thisDictionnary.Add(key, value);
            }
        }       

        public static Dictionary<string, string> ExportProperties(this FaultException<OrganizationServiceFault> faultException)
        {
            var exceptionProperties = new Dictionary<string, string>
            {
                { "Crm.Exception.Activity Id", faultException.Detail.ActivityId.ToString() },
                { "Crm.Exception.Error Code", faultException.Detail.ErrorCode.ToString() },
                { "Crm.Exception.Message", faultException.Detail.Message?.ToString() },
                { "Crm.Exception.Timestamp", faultException.Detail.Timestamp.ToString() },
                { "Crm.Exception.Trace Text", faultException.Detail.TraceText?.ToString() }
            };
            return exceptionProperties;
        }
    }
}
