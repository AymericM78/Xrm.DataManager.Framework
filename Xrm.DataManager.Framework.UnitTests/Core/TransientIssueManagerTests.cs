using Microsoft.Xrm.Sdk;
using System;
using System.Diagnostics;
using System.ServiceModel;
using Xunit;

namespace Xrm.DataManager.Framework.UnitTests.Core
{
    public class TransientIssueManagerTests
    {
        private const int RateLimitExceededErrorCode = -2147015902;
        private const int PluginErrorCode = -2147220891;

        private static FaultException<OrganizationServiceFault> Fault(int errorCode, TimeSpan? retryAfter = null)
        {
            var detail = new OrganizationServiceFault { ErrorCode = errorCode, Message = "fault" };
            if (retryAfter.HasValue)
            {
                detail.ErrorDetails["Retry-After"] = retryAfter.Value;
            }
            return new FaultException<OrganizationServiceFault>(detail, "fault");
        }

        [Fact]
        public void Generic_exceptions_are_not_transient()
        {
            Assert.False(TransientIssueManager.IsTransientError(new Exception("boom")));
            Assert.False(TransientIssueManager.IsTransientError(new InvalidOperationException()));
        }

        [Fact]
        public void Network_exceptions_are_transient()
        {
            Assert.True(TransientIssueManager.IsTransientError(new TimeoutException()));
            Assert.True(TransientIssueManager.IsTransientError(new CommunicationException()));
            Assert.True(TransientIssueManager.IsTransientError(new EndpointNotFoundException("not found")));
        }

        [Fact]
        public void Only_service_protection_faults_are_transient()
        {
            Assert.True(TransientIssueManager.IsTransientError(Fault(RateLimitExceededErrorCode)));
            Assert.False(TransientIssueManager.IsTransientError(Fault(PluginErrorCode)));
            // Former "LoginDenied" code was the HResult of any System.Exception
            Assert.False(TransientIssueManager.IsTransientError(Fault(-2146233088)));
            // Faults are never handled by generic overload
            Assert.False(TransientIssueManager.IsTransientError((Exception)Fault(PluginErrorCode)));
        }

        [Fact]
        public void Retry_After_provided_by_Dataverse_is_honored()
        {
            var logger = new MemoryLogger();
            var stopwatch = Stopwatch.StartNew();

            TransientIssueManager.ApplyDelay(Fault(RateLimitExceededErrorCode, TimeSpan.FromSeconds(1)), logger);

            Assert.InRange(stopwatch.ElapsedMilliseconds, 900, 5000);
            Assert.Contains(logger.Messages, m => m.Contains("API Limit reached") && m.Contains("1s"));
        }
    }
}
