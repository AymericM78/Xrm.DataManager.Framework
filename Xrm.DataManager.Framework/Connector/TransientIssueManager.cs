using Microsoft.Xrm.Sdk;
using System;
using System.ServiceModel;
using System.Threading;

namespace Xrm.DataManager.Framework
{
    public static class TransientIssueManager
    {
        private const int RateLimitExceededErrorCode = -2147015902;
        private const int TimeLimitExceededErrorCode = -2147015903;
        private const int ConcurrencyLimitExceededErrorCode = -2147015898;

        public static void ApplyDelay(FaultException<OrganizationServiceFault> e, ILogger logger)
        {
            // Wait during random duration
            var seconds = new Random().Next(1, 100);
            var delay = TimeSpan.FromSeconds(seconds);

            var currentThread = Thread.CurrentThread;
            logger.LogInformation($"API Limit reached! Current thread '{currentThread.ManagedThreadId}' will wait during {delay.TotalSeconds}s!");
            Thread.Sleep(delay);
        }

        public static bool IsTransientError(FaultException<OrganizationServiceFault> ex)
        {
            // You can add more transient fault codes to retry here
            return ex.Detail.ErrorCode == RateLimitExceededErrorCode ||
                   ex.Detail.ErrorCode == TimeLimitExceededErrorCode ||
                   ex.Detail.ErrorCode == ConcurrencyLimitExceededErrorCode;
        }
    }
}
