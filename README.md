# Xrm.DataManager.Framework

Nuget framework for massive Microsoft Dataverse data operations.

<p align="center">
    <a href="https://github.com/AymericM78/Xrm.DataManager.Framework/actions/workflows/build.yml" alt="Build">
        <img src="https://github.com/AymericM78/Xrm.DataManager.Framework/actions/workflows/build.yml/badge.svg" alt="Build" /></a>
    <a href="https://www.nuget.org/packages/Xrm.DataManager.Framework/" alt="NuGet">
        <img src="https://img.shields.io/nuget/v/Xrm.DataManager.Framework?label=NuGet" alt="NuGet" /></a>
    <a href="https://github.com/AymericM78/Xrm.DataManager.Framework/blob/master/LICENSE" alt="Repository License">
        <img src="https://img.shields.io/github/license/AymericM78/Xrm.DataManager.Framework?color=yellow&label=License" alt="License" /></a>
    <a href="https://github.com/AymericM78/Xrm.DataManager.Framework/issues" alt="Open Issues">
        <img src="https://img.shields.io/github/issues-raw/AymericM78/Xrm.DataManager.Framework?label=Open%20Issues" alt="Open Issues" /></a>
    <a href="https://github.com/AymericM78/Xrm.DataManager.Framework/pulls" alt="Open Pull Requests">
        <img src="https://img.shields.io/github/issues-pr-raw/AymericM78/Xrm.DataManager.Framework?label=Open%20Pull%20Requests" alt="Open Pull Requests" /></a>
</p>

## 🚀 Features

`Xrm.DataManager.Framework` provides following features :

- **Batching patterns** : Full, Delta, ...
- **Performances** : Perf tuning, multi threading, SQL retrieval (TDS endpoint)...
- **Errors handling and retry** : API Limits catching and waits
- **Tracing** : Supports file output, App Insights and any Microsoft.Extensions.Logging provider...
- **Configuration management** : Simplified conf ...

`Xrm.DataManager.Framework` is designed to simplify dev work by reducing core development and focus to functional implementation.

## 👐 Community contributions

This project is open to community contributions in order to enrich this tooling and helps everyone to bring more capabilities for Microsoft Dataverse data operations.

## ✈️ Installation and configuration

### Installation

Create a new C# console app project (.NET Framework 4.8 or .NET 10) and add [Xrm.DataManager.Framework Nuget package](https://www.nuget.org/packages/Xrm.DataManager.Framework/).

Package|Description
-------|-----------
`Xrm.DataManager.Framework`|Core framework (only depends on Dataverse ServiceClient)
`Xrm.DataManager.Framework.ApplicationInsights`|Optional : Application Insights logger
`Xrm.DataManager.Framework.Tds`|Optional : retrieve records with SQL (Dataverse TDS endpoint)

Packages follow semantic versioning (major version changes on breaking changes). Versions start at 2026.0.0 : previous versions were date based (2022.x).

