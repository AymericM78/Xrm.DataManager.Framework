using Microsoft.Xrm.Sdk;
using System.Collections.Generic;

namespace Xrm.DataManager.Framework
{
    /// <summary>
    /// Comparison status of a record between source and target connections
    /// </summary>
    public enum DataCompareStatus
    {
        /// <summary>
        /// Record exists only on source (missing on target)
        /// </summary>
        OnlyInSource,

        /// <summary>
        /// Record exists only on target (missing on source)
        /// </summary>
        OnlyInTarget,

        /// <summary>
        /// Record exists on both connections with different values
        /// </summary>
        Different,

        /// <summary>
        /// Record exists on both connections with same values
        /// </summary>
        Equal
    }

    /// <summary>
    /// Comparison result of a record between source and target connections
    /// </summary>
    public class DataCompareResult
    {
        /// <summary>
        /// Matching key (default : record id)
        /// </summary>
        public string Key { get; }

        public DataCompareStatus Status { get; }

        /// <summary>
        /// Source record (null if OnlyInTarget)
        /// </summary>
        public Entity SourceRecord { get; }

        /// <summary>
        /// Target record (null if OnlyInSource)
        /// </summary>
        public Entity TargetRecord { get; }

        /// <summary>
        /// Attributes with different values (Different status)
        /// </summary>
        public IReadOnlyList<string> DifferentAttributes { get; }

        public DataCompareResult(string key, DataCompareStatus status, Entity sourceRecord, Entity targetRecord, IReadOnlyList<string> differentAttributes = null)
        {
            Key = key;
            Status = status;
            SourceRecord = sourceRecord;
            TargetRecord = targetRecord;
            DifferentAttributes = differentAttributes ?? new List<string>();
        }
    }
}
