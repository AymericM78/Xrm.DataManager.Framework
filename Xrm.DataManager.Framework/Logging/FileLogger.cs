using Serilog;
using System;
using System.Collections.Generic;

namespace Xrm.DataManager.Framework
{
    public class FileLogger : BaseLogger, ILogger
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public FileLogger(JobSettings jobSettings) : base(jobSettings)
        {
            JobSettings = jobSettings;

            var loggerConfiguration = new LoggerConfiguration()
                .WriteTo.Console(Serilog.Events.LogEventLevel.Error)
                .WriteTo.File($"{jobSettings.RunId}.log");

            if (LogLevel == LogLevel.Verbose)
            {
                loggerConfiguration.MinimumLevel.Verbose();
            }
            else if (LogLevel == LogLevel.Information)
            {
                loggerConfiguration.MinimumLevel.Information();
            }
            else if (LogLevel == LogLevel.ErrorsAndSuccess)
            {
                loggerConfiguration.MinimumLevel.Warning();
            }
            else if (LogLevel == LogLevel.ErrorsOnly)
            {
                loggerConfiguration.MinimumLevel.Error();
            }
            Log.Logger = loggerConfiguration.CreateLogger();
        }

        /// <summary>
        /// Log verbose info to file
        /// </summary>
        /// <param name="message"></param>
        public override void LogVerbose(string message)
        {
            if (LogLevel != LogLevel.Verbose)
            {
                return;
            }
            Log.Verbose(message);
        }

        /// <summary>
        /// Log information to file
        /// </summary>
        public override void LogInformation(string message, bool display = true)
        {
            if (display)
            {
                LogDisplay(message);
            }

            if (LogLevel > LogLevel.Information)
            {
                return;
            }
            Log.Information(message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="properties"></param>
        /// <param name="display"></param>
        public override void LogInformation(string message, Dictionary<string, string> properties, bool display = true)
        {
            LogInformation(message, display);
        }

        /// <summary>
        /// Log success to file
        /// </summary>
        /// <param name="message"></param>
        /// <param name="properties"></param>
        /// <param name="jobName"></param>
        public override void LogSuccess(string message, Dictionary<string, string> properties)
        {
            Log.Warning(message, properties);
        }

        /// <summary>
        /// Log failure to file
        /// </summary>
        /// <param name="exception"></param>
        /// <param name="properties"></param>
        /// <param name="jobName"></param>
        public override void LogFailure(Exception exception, Dictionary<string, string> properties)
        {
            Log.Error(exception, "Failure", properties);
        }
    }
}
