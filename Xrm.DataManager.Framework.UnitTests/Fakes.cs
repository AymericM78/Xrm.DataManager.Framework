using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Xrm.DataManager.Framework.UnitTests
{
    internal class FakeProxy : IManagedTokenOrganizationServiceProxy
    {
        public bool Disposed { get; private set; }
        public string ActiveAuthenticationType { get; set; } = "ClientSecret";
        public Guid CallerId { get; set; } = Guid.NewGuid();
        public string ConnectedOrgFriendlyName { get; set; } = "Org";
        public ServiceClient ServiceClient { get; set; }
        public ServiceClient CrmServiceClient => ServiceClient;
        public string EndpointUrl { get; set; } = "https://org.crm4.dynamics.com";
        public EntityCollection RetrieveAll(QueryExpression query) => throw new NotSupportedException();
        public Guid Create(Entity entity) => throw new NotSupportedException();
        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => throw new NotSupportedException();
        public void Update(Entity entity) => throw new NotSupportedException();
        public void Delete(string entityName, Guid id) => throw new NotSupportedException();
        public OrganizationResponse Execute(OrganizationRequest request) => throw new NotSupportedException();
        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();
        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();
        public EntityCollection RetrieveMultiple(QueryBase query) => throw new NotSupportedException();
        public void Dispose() => Disposed = true;
    }

    internal class MemoryLogger : BaseLogger
    {
        public List<string> Messages { get; } = new List<string>();

        public MemoryLogger() : base(new JobSettings())
        {
        }

        public override void LogDisplay(string message) { }
        public override void LogFailure(Exception exception, Dictionary<string, string> properties) => Messages.Add($"FAILURE {exception.Message}");
        public override void LogInformation(string message, bool display = true) => Messages.Add($"INFO {message}");
        public override void LogInformation(string message, Dictionary<string, string> properties, bool display = true) => LogInformation(message, display);
        public override void LogSuccess(string message, Dictionary<string, string> properties) => Messages.Add($"SUCCESS {message}");
        public override void LogVerbose(string message) => Messages.Add($"VERBOSE {message}");
    }

    /// <summary>
    /// SQL result without rows : allows column names refused by DataTable (duplicates, empty)
    /// </summary>
    internal class ColumnsOnlyDataReader : System.Data.Common.DbDataReader
    {
        private readonly string[] columns;

        public ColumnsOnlyDataReader(params string[] columns) => this.columns = columns;

        public override int FieldCount => columns.Length;
        public override string GetName(int ordinal) => columns[ordinal];
        public override bool Read() => false;
        public override bool HasRows => false;
        public override bool IsClosed => false;
        public override int Depth => 0;
        public override int RecordsAffected => -1;
        public override bool NextResult() => false;
        public override object this[int ordinal] => throw new NotSupportedException();
        public override object this[string name] => throw new NotSupportedException();
        public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
        public override byte GetByte(int ordinal) => throw new NotSupportedException();
        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override char GetChar(int ordinal) => throw new NotSupportedException();
        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override string GetDataTypeName(int ordinal) => throw new NotSupportedException();
        public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();
        public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
        public override double GetDouble(int ordinal) => throw new NotSupportedException();
        public override System.Collections.IEnumerator GetEnumerator() => throw new NotSupportedException();
        public override Type GetFieldType(int ordinal) => throw new NotSupportedException();
        public override float GetFloat(int ordinal) => throw new NotSupportedException();
        public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
        public override short GetInt16(int ordinal) => throw new NotSupportedException();
        public override int GetInt32(int ordinal) => throw new NotSupportedException();
        public override long GetInt64(int ordinal) => throw new NotSupportedException();
        public override int GetOrdinal(string name) => throw new NotSupportedException();
        public override string GetString(int ordinal) => throw new NotSupportedException();
        public override object GetValue(int ordinal) => throw new NotSupportedException();
        public override int GetValues(object[] values) => throw new NotSupportedException();
        public override bool IsDBNull(int ordinal) => throw new NotSupportedException();
    }

    /// <summary>
    /// Microsoft.Extensions.Logging logger keeping entries with current scope
    /// </summary>
    internal class CapturingExtensionsLogger : Microsoft.Extensions.Logging.ILogger
    {
        private readonly Stack<object> scopes = new Stack<object>();

        public List<string> Entries { get; } = new List<string>();

        public IDisposable BeginScope<TState>(TState state)
        {
            scopes.Push(state);
            return new ScopeHandle(scopes);
        }

        public bool IsEnabled(MsLogLevel logLevel) => true;

        public void Log<TState>(MsLogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var scope = (scopes.Count > 0 && scopes.Peek() is IEnumerable<KeyValuePair<string, object>> properties)
                ? string.Join(",", properties.Select(p => $"{p.Key}={p.Value}"))
                : string.Empty;
            Entries.Add($"{logLevel}|{eventId.Name}|{formatter(state, exception)}|{exception?.Message}|{scope}");
        }

        private class ScopeHandle : IDisposable
        {
            private readonly Stack<object> scopes;
            public ScopeHandle(Stack<object> scopes) => this.scopes = scopes;
            public void Dispose() => scopes.Pop();
        }
    }
}
