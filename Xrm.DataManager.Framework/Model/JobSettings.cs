using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;

namespace Xrm.DataManager.Framework
{
    public class JobSettings
    {
        /// <summary>
        /// connectionStrings entry reserved for Application Insights (not a Dataverse connection)
        /// </summary>
        public const string AppInsightsConnectionStringName = "AppInsights";

        /// <summary>
        /// Name of the Dataverse connection used by current processor (connectionStrings entry)
        /// </summary>
        public string SelectedConnectionName
        {
            get; set;
        }

        public string SelectedJobName
        {
            get; set;
        }

        public string RunId
        {
            get; set;
        }
        public string Jobs
        {
            get; set;
        }

        /// <summary>
        /// Connection string used by current processor
        /// </summary>
        public string SelectedConnectionString
        {
            get; set;
        }

        /// <summary>
        /// Name of the connectionStrings entry to process (target)
        /// </summary>
        public string TargetConnectionName => GetOptionalParameter<string>("Target.Connection.Name");

        /// <summary>
        /// Name of the connectionStrings entry to read records from (synchronization jobs)
        /// </summary>
        public string SourceConnectionName => GetOptionalParameter<string>("Source.Connection.Name");

        /// <summary>
        /// Jobs to run (class names separated with ',')
        /// </summary>
        public string TargetJobNames => GetOptionalParameter<string>("Target.Job.Names");

        /// <summary>
        /// connectionStrings entry named AppInsights, or APPLICATIONINSIGHTS_CONNECTION_STRING environment variable (Azure)
        /// </summary>
        public string AppInsightsConnectionString => GetAppInsightsConnectionString();
        public int MaxRunDurationInHour => GetOptionalParameter<int>("Process.Duration.MaxHours", 48);
        public int QueryRecordLimit => GetOptionalParameter<int>("Process.Query.RecordLimit", 2500);
        public int ThreadNumber => GetOptionalParameter<int>("Process.Thread.Number", 5);
        /// <summary>
        /// Keep requests on the same Dataverse node (disabled by default : parallel requests are spread on all nodes)
        /// </summary>
        public bool AffinityCookieEnabled => GetOptionalParameter<bool>("Crm.AffinityCookie.Enabled", false);
        /// <summary>
        /// Apply process wide connection settings (thread pool, .NET Framework connection limit...)
        /// Disable if host application manages them
        /// </summary>
        public bool ConnectionOptimizationsEnabled => GetOptionalParameter<bool>("Process.ConnectionOptimizations.Enabled", true);
        public int LogLevel => GetOptionalParameter<int>("LogLevel", 1);
        /// <summary>
        /// Trace all record columns in telemetry (disabled by default : may contain personal data)
        /// </summary>
        public bool LogRecordAttributes => GetOptionalParameter<bool>("Logging.Record.Attributes", false);


        public bool TargetJobNamesDefined => (string.IsNullOrEmpty(TargetJobNames) == false);
        public bool ApplicationInsightsEnabled => (string.IsNullOrEmpty(AppInsightsConnectionString) == false);

        const string DateFormat = "dd/MM/yyyy";
        static CultureInfo DateProvider = CultureInfo.InvariantCulture;

        /// <summary>
        /// Constructor
        /// </summary>
        public JobSettings()
        {
            RunId = Utilities.GetRunId();
        }

        /// <summary>
        /// Get connection strings defined in configuration file (connectionStrings section)
        /// </summary>
        /// <returns>Connection strings by name</returns>
        public static Dictionary<string, string> GetConnectionStrings()
        {
            var connectionStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (ConnectionStringSettings connectionString in ConfigurationManager.ConnectionStrings)
            {
                // Ignore connection strings inherited from machine.config (ex : LocalSqlServer)
                if (!connectionString.ElementInformation.IsPresent)
                {
                    continue;
                }
                connectionStrings[connectionString.Name] = connectionString.ConnectionString;
            }
            return connectionStrings;
        }

        /// <summary>
        /// Get Dataverse connection strings (connectionStrings section without AppInsights entry)
        /// </summary>
        /// <returns>Connection strings by name</returns>
        public static Dictionary<string, string> GetDataverseConnectionStrings() => GetDataverseConnectionStrings(GetConnectionStrings());

        internal static Dictionary<string, string> GetDataverseConnectionStrings(Dictionary<string, string> connectionStrings)
        {
            var dataverseConnectionStrings = new Dictionary<string, string>(connectionStrings, StringComparer.OrdinalIgnoreCase);
            dataverseConnectionStrings.Remove(AppInsightsConnectionStringName);
            return dataverseConnectionStrings;
        }

        public static T GetMandatoryParameter<T>(string key) => GetParameter<T>(key, default, true);
        public static T GetOptionalParameter<T>(string key) => GetParameter<T>(key, default, false);
        public static T GetOptionalParameter<T>(string key, T defaultValue) => GetParameter<T>(key, defaultValue, false);

        /// <summary>
        /// Get Parameter from App.Config
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private static T GetParameter<T>(string key, T defaultValue, bool isRequired = false)
        {
            if (!ConfigurationManager.AppSettings.AllKeys.Contains(key))
            {
                if (isRequired)
                {
                    throw new Exception($"Invalid configuration : setting '{key}' is missing in AppSettings section!");
                }
                else
                {
                    if (defaultValue != null)
                    {
                        return defaultValue;
                    }
                    return default;
                }
            }

            var value = ConfigurationManager.AppSettings[key];

            if (typeof(T) == typeof(bool))
            {
                if (bool.TryParse(value, out bool v))
                {
                    return (T)(object)v;
                }
            }
            else if (typeof(T) == typeof(string))
            {
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(int))
            {
                if (int.TryParse(value, out int v))
                {
                    return (T)(object)v;
                }
            }
            else if (typeof(T) == typeof(DateTime))
            {
                try
                {
                    var date = DateTime.ParseExact(value, DateFormat, DateProvider);
                    return (T)(object)date;
                }
                catch (Exception)
                {
                    throw new Exception($"Parameter {key} doesn't match expected date format! (Format: {DateFormat})");
                }
            }

            throw new Exception($"Invalid configuration : setting '{key}' has not valid type!");
        }

        private static string GetAppInsightsConnectionString()
        {
            var value = ConfigurationManager.ConnectionStrings[AppInsightsConnectionStringName]?.ConnectionString;
            if (string.IsNullOrEmpty(value))
            {
                // Azure compatibility (App Service, Functions...)
                value = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
            }
            return value;
        }
    }
}
