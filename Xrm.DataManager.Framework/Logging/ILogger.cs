using System;
using System.Collections.Generic;

namespace Xrm.DataManager.Framework
{
    public interface ILogger
    {
        void LogVerbose(string message);

        void LogDisplay(string message);

        void LogInformation(string message, bool display = true);

        void LogSuccess(string message, Dictionary<string, string> properties);

        void LogFailure(Exception exception);

        void LogFailure(Exception exception, Dictionary<string, string> properties);
    }
}