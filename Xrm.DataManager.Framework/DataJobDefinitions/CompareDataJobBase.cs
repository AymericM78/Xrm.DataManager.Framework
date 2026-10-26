using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Xrm.DataManager.Framework
{
    /// <summary>
    /// Compare records between source connection (Source.Connection.Name) and target connection :
    /// records only in source, only in target, different or equal (CSV report)
    /// Results can then be processed on target (ShouldProcess / ProcessResult)
    /// </summary>
    public abstract class CompareDataJobBase : DataJobBase
    {
        /// <summary>
        /// Excluded when Compare.ExcludedAttributes setting is not defined : system, ownership and technical columns
        /// </summary>
        public static readonly string[] DefaultExcludedAttributes = SyncDataJobBase.DefaultExcludedAttributes.Concat(new[]
        {
            "versionnumber",
            "importsequencenumber",
            "overriddencreatedon",
            "timezoneruleversionnumber",
            "utcconversiontimezonecode",
            "exchangerate"
        }).ToArray();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="jobSettings"></param>
        public CompareDataJobBase(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters)
        {
        }

        /// <summary>
        /// Records and columns to compare (executed on source connection)
        /// </summary>
        /// <param name="callerId">Source connection user id</param>
        /// <returns></returns>
        public abstract QueryExpression GetQuery(Guid callerId);

        /// <summary>
        /// Records and columns to compare on target connection (default : GetQuery)
        /// </summary>
        /// <param name="callerId">Target connection user id</param>
        /// <returns></returns>
        public virtual QueryExpression GetTargetQuery(Guid callerId) => GetQuery(callerId);

        /// <summary>
        /// connectionStrings entry to read source records from (default : Source.Connection.Name setting)
        /// </summary>
        /// <returns></returns>
        public virtual string GetSourceConnectionName() => JobSettings.SourceConnectionName;

        /// <summary>
        /// Key used to match source and target records (default : record id)
        /// Override if ids are different between connections (ex : account number)
        /// </summary>
        /// <param name="record"></param>
        /// <returns>Null or empty : record is ignored</returns>
        public virtual string GetMatchingKey(Entity record) => record.Id.ToString();

        /// <summary>
        /// Attributes never compared (default : Compare.ExcludedAttributes setting, or DefaultExcludedAttributes if not defined)
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<string> GetExcludedAttributes() => Utilities.ParseAttributeNames(JobSettings.GetOptionalParameter<string>("Compare.ExcludedAttributes"), DefaultExcludedAttributes);

        /// <summary>
        /// Compare attribute values (default : lookups by id, choices and money by value, null equals empty string)
        /// </summary>
        public virtual bool AreEqual(string attributeName, object sourceValue, object targetValue) => Equals(NormalizeValue(sourceValue), NormalizeValue(targetValue));

        /// <summary>
        /// Write equal records in report (default : false)
        /// </summary>
        protected virtual bool ReportEqualRecords => false;

        /// <summary>
        /// Report columns separator
        /// </summary>
        protected virtual char ReportSeparator => ';';

        /// <summary>
        /// Report file (default : {JobClassName}_{RunId}.csv in current directory)
        /// </summary>
        /// <returns></returns>
        protected virtual string GetReportFilePath() => Path.Combine(Environment.CurrentDirectory, $"{GetType().Name}_{JobSettings.RunId}.csv");

        /// <summary>
        /// Specify results to process on target with ProcessResult (default : none)
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        protected virtual bool ShouldProcess(DataCompareResult result) => false;

        /// <summary>
        /// Process comparison result on target (ex : delete records only in target)
        /// </summary>
        /// <param name="context">Record : target record (source record if OnlyInSource) / Proxy : target connection</param>
        /// <param name="result"></param>
        protected virtual void ProcessResult(JobExecutionContext context, DataCompareResult result) => throw new NotImplementedException("Override ProcessResult to process comparison results!");

        /// <summary>
        /// Run the comparison
        /// </summary>
        /// <returns></returns>
        public override bool Run()
        {
            var stopwatch = Stopwatch.StartNew();
            var sourceConnectionName = GetSourceConnectionName();
            var sourceProxiesPool = GetSourceProxiesPool(sourceConnectionName);
            var sourceQuery = GetQuery(sourceProxiesPool.MainProxy.CallerId);
            var targetQuery = GetTargetQuery(CallerId);

            Logger.LogInformation($"Comparing '{sourceQuery.EntityName}' records from '{sourceConnectionName}' with '{targetQuery.EntityName}' records from '{JobSettings.SelectedConnectionName}'", ContextProperties);

            // Both connections are queried simultaneously
            var sourceTask = Task.Run(() => RetrieveAll(sourceProxiesPool, sourceQuery));
            var targetTask = Task.Run(() => RetrieveAll(ProxiesPool, targetQuery));
            var sourceRecords = sourceTask.GetAwaiter().GetResult();
            var targetRecords = targetTask.GetAwaiter().GetResult();
            Logger.LogInformation($"Retrieved {sourceRecords.Count} records from '{sourceConnectionName}' and {targetRecords.Count} records from '{JobSettings.SelectedConnectionName}'", ContextProperties);

            var excludedAttributes = new HashSet<string>(GetExcludedAttributes(), StringComparer.OrdinalIgnoreCase);
            var results = CompareRecords(sourceRecords, targetRecords, GetMatchingKey, excludedAttributes, AreEqual, message => Logger.LogInformation(message, ContextProperties));

            var counts = results.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());
            int Count(DataCompareStatus status) => counts.TryGetValue(status, out var count) ? count : 0;
            Logger.LogInformation($"Comparison completed in {stopwatch.Elapsed:g} : {Count(DataCompareStatus.OnlyInSource)} only in source, {Count(DataCompareStatus.OnlyInTarget)} only in target, {Count(DataCompareStatus.Different)} different, {Count(DataCompareStatus.Equal)} equal", ContextProperties);

            var reportFilePath = GetReportFilePath();
            using (var writer = new StreamWriter(reportFilePath, false, new UTF8Encoding(true)))
            {
                WriteReport(writer, results, ReportSeparator, ReportEqualRecords);
            }
            Logger.LogInformation($"Comparison report : {reportFilePath}", ContextProperties);

            ProcessResults(results.Where(ShouldProcess).ToList());
            return true;
        }

        /// <summary>
        /// Process selected results on target connection (multi threaded)
        /// </summary>
        private void ProcessResults(List<DataCompareResult> results)
        {
            if (results.Count == 0)
            {
                return;
            }
            Logger.LogInformation($"Processing {results.Count} comparison results on '{JobSettings.SelectedConnectionName}'", ContextProperties);

            var threads = OverrideThreadNumber ?? JobSettings.ThreadNumber;
            var logRecordAttributes = JobSettings.LogRecordAttributes;
            var processedCount = 0;
            var failureCount = 0;
            Parallel.ForEach(results,
                new ParallelOptions { MaxDegreeOfParallelism = threads },
                () => ProxiesPool.GetProxy(),
                (result, loopState, proxy) =>
                {
                    var context = new JobExecutionContext(proxy, result.TargetRecord ?? result.SourceRecord, logRecordAttributes);
                    context.PushMetrics(ContextProperties);
                    context.PushMetric("Compare.Status", result.Status.ToString());
                    context.PushMetric("Compare.Key", result.Key);
                    try
                    {
                        ProcessResult(context, result);
                        System.Threading.Interlocked.Increment(ref processedCount);
                        Logger.LogSuccess("Comparison result processed with success!", context.DumpMetrics());
                    }
                    catch (FaultException<OrganizationServiceFault> faultException)
                    {
                        System.Threading.Interlocked.Increment(ref failureCount);
                        Logger.LogFailure(faultException, context.DumpMetrics().MergeWith(faultException.ExportProperties()));
                    }
                    catch (Exception ex)
                    {
                        System.Threading.Interlocked.Increment(ref failureCount);
                        Logger.LogFailure(ex, context.DumpMetrics());
                    }
                    return proxy;
                },
                proxy => ProxiesPool.ReleaseProxy(proxy));

            Logger.LogInformation($"Comparison results processed : Success = {processedCount} | Failures = {failureCount}", ContextProperties);
        }

        /// <summary>
        /// Match source and target records by key and compare their attributes
        /// </summary>
        internal static List<DataCompareResult> CompareRecords(IEnumerable<Entity> sourceRecords, IEnumerable<Entity> targetRecords, Func<Entity, string> getKey, ISet<string> excludedAttributes, Func<string, object, object, bool> areEqual, Action<string> logWarning)
        {
            var sourceByKey = IndexByKey(sourceRecords, getKey, "source", logWarning);
            var targetByKey = IndexByKey(targetRecords, getKey, "target", logWarning);

            var results = new List<DataCompareResult>();
            foreach (var source in sourceByKey)
            {
                if (!targetByKey.TryGetValue(source.Key, out var targetRecord))
                {
                    results.Add(new DataCompareResult(source.Key, DataCompareStatus.OnlyInSource, source.Value, null));
                    continue;
                }

                var differentAttributes = GetDifferentAttributes(source.Value, targetRecord, excludedAttributes, areEqual);
                var status = (differentAttributes.Count == 0) ? DataCompareStatus.Equal : DataCompareStatus.Different;
                results.Add(new DataCompareResult(source.Key, status, source.Value, targetRecord, differentAttributes));
            }

            foreach (var target in targetByKey.Where(t => !sourceByKey.ContainsKey(t.Key)))
            {
                results.Add(new DataCompareResult(target.Key, DataCompareStatus.OnlyInTarget, null, target.Value));
            }
            return results;
        }

        private static Dictionary<string, Entity> IndexByKey(IEnumerable<Entity> records, Func<Entity, string> getKey, string side, Action<string> logWarning)
        {
            var recordsByKey = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
            var emptyKeys = 0;
            var duplicateKeys = 0;
            foreach (var record in records)
            {
                var key = getKey(record);
                if (string.IsNullOrEmpty(key))
                {
                    emptyKeys++;
                    continue;
                }
                if (recordsByKey.ContainsKey(key))
                {
                    duplicateKeys++;
                    continue;
                }
                recordsByKey.Add(key, record);
            }

            if (emptyKeys > 0)
            {
                logWarning($"{emptyKeys} {side} records ignored : matching key is empty!");
            }
            if (duplicateKeys > 0)
            {
                logWarning($"{duplicateKeys} {side} records ignored : matching key is not unique (first record is compared)!");
            }
            return recordsByKey;
        }

        private static List<string> GetDifferentAttributes(Entity sourceRecord, Entity targetRecord, ISet<string> excludedAttributes, Func<string, object, object, bool> areEqual)
        {
            var attributes = sourceRecord.Attributes.Keys.Union(targetRecord.Attributes.Keys, StringComparer.OrdinalIgnoreCase)
                .Where(a => !excludedAttributes.Contains(a))
                .OrderBy(a => a, StringComparer.OrdinalIgnoreCase);

            var differentAttributes = new List<string>();
            foreach (var attribute in attributes)
            {
                var sourceValue = sourceRecord.Contains(attribute) ? sourceRecord[attribute] : null;
                var targetValue = targetRecord.Contains(attribute) ? targetRecord[attribute] : null;

                // Primary key : ids may be different when records are matched with another key
                if (sourceValue is Guid sourceId && sourceId == sourceRecord.Id && targetValue is Guid targetId && targetId == targetRecord.Id)
                {
                    continue;
                }
                if (!areEqual(attribute, sourceValue, targetValue))
                {
                    differentAttributes.Add(attribute);
                }
            }
            return differentAttributes;
        }

        /// <summary>
        /// Comparable value : lookups by table and id, choices and money by value, empty string as null
        /// </summary>
        internal static object NormalizeValue(object value)
        {
            switch (value)
            {
                case null:
                    return null;
                case AliasedValue aliasedValue:
                    return NormalizeValue(aliasedValue.Value);
                case string text:
                    return (text.Length == 0) ? null : text;
                case EntityReference reference:
                    return $"{reference.LogicalName}:{reference.Id}";
                case OptionSetValue option:
                    return option.Value;
                case Money money:
                    return money.Value;
                case OptionSetValueCollection options:
                    return string.Join(",", options.Select(o => o.Value).OrderBy(v => v));
                case EntityCollection collection:
                    return string.Join(",", collection.Entities.Select(e => e.Id).OrderBy(id => id));
                default:
                    return value;
            }
        }

        /// <summary>
        /// Write comparison results : Status, Key, Source Id, Target Id, Differences
        /// </summary>
        internal static void WriteReport(TextWriter writer, IEnumerable<DataCompareResult> results, char separator, bool includeEqualRecords)
        {
            string Line(params string[] fields) => string.Join(separator.ToString(), fields.Select(f => EscapeCsv(f, separator)));

            writer.WriteLine(Line("Status", "Key", "Source Id", "Target Id", "Differences"));
            foreach (var result in results.Where(r => includeEqualRecords || r.Status != DataCompareStatus.Equal).OrderBy(r => r.Status).ThenBy(r => r.Key))
            {
                var differences = string.Join(" | ", result.DifferentAttributes.Select(a => $"{a} : {FormatValue(result.SourceRecord, a)} => {FormatValue(result.TargetRecord, a)}"));
                writer.WriteLine(Line(result.Status.ToString(), result.Key, result.SourceRecord?.Id.ToString(), result.TargetRecord?.Id.ToString(), differences));
            }
        }

        /// <summary>
        /// Readable value for report
        /// </summary>
        internal static string FormatValue(Entity record, string attribute)
        {
            if (record == null || !record.Contains(attribute) || record[attribute] == null)
            {
                return "(empty)";
            }

            var value = (record[attribute] is AliasedValue aliasedValue) ? aliasedValue.Value : record[attribute];
            switch (value)
            {
                case EntityReference reference:
                    return string.IsNullOrEmpty(reference.Name) ? reference.Id.ToString() : $"{reference.Name} ({reference.Id})";
                case OptionSetValue option:
                    return record.FormattedValues.Contains(attribute) ? $"{record.FormattedValues[attribute]} ({option.Value})" : option.Value.ToString(CultureInfo.InvariantCulture);
                case Money money:
                    return money.Value.ToString(CultureInfo.InvariantCulture);
                case OptionSetValueCollection options:
                    return string.Join(",", options.Select(o => o.Value));
                case EntityCollection collection:
                    return string.Join(",", collection.Entities.Select(e => e.Id));
                case DateTime date:
                    return date.ToString("o", CultureInfo.InvariantCulture);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        private static string EscapeCsv(string field, char separator)
        {
            if (string.IsNullOrEmpty(field))
            {
                return string.Empty;
            }
            if (field.IndexOf(separator) >= 0 || field.IndexOf('"') >= 0 || field.IndexOf('\r') >= 0 || field.IndexOf('\n') >= 0)
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }
    }
}
