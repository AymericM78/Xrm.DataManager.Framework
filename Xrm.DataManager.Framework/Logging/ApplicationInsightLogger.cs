using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Collections.Generic;

namespace Xrm.DataManager.Framework
{
    public class ApplicationInsightLogger : BaseLogger, ILogger
    {
        protected TelemetryClient TelemetryClient;

        /// <summary>
        /// Constructor
        /// </summary>
        public ApplicationInsightLogger(JobSettings jobSettings) : base(jobSettings)
        {
            JobSettings = jobSettings;
            InitializeClient();
        }

        /// <summary>
        /// Initialize telemetry client
        /// </summary>
        private void InitializeClient()
        {
            TelemetryClient = new TelemetryClient();
            TelemetryConfiguration.Active.InstrumentationKey = JobSettings.AppInsightsInstrumentationKey;
        }

        public override void LogVerbose(string message)
        {
            if (LogLevel != LogLevel.Verbose)
            {
                return;
            }

            message = $"DBG : {message}";
            TelemetryClient.TrackTrace(message, SeverityLevel.Verbose);
            Console.WriteLine(message);
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
            TelemetryClient.TrackTrace(message, SeverityLevel.Information);
        }

        public override void LogSuccess(string message, Dictionary<string, string> properties)
        {
            if (LogLevel > LogLevel.ErrorsAndSuccess)
            {
                return;
            }
            TelemetryClient.TrackEvent(message, properties);
        }

        public override void LogFailure(Exception exception, Dictionary<string, string> properties)
        {
            TelemetryClient.TrackException(exception, properties);
            Console.WriteLine(exception.Message);
        }
    }
}
