using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xrm.DataManager.Framework
{
    /// <summary>
    /// Copy records from source connection (Source.Connection.Name) to target connection (Upsert with same id)
    /// Copied attributes : retrieved by GetQuery, not excluded (Sync.ExcludedAttributes), valid for update on target, lookups only if enabled
    /// </summary>
    public abstract class SyncDataJobBase : FullProcessDataJobBase
    {
        /// <summary>
        /// Excluded when Sync.ExcludedAttributes setting is not defined : system and ownership columns
        /// </summary>
        public static readonly string[] DefaultExcludedAttributes =
        {
            "createdon",
            "modifiedon",
            "createdby",
            "modifiedby",
            "createdonbehalfby",
            "modifiedonbehalfby",
            "ownerid",
            "owningbusinessunit",
            "owninguser",
            "owningteam",
            "transactioncurrencyid"
        };

        /// <summary>
        /// Never copied with other attributes : applied after upsert when SyncState is enabled
        /// </summary>
        private static readonly string[] StateAttributes = { "statecode", "statuscode" };

        private List<Entity> sourceRecords;
        private HashSet<string> excludedAttributes;
        private HashSet<string> targetAttributes;
        private int currentPass = 1;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="jobSettings"></param>
        public SyncDataJobBase(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters)
        {
        }

        /// <summary>
        /// Records and columns to copy (executed on source connection)
        /// </summary>
        /// <param name="callerId">Source connection user id</param>
        /// <returns></returns>
        public abstract override QueryExpression GetQuery(Guid callerId);

        /// <summary>
        /// connectionStrings entry to read records from (default : Source.Connection.Name setting)
        /// </summary>
        /// <returns></returns>
        public virtual string GetSourceConnectionName() => JobSettings.SourceConnectionName;

        /// <summary>
        /// Attributes never copied (default : Sync.ExcludedAttributes setting, or DefaultExcludedAttributes if not defined)
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<string> GetExcludedAttributes() => ParseExcludedAttributes(JobSettings.GetOptionalParameter<string>("Sync.ExcludedAttributes"));

        /// <summary>
        /// Copy lookups (default : false, referenced records must exist on target)
        /// </summary>
        protected virtual bool IncludeEntityReferences => false;

        /// <summary>
        /// Run 2 passes : records without lookups, then lookups (ex : parent account in the same table)
        /// </summary>
        protected virtual bool TwoPassDependencies => false;

        /// <summary>
        /// Apply source statecode / statuscode after upsert (default : false)
        /// </summary>
        protected virtual bool SyncState => false;

        /// <summary>
        /// Bypass custom plugins on target (prvBypassCustomPlugins privilege required)
        /// </summary>
        protected virtual bool BypassCustomPluginExecution => false;

        /// <summary>
        /// Customize record sent to target (ex : value mapping)
        /// </summary>
        /// <param name="sourceRecord">Record retrieved from source</param>
        /// <param name="targetRecord">Filtered attributes sent to target</param>
        protected virtual void TransformRecord(Entity sourceRecord, Entity targetRecord)
        {
            // Nothing to do here
        }

        /// <summary>
        /// Run the synchronization (2 passes if TwoPassDependencies is enabled)
        /// </summary>
        /// <returns></returns>
        public override bool Run()
        {
            if (!TwoPassDependencies)
            {
                return base.Run();
            }

            // Pass 1 : referenced records may not exist yet on target
            currentPass = 1;
            Logger.LogInformation("Synchronization pass 1/2 : without lookups", ContextProperties);
            base.Run();

            currentPass = 2;
            Logger.LogInformation("Synchronization pass 2/2 : with lookups", ContextProperties);
            return base.Run();
        }

        /// <summary>
        /// One progress file per pass
        /// </summary>
        protected override string GetProgressFilePath()
        {
            var filePath = base.GetProgressFilePath();
            return TwoPassDependencies ? $"{Path.ChangeExtension(filePath, null)}_Pass{currentPass}.txt" : filePath;
        }

        /// <summary>
        /// Retrieve records from source connection (once for both passes)
        /// </summary>
        protected override List<Entity> RetrieveRecords()
        {
            if (sourceRecords != null)
            {
                return sourceRecords;
            }

            var sourceConnectionName = GetSourceConnectionName();
            if (string.IsNullOrEmpty(sourceConnectionName))
            {
                throw new Exception("Source connection is not defined : add Source.Connection.Name setting!");
            }
            if (string.Equals(sourceConnectionName, JobSettings.SelectedConnectionName, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Source and target connections must be different! ('{sourceConnectionName}')");
            }

            var sourceProxiesPool = Connections.GetProxiesPool(sourceConnectionName);
            var query = GetQuery(sourceProxiesPool.MainProxy.CallerId);
            query.PageInfo.Count = JobSettings.QueryRecordLimit;
            query.NoLock = true;

            excludedAttributes = new HashSet<string>(GetExcludedAttributes(), StringComparer.OrdinalIgnoreCase);
            targetAttributes = GetTargetAttributes(query.EntityName);

            Logger.LogInformation($"Retrieving '{query.EntityName}' records from '{sourceConnectionName}' to synchronize with '{JobSettings.SelectedConnectionName}'", ContextProperties);
            sourceRecords = sourceProxiesPool.MainProxy.RetrieveAll(query).Entities.ToList();
            return sourceRecords;
        }

        /// <summary>
        /// Upsert record on target
        /// </summary>
        /// <param name="context"></param>
        public override void ProcessRecord(JobExecutionContext context)
        {
            var includeEntityReferences = TwoPassDependencies ? (currentPass == 2) : IncludeEntityReferences;
            var targetRecord = BuildTargetRecord(context.Record, excludedAttributes, targetAttributes, includeEntityReferences);
            TransformRecord(context.Record, targetRecord);
            Execute(context, new UpsertRequest { Target = targetRecord });

            // State is applied once (last pass)
            if (SyncState && (!TwoPassDependencies || currentPass == 2))
            {
                var stateRecord = new Entity(context.Record.LogicalName, context.Record.Id);
                foreach (var stateAttribute in StateAttributes.Where(a => context.Record.Contains(a) && targetAttributes.Contains(a)))
                {
                    stateRecord[stateAttribute] = context.Record[stateAttribute];
                }
                if (stateRecord.Attributes.Count > 0)
                {
                    Execute(context, new UpdateRequest { Target = stateRecord });
                }
            }
        }

        private void Execute(JobExecutionContext context, OrganizationRequest request)
        {
            if (BypassCustomPluginExecution)
            {
                request.Parameters["BypassCustomPluginExecution"] = true;
            }
            context.Proxy.Execute(request);
        }

        /// <summary>
        /// Attributes valid for update on target table
        /// </summary>
        private HashSet<string> GetTargetAttributes(string entityName)
        {
            var request = new RetrieveEntityRequest
            {
                LogicalName = entityName,
                EntityFilters = EntityFilters.Attributes
            };
            var response = (RetrieveEntityResponse)ProxiesPool.MainProxy.Execute(request);
            var attributes = response.EntityMetadata.Attributes.Where(a => a.IsValidForUpdate == true).Select(a => a.LogicalName);
            return new HashSet<string>(attributes, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parse excluded attributes setting (comma separated, empty value : no exclusion)
        /// </summary>
        internal static IEnumerable<string> ParseExcludedAttributes(string setting)
        {
            if (setting == null)
            {
                return DefaultExcludedAttributes;
            }
            return setting.Split(',').Select(a => a.Trim()).Where(a => a.Length > 0).ToList();
        }

        /// <summary>
        /// Build record sent to target with filtered source attributes
        /// </summary>
        internal static Entity BuildTargetRecord(Entity sourceRecord, ISet<string> excludedAttributes, ISet<string> targetAttributes, bool includeEntityReferences)
        {
            var targetRecord = new Entity(sourceRecord.LogicalName, sourceRecord.Id);
            foreach (var attribute in sourceRecord.Attributes)
            {
                var value = attribute.Value;
                if (StateAttributes.Contains(attribute.Key)
                    || excludedAttributes.Contains(attribute.Key)
                    // Missing or read only on target
                    || !targetAttributes.Contains(attribute.Key)
                    // Linked entities columns
                    || value is AliasedValue)
                {
                    continue;
                }
                if (!includeEntityReferences && (value is EntityReference || value is EntityCollection))
                {
                    continue;
                }
                targetRecord[attribute.Key] = value;
            }
            return targetRecord;
        }
    }
}
