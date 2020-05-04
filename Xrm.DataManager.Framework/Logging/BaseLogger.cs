using System;
using System.Collections.Generic;

namespace Xrm.DataManager.Framework
{
    public abstract class BaseLogger : ILogger
    {
        protected JobSettings JobSettings;
        protected LogLevel LogLevel;

        public BaseLogger(JobSettings jobSettings)
        {
            JobSettings = jobSettings;

            try
            {
                LogLevel = (LogLevel)Enum.Parse(typeof(LogLevel), JobSettings.LogLevel.ToString());
            }
            catch
            {
                throw new Exception($"Incorrect log level in configuration! (value = '{JobSettings.LogLevel.ToString()}')");
            }
        }

        protected Dictionary<string, string> FillWithContext(Dictionary<string, string> properties)
        {
            var fullContextDictionnary = properties.MergeWith(Utilities.GetContextProperties(JobSettings));
            return fullContextDictionnary;
        }

        public virtual void LogDisplay(string message) => Console.WriteLine(message);

        public virtual void LogSuccess(string message) => LogSuccess(message, new Dictionary<string, string>());

        public virtual void LogFailure(Exception exception) => LogFailure(exception, new Dictionary<string, string>());

        public abstract void LogFailure(Exception exception, Dictionary<string, string> properties);

        public abstract void LogInformation(string message, bool display = true);

        public abstract void LogSuccess(string message, Dictionary<string, string> properties);

        public abstract void LogVerbose(string message);
    }
}
