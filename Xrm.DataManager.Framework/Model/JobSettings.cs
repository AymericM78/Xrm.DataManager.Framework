using System;
using System.Configuration;
using System.Globalization;
using System.Linq;

namespace Xrm.DataManager.Framework
{
    public class JobSettings
    {
        public string SelectedInstanceName
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

        public string CrmConnectionString => GetConnectionStringParameter();

        [Obsolete("You must use connection string with oAuth.")]
        public string CrmUserName => GetOptionalParameter<string>("Crm.User.Name");
        [Obsolete("You must use connection string with oAuth.")]
        public string CrmUserPassword => GetOptionalParameter<string>("Crm.User.Password");
        [Obsolete("You must use connection string with oAuth.")]
        public string CrmInstanceName => GetOptionalParameter<string>("Crm.Instance.Name");

        public string JobNames => GetOptionalParameter<string>("Job.Names");
        public string AppInsightsInstrumentationKey => GetAppInsightsTelemetryKey();
        public int MaxRunDurationInHour => GetOptionalParameter<int>("Process.Duration.MaxHours", 8);
        public int QueryRecordLimit => GetOptionalParameter<int>("Process.Query.RecordLimit", 2500);
        public int ThreadNumber => GetOptionalParameter<int>("Process.Thread.Number", 10);
        public string GrayLogUrl => GetOptionalParameter<string>("Graylog.Url");
        public int LogLevel => GetOptionalParameter<int>("LogLevel", 1);


        public bool ConnectionStringDefined => (string.IsNullOrEmpty(CrmConnectionString) == false);

        public bool JobNamesDefined => (string.IsNullOrEmpty(JobNames) == false);
        public bool CrmInstanceNameDefined => (string.IsNullOrEmpty(CrmInstanceName) == false);
        public bool ApplicationInsightsEnabled => (string.IsNullOrEmpty(AppInsightsInstrumentationKey) == false);
        public bool GrayLogEnabled => (string.IsNullOrEmpty(GrayLogUrl) == false);

        const string DateFormat = "dd/MM/yyyy";
        static CultureInfo DateProvider = CultureInfo.InvariantCulture;

        /// <summary>
        /// Constructor
        /// </summary>
        public JobSettings()
        {
            RunId = Utilities.GetRunId();
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

        private string GetConnectionStringParameter()
        {
            string value = GetOptionalParameter<string>("Crm.ConnectionString");
            if (string.IsNullOrEmpty(value))
            {
                // XrmFramework compatibility
                value = GetOptionalParameter<string>("Xrm");
            }
            return value;
        }

        private string GetAppInsightsTelemetryKey()
        {
            string value = GetOptionalParameter<string>("AppInsights.Instrumentation.Key");
            if (string.IsNullOrEmpty(value))
            {
                // Azure compatibility
                value = GetOptionalParameter<string>("APPINSIGHTS_INSTRUMENTATIONKEY");
            }
            return value;
        }
    }
}
