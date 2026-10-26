using System;
using System.Collections.Generic;
using System.IO;

namespace Xrm.DataManager.Framework
{
    /// <summary>
    /// Default logger : write logs to {RunId}.log file in current directory (no dependency)
    /// </summary>
    public class FileLogger : BaseLogger, ILogger
    {
        // Shared by all instances : several processors may write in the same file
        private static readonly object LockObject = new object();

        protected string FilePath;

        /// <summary>
        /// Constructor
        /// </summary>
        public FileLogger(JobSettings jobSettings) : base(jobSettings)
        {
            JobSettings = jobSettings;
            FilePath = Path.Combine(Environment.CurrentDirectory, $"{jobSettings.RunId}.log");
        }

        /// <summary>
        /// Append line to log file (logging must never stop the job)
        /// </summary>
        protected void Write(string level, string message, Exception exception = null)
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}{Environment.NewLine}";
            if (exception != null)
            {
                line += exception + Environment.NewLine;
            }

            try
            {
                lock (LockObject)
                {
                    File.AppendAllText(FilePath, line);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write log file {FilePath} : {ex.Message}");
            }
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
            Write("VRB", message);
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
            Write("INF", message);
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
        public override void LogSuccess(string message, Dictionary<string, string> properties)
        {
            if (LogLevel > LogLevel.ErrorsAndSuccess)
            {
                return;
            }
            Write("SUC", message);
        }

        /// <summary>
        /// Log failure to file
        /// </summary>
        /// <param name="exception"></param>
        /// <param name="properties"></param>
        public override void LogFailure(Exception exception, Dictionary<string, string> properties)
        {
            Write("ERR", "Failure", exception);
            Console.WriteLine(exception.Message);
        }
    }
}
