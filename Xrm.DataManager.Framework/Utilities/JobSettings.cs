using System;
using System.Configuration;
using System.Globalization;
using System.Linq;

namespace Xrm.DataManager.Framework
{
    public class JobSettings
    {
        public string CrmOrganizationName
        {
            get; set;
        }
        public string RunId
        {
            get;
        }

        public string CrmUserName => GetParameter<string>("Crm.User.Name");
        public string CrmUserPassword => GetParameter<string>("Crm.User.Password");
        public string CrmInstanceName => GetParameter<string>("Crm.Instance.Name");
        public bool CrmInstanceNameDefined => (string.IsNullOrEmpty(CrmInstanceName) == false);

        public string Jobs
        {
            get; set;
        }
        public string JobNames => GetParameter<string>("Job.Names");
        public bool JobNamesDefined => (string.IsNullOrEmpty(JobNames) == false);
        public string AppInsightsInstrumentationKey => GetParameter<string>("AppInsights.Instrumentation.Key");
        public bool ApplicationInsightsEnabled => (string.IsNullOrEmpty(AppInsightsInstrumentationKey) == false);
        public int MaxRunDurationInHour => GetParameter<int>("Process.Duration.MaxHours");
        public int QueryRecordLimit => GetParameter<int>("Process.Query.RecordLimit");
        public int ThreadNumber => GetParameter<int>("Process.Thread.Number");

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
        /// Get Parameter from App.Config
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static T GetParameter<T>(string key)
        {
            if (!ConfigurationManager.AppSettings.AllKeys.Contains(key))
            {
                throw new Exception($"Invalid configuration : setting '{key}' is missing in AppSettings section!");
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
    }
}
