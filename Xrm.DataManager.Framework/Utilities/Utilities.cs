
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading;

namespace Xrm.DataManager.Framework
{
    public static class Utilities
    {
        /// <summary>
        /// Get unique identifier for this application execution
        /// </summary>
        /// <returns></returns>
        public static string GetRunId()
        {
            var now = DateTime.Now;
            return $"Run-{now.ToString("yyyy-MM-dd--HH-mm-ss") }";
        }

        /// <summary>
        /// Show context information in console
        /// </summary>
        /// <param name="jobSettings"></param>
        /// <param name="runId"></param>
        /// <param name="logger"></param>
        public static void OutputContextInformation(JobSettings jobSettings, string runId, ILogger logger)
        {
            logger.LogMessage($"Version : {Version.VersionNumber}");
            logger.LogMessage($"Run Identifier : {runId}");

            logger.LogMessage($"Job parameters : ");
            logger.LogMessage($" - Crm.Instance.Name = {jobSettings.CrmOrganizationName}");
            logger.LogMessage($" - Crm.User.Name = {jobSettings.CrmUserName}");
            logger.LogMessage($" - Crm.User.Password = {jobSettings.CrmUserPassword}");
            logger.LogMessage($" - Process.Duration.MaxHours = {jobSettings.MaxRunDurationInHour}");
            logger.LogMessage($" - Process.Query.RecordLimit = {jobSettings.QueryRecordLimit}");
            logger.LogMessage($" - Process.Thread.Number = {jobSettings.ThreadNumber}");
            logger.LogMessage($" - AppInsight.Enabled = {jobSettings.ApplicationInsightsEnabled}");
            logger.LogMessage($" - Telemetry.Key = {jobSettings.AppInsightsInstrumentationKey}");

            if (jobSettings.JobNamesDefined)
            {
                logger.LogMessage($" - Job.Names = {jobSettings.JobNames}");
            }

            logger.LogMessage($"Context parameters : ");
            var defaultProperties = GetContextProperties(jobSettings, runId);
            foreach (var property in defaultProperties)
            {
                logger.LogMessage($" - {property.Key} : {property.Value}");
            }

            logger.LogMessage("");
        }

        /// <summary>
        /// Define context properties based on configuration for App Insights logging
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string> GetContextProperties(JobSettings jobSettings, string runId)
        {
            return new Dictionary<string, string>()
            {
                { "Crm Organization name", jobSettings.CrmOrganizationName },
                { "source", jobSettings.CrmOrganizationName },
                { "Crm User name", jobSettings.CrmUserName },
                { "USER", jobSettings.CrmUserName },
                { "Run ID", runId },
                { "CORRELATION_ID", runId },
                { "Computer name", Environment.MachineName },
                { "OS Version", Environment.OSVersion.Version.ToString() },
                { "Local user name", Environment.UserName },
                { "Current directory", Environment.CurrentDirectory }
            };
        }

        /// <summary>
        /// Extract record per second speed
        /// </summary>
        /// <returns></returns>
        public static string GetSpeed(double elapsedMilliseconds, int numberOfOperations) => Math.Round((numberOfOperations * 1.0 / elapsedMilliseconds * 1000), 2).ToString() + " rec/sec";

        /// <summary>
        /// Retry to perform an action with a max retry 
        /// </summary>
        /// <param name="action">Action to perform</param>
        /// <param name="maxRetries">Maximum retries</param>
        /// <param name="waitBetweenRetrySec">Waiting time between requests</param>
        /// <param name="retryStrategy">Retry strategy <seealso cref="BackOffStrategy"/></param>
        /// <param name="onExceptionAction">Action to perform when an exception is raised</param>
        public static void DoActionWithRetry(Action action, int maxRetries, int waitBetweenRetrySec, BackOffStrategy retryStrategy,
            Action<Exception, int> onExceptionAction = null)
        {
            if (action == null)
            {
                throw new ArgumentNullException("No action specified");
            }

            var retryCount = 1;

            while (retryCount <= maxRetries)
            {
                try
                {
                    action();
                    break;
                }
                catch (Exception ex)
                {
                    if (retryCount >= maxRetries)
                    {
                        throw new Exception($"Reach max retries ({maxRetries}) : \n {ex.Message}");
                    }

                    onExceptionAction?.Invoke(ex, retryCount);

                    var sleepTime = TimeSpan.FromSeconds(
                        retryStrategy == BackOffStrategy.Linear
                            ? waitBetweenRetrySec
                            : Math.Pow(waitBetweenRetrySec, retryCount));

                    Thread.Sleep(sleepTime);
                    retryCount++;
                }
            }
        }



        /// <summary>
        /// Run query expression with retry
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public static EntityCollection TryRetrieveMultiple(ProxiesPool proxiesPool, ILogger logger, QueryExpression query)
        {
            EntityCollection results = new EntityCollection();
            using (var crmProxy = proxiesPool.GetProxy())
            {
                Utilities.DoActionWithRetry
                (
                    () =>
                    {
                        results = crmProxy.RetrieveMultiple(query);
                    },
                    5, // Max retries
                    5, // Wait duration between each try (in s)
                    BackOffStrategy.Exponential, // Wait increase model applied to the wait duration
                    (ex, tryCount) =>
                    {
                        var exceptionDetails = new Dictionary<string, string>
                        {
                            { "Try count", tryCount.ToString() },
                        };
                        if (ex is FaultException<OrganizationServiceFault> faultException)
                        {
                            exceptionDetails.Add("Crm Exception : Activity Id", faultException.Detail.ActivityId.ToString());
                            exceptionDetails.Add("Crm Exception : Error Code", faultException.Detail.ErrorCode.ToString());
                            exceptionDetails.Add("Crm Exception : Message", faultException.Detail.Message?.ToString());
                            exceptionDetails.Add("Crm Exception : Timestamp", faultException.Detail.Timestamp.ToString());
                            exceptionDetails.Add("Crm Exception : Trace Text", faultException.Detail.TraceText?.ToString());
                        }
                        logger.LogException(ex, exceptionDetails);
                    }
                );

            }
            return results;
        }

        /// <summary>
        /// Run query expression with retry and pagination
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public static EntityCollection TryRetrieveAll(ProxiesPool proxiesPool, ILogger logger, QueryExpression query)
        {
            EntityCollection results = new EntityCollection();
            query.PageInfo.PageNumber = 1;

            var moreRecords = true;
            while (moreRecords)
            {
                var pageResults = TryRetrieveMultiple(proxiesPool, logger, query);
                results.Entities.AddRange(pageResults.Entities);
                query.PageInfo.PagingCookie = pageResults.PagingCookie;
                query.PageInfo.PageNumber++;
                moreRecords = pageResults.MoreRecords;

                logger.LogDebug($"Retrieving {query.EntityName} records [Page = {query.PageInfo.PageNumber} | Records retrieved = {results.Entities.Count}]");
            }
            return results;
        }
    }

    /// <summary>
    /// BackOffStrategy
    /// </summary>
    public enum BackOffStrategy
    {
        Linear = 1,
        Exponential = 2
    }
}
