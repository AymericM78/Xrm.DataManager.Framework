using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;

namespace Xrm.DataManager.Framework
{
    public interface ILogger
    {
        void LogDebug(string message);
        void LogEvent(string name, Dictionary<string, string> properties, string jobName = null);
        void LogException(Exception exception, Dictionary<string, string> properties, string jobName = null);
        void LogFailure(Exception ex, Entity crmRecord, string jobName);
        void LogMessage(string message, string jobName = null);
        void LogSuccess(string eventName, Entity crmRecord, string jobName);
    }
}