using Microsoft.Xrm.Sdk;
using System;
using System.Net;
using System.ServiceModel;
using System.Threading;

namespace Xrm.DataManager.Framework
{
    /// <summary>
    /// Last resort retry policy : ServiceClient already retries service protection limits (MaxRetryCount / RetryPauseTime)
    /// </summary>
    public static class TransientIssueManager
    {
        private const int RateLimitExceededErrorCode = -2147015902;
        private const int TimeLimitExceededErrorCode = -2147015903;
        private const int ConcurrencyLimitExceededErrorCode = -2147015898;

        // Shared generator : Random instances created at the same time on several threads return the same values
        private static readonly Random Random = new Random();

        public static void ApplyDelay(FaultException<OrganizationServiceFault> e, ILogger logger)
        {
            // Use delay provided by Dataverse if any
            if (e.Detail.ErrorDetails.TryGetValue("Retry-After", out var retryAfter) && retryAfter is TimeSpan retryAfterDelay)
            {
                Wait(retryAfterDelay, "API Limit reached!", logger);
                return;
            }
            Wait(GetRandomDelay(), "API Limit reached!", logger);
        }

        public static void ApplyDelay(ILogger logger)
        {
            Wait(GetRandomDelay(), "Transient error detected!", logger);
        }

        private static TimeSpan GetRandomDelay()
        {
            // Wait during random duration
            lock (Random)
            {
                return TimeSpan.FromSeconds(Random.Next(30, 60));
            }
        }

        private static void Wait(TimeSpan delay, string reason, ILogger logger)
        {
            var currentThread = Thread.CurrentThread;
            logger.LogInformation($"{reason} Current thread '{currentThread.ManagedThreadId}' will wait during {delay.TotalSeconds}s!");
            Thread.Sleep(delay);
        }

        public static bool IsTransientError(Exception ex)
        {
            // Network issues only (faults are functional errors, see overload below)
            // You can add more transient exception types to retry here
            return ex is TimeoutException
                || ex is WebException
                || (ex is CommunicationException && !(ex is FaultException));
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