⚠️ Upgrading from 2022.x versions : see [upgrade guide](https://github.com/AymericM78/Xrm.DataManager.Framework/blob/master/UPGRADE.md).

### Connections

Connections are defined in `connectionStrings` section of 'app.config' (`configSource` attribute allows to keep them in a separate file, out of source control) :

```xml
<connectionStrings>
  <!-- Dataverse instances : ServiceClient format (OAuth, ClientSecret or Certificate) -->
  <add name="Dev" connectionString="AuthType=ClientSecret;Url=https://dev.crm4.dynamics.com;ClientId=...;ClientSecret=..." />
  <add name="Prod" connectionString="AuthType=ClientSecret;Url=https://prod.crm4.dynamics.com;ClientId=...;ClientSecret=..." />
  <!-- Optional : Application Insights (reserved name) -->
  <add name="AppInsights" connectionString="InstrumentationKey=...;IngestionEndpoint=https://..." />
</connectionStrings>
```

- Dataverse connection strings use [ServiceClient format](https://learn.microsoft.com/power-apps/developer/data-platform/xrm-tooling/use-connection-strings-xrm-tooling-connect) : OAuth, ClientSecret or Certificate (`Office365` / `IFD` are no longer supported).
- Target connection (where jobs run) is resolved in following order :
  1. First command line argument (`Processor` : `YourApp.exe Dev Job1,Job2`) or `Target.Connection.Name` setting
  2. Single Dataverse entry, or console selection if several entries are defined
- Other entries are available in jobs with `Connections.GetProxiesPool("Prod")` (opened on first use).
- Application Insights uses `AppInsights` entry, or `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable (Azure).

### Settings

Following parameters can be specified in `appSettings` section of 'app.config' :

Parameter name|Default or available values|Description
--------------|---------------------------|-----------
Target.Connection.Name|ex: Dev|Name of the `connectionStrings` entry where jobs run
Target.Job.Names|ex: DeleteAccountsJobs, DeleteContactsJob|Jobs to run with `Processor` (class names separated with ',')
Source.Connection.Name|ex: Prod|Name of the `connectionStrings` entry to read records from (`CrossInstanceDataJobBase`, `SyncDataJobBase`, `CompareDataJobBase`)
Sync.ExcludedAttributes|Default: createdon, modifiedon, createdby, modifiedby, createdonbehalfby, modifiedonbehalfby, ownerid, owningbusinessunit, owninguser, owningteam, transactioncurrencyid|Attributes never copied by `SyncDataJobBase` (separated with ',', replaces default list, empty value : no exclusion)
Compare.ExcludedAttributes|Default: same as `Sync.ExcludedAttributes` + versionnumber, importsequencenumber, overriddencreatedon, timezoneruleversionnumber, utcconversiontimezonecode, exchangerate|Attributes never compared by `CompareDataJobBase` (separated with ',', replaces default list, empty value : no exclusion)
Process.Duration.MaxHours|Default: 48|Max job duration, to prevent night batch to run in prod during the day
Process.Query.RecordLimit|Default: 2500|Records count to retrieve for each page (max 5000)
Process.Thread.Number|Default: 5|Specify the number of threads to run (server recommended value is displayed at startup)
Process.ConnectionOptimizations.Enabled|Default: true|Apply process wide connection settings (thread pool, .NET Framework connection limit). Disable if your application manages them
Crm.AffinityCookie.Enabled|Default: false|Keep all requests on the same Dataverse node. Disabled by default to spread parallel requests on all nodes
LogLevel|Verbose = 0,<br/>Information = 1,<br/>ErrorsAndSuccess = 2,<br/>ErrorsOnly = 3|Tracing level
Logging.Record.Attributes|Default: false|Trace all record columns in telemetry (record logical name and id are always traced). Keep disabled if records contain personal data
Tds.Port|Default: 1433|TDS endpoint port (1433 or 5558, depending on your firewall)
Tds.CommandTimeout.Seconds|Default: 300|SQL query timeout (TDS endpoint limit is 5 minutes, 2 minutes for `SELECT *` and joins)

### Logging

Logger is provided to processor constructor (default : `FileLogger`, writes `{RunId}.log` in current directory) :

```csharp
// Application Insights (Xrm.DataManager.Framework.ApplicationInsights package)
using (var processor = new JobProcessor(settings => new ApplicationInsightLogger(settings)))
{
    processor.Execute<MyDataJob>();
}

// Any Microsoft.Extensions.Logging provider (Serilog, NLog, OpenTelemetry...)
using (var processor = new JobProcessor(settings => new ExtensionsLogger(settings, loggerFactory.CreateLogger("DataManager"))))
{
    processor.Execute<MyDataJob>();
}
```

### Usage

7 patterns are available:

- `PickAndProcessDataJobBase` : Query X rows from given QueryExpression and process them then loop as long as data is retrieved.
- `FullProcessDataJobBase` : Query all rows from given QueryExpression, load all data in memory and process.
- `SqlFullProcessDataJobBase` (`Xrm.DataManager.Framework.Tds` package) : Same as `FullProcessDataJobBase` with rows retrieved by a SQL query.
- `CrossInstanceDataJobBase` : Query rows on a source instance and process them on the target instance.
- `SyncDataJobBase` : Copy rows from a source instance to the target instance.
- `CompareDataJobBase` : Compare rows between a source instance and the target instance (only in source, only in target, different, equal).
- `InputFileProcessDataJobBase` : Map a csv or txt file to table/columns and process each line.

These patterns provide following overridable methods:

- GetName : Set a display name for job.
- IsEnabled : Specify if job is runnable or not.
- OverrideThreadNumber : Specify thread number for current job (For debug only)
- PreOperation : This method is called before proceeding data, you can use it to put data in cache for example or disable audit or whatever.
- GetQuery : Specify the query expression to retrieve data.
- PrepareData : Useful with `FullProcessDataJobBase` to apply data aggregation like groupings.
- ProcessRecord : Apply custom operation for a single row.
- PostOperation : This method is called after all data have been processed, you can use it to send notification email or reactivate audit or whatever.

### SQL retrieval (TDS endpoint)

`SqlFullProcessDataJobBase` retrieves records with a SQL query on [Dataverse TDS endpoint](https://learn.microsoft.com/power-apps/developer/data-platform/dataverse-sql-query) (read only, `Enable TDS endpoint` environment setting is required). Authentication uses the access token of the Dataverse connection.

```csharp
public class AnonymizeContactsJob : SqlFullProcessDataJobBase
{
    public AnonymizeContactsJob(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters) { }

    public override string GetName() => "Anonymize contacts";

    // contactid is added automatically when missing
    public override string GetSqlQuery() => "SELECT TOP 1000 emailaddress1, createdby, createdbyname FROM contact WHERE emailaddress1 LIKE '%.com'";

    public override void ProcessRecord(JobExecutionContext context)
    {
        var email = context.Record.GetSqlValue<string>("emailaddress1");
        var creator = context.Record.GetSqlEntityReference("createdby", "systemuser");

        // SQL columns are not in Attributes : only modified columns are sent
        context.Record["emailaddress1"] = email.Replace(".com", ".fake");
        context.Proxy.Update(context.Record);
    }
}
```

- The query is parsed before execution : it must be a single `SELECT` statement. Primary key column of the records table (first table of `FROM` clause, or `GetSqlEntityName()`) is added if missing and used as record id.
- Other columns are stored in `Record.FormattedValues` (culture invariant strings) and read with `GetSqlValue<T>(column)` : `Attributes` stay empty so the record can be updated without sending SQL columns back.
- Lookups are returned as `{column}` / `{column}name`, choices as `{column}` / `{column}name`, dates are UTC.
- SQL queries don't trigger RetrieveMultiple plugins, elastic tables aren't supported, avoid `NOLOCK` hint.
- `TdsClient` can also be used directly (ex : `PreOperation`) to retrieve records or open a SQL connection.

### Multi instance processing

`CrossInstanceDataJobBase` retrieves records from source connection (`Source.Connection.Name`) and processes them on target connection :

```csharp
public class DeleteDeactivatedAccountsDataJob : CrossInstanceDataJobBase
{
    public DeleteDeactivatedAccountsDataJob(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters) { }

    public override string GetName() => "Delete accounts deactivated on source instance";

    // Executed on source connection
    public override QueryExpression GetQuery(Guid callerId)
    {
        var query = new QueryExpression("account");
        query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 1);
        return query;
    }

    // Record : source record / Proxy : target connection
    public override void ProcessRecord(JobExecutionContext context) => context.Proxy.Delete(context.Record.LogicalName, context.Record.Id);
}
```

- Multi threading, progress file (resume), errors handling and logging are the same as `FullProcessDataJobBase`.
- `SourceProxiesPool` gives access to the source connection (ex : `TdsClient` to retrieve source records with SQL in `RetrieveRecords` override).
- Any job can also use other connections with `Connections.GetProxiesPool("Name")`.

### Synchronization between instances

`SyncDataJobBase` (specialized `CrossInstanceDataJobBase`) reads records from source connection (`Source.Connection.Name`) and upserts them on target connection with the same id :

```csharp
public class SyncAccountsDataJob : SyncDataJobBase
{
    public SyncAccountsDataJob(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters) { }

    public override string GetName() => "Copy active accounts";

    // Executed on source connection
    public override QueryExpression GetQuery(Guid callerId)
    {
        var query = new QueryExpression("account") { ColumnSet = new ColumnSet(true) };
        query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
        return query;
    }

    // Job specific exclusions (in addition to Sync.ExcludedAttributes setting or default list)
    public override IEnumerable<string> GetExcludedAttributes() => base.GetExcludedAttributes().Concat(new[] { "primarycontactid" });

    // Parent accounts are synchronized by the same job : lookups are set in a second pass
    protected override bool TwoPassDependencies => true;
}
```

- Copied attributes : retrieved by `GetQuery`, not excluded, existing and valid for update on target table (target metadata).
- Options (overridable properties) : `IncludeEntityReferences` (copy lookups, default false), `TwoPassDependencies` (records without lookups, then lookups), `SyncState` (apply statecode / statuscode after upsert), `BypassCustomPluginExecution`.
- `TransformRecord(sourceRecord, targetRecord)` allows to change values before upsert.
- Multi threading, progress file (resume), errors handling and logging are the same as `FullProcessDataJobBase`.

### Comparison between instances

`CompareDataJobBase` retrieves records from source connection (`Source.Connection.Name`) and target connection (simultaneously), then compares them :

```csharp
public class CompareContactsDataJob : CompareDataJobBase
{
    public CompareContactsDataJob(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters) { }

    public override string GetName() => "Compare contacts";

    // Executed on both connections (override GetTargetQuery for a different target query)
    public override QueryExpression GetQuery(Guid callerId)
    {
        var query = new QueryExpression("contact");
        query.ColumnSet.AddColumns("fullname", "emailaddress1", "telephone1", "parentcustomerid");
        return query;
    }

    // Optional : delete on target the contacts missing on source
    protected override bool ShouldProcess(DataCompareResult result) => result.Status == DataCompareStatus.OnlyInTarget;

    protected override void ProcessResult(JobExecutionContext context, DataCompareResult result) => context.Proxy.Delete("contact", result.TargetRecord.Id);
}
```

- Each record gets a status : `OnlyInSource`, `OnlyInTarget`, `Different` (with different attributes) or `Equal`. A summary is logged and a CSV report is written (`{JobClassName}_{RunId}.csv`, equal records excluded by default : `ReportEqualRecords`).
- Records are matched by id (instances synchronized with `SyncDataJobBase` keep the same ids) : override `GetMatchingKey(record)` to use another key (ex : account number).
- Compared attributes : retrieved by the queries, not excluded (`Compare.ExcludedAttributes`). Lookups are compared by id, choices and money by value, empty string equals null : override `AreEqual(attribute, sourceValue, targetValue)` to change it.
- Read only by default : `ShouldProcess` / `ProcessResult` allow to process selected results on target connection (multi threaded, same errors handling and logging as other patterns).
- Both tables are loaded in memory : restrict columns and rows in queries.
