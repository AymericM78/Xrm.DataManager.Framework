using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using MsLogger = Microsoft.Extensions.Logging.ILogger;

namespace Xrm.DataManager.Framework
{
    /// <summary>
    /// Forward logs to Microsoft.Extensions.Logging : use any provider (Serilog, NLog, OpenTelemetry, Console...)
    /// Properties are provided as logging scope
    /// </summary>
    public class ExtensionsLogger : BaseLogger, ILogger
    {
        public static readonly EventId SuccessEventId = new EventId(1, "Success");
        public static readonly EventId FailureEventId = new EventId(2, "Failure");

        protected MsLogger InnerLogger;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="jobSettings"></param>
        /// <param name="logger">ex : loggerFactory.CreateLogger("Xrm.DataManager")</param>
        public ExtensionsLogger(JobSettings jobSettings, MsLogger logger) : base(jobSettings)
        {
            JobSettings = jobSettings;
            InnerLogger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private IDisposable BeginScope(Dictionary<string, string> properties)
        {
            // Structured providers expect key / object pairs
            return InnerLogger.BeginScope(properties.ToDictionary(p => p.Key, p => (object)p.Value));
        }

        public override void LogVerbose(string message)
        {
            if (LogLevel != LogLevel.Verbose)
            {
                return;
            }
            InnerLogger.LogDebug("{Message}", message);
        }

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
            InnerLogger.LogInformation("{Message}", message);
        }

        public override void LogInformation(string message, Dictionary<string, string> properties, bool display = true)
        {
            if (display)
            {
                LogDisplay(message);
            }
            if (LogLevel > LogLevel.Information)
            {
                return;
            }
            using (BeginScope(properties))
            {
                InnerLogger.LogInformation("{Message}", message);
            }
        }

        public override void LogSuccess(string message, Dictionary<string, string> properties)
        {
            if (LogLevel > LogLevel.ErrorsAndSuccess)
            {
                return;
            }
            using (BeginScope(properties))
            {
                InnerLogger.LogInformation(SuccessEventId, "{Message}", message);
            }
        }

        public override void LogFailure(Exception exception, Dictionary<string, string> properties)
        {
            using (BeginScope(properties))
            {
                InnerLogger.LogError(FailureEventId, exception, "{Message}", exception.Message);
            }
            Console.WriteLine(exception.Message);
        }
    }
}
