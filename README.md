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

### Configuration

You need to specify following parameters in 'app.config':

Parameter name|Default or available values|Description
--------------|---------------------------|-----------
Crm.ConnectionString|ex: `AuthType=ClientSecret;Url=https://org.crm.dynamics.com;ClientId=...;ClientSecret=...`|Specify connection string to Microsoft Dataverse ([Dataverse ServiceClient format](https://learn.microsoft.com/power-apps/developer/data-platform/xrm-tooling/use-connection-strings-xrm-tooling-connect) : OAuth, ClientSecret or Certificate, `Office365` / `IFD` are no longer supported)
Crm.Instance.Name|ex: Dev|Name of the `connectionStrings` entry to use (see below)
Job.Names|ex: DeleteAccountsJobs, DeleteContactsJob|Specify if you want to run multiple jobs (class names separated with ',')
Process.Duration.MaxHours|Default: 48|Max job duration, to prevent night batch to run in prod during the day
Process.Query.RecordLimit|Default: 2500|Records count to retrieve for each page (max 5000)
Process.Thread.Number|Default: 5|Specify the number of threads to run (server recommended value is displayed at startup)
Process.ConnectionOptimizations.Enabled|Default: true|Apply process wide connection settings (thread pool, .NET Framework connection limit). Disable if your application manages them
Crm.AffinityCookie.Enabled|Default: false|Keep all requests on the same Dataverse node. Disabled by default to spread parallel requests on all nodes
LogLevel|Verbose = 0,<br/>Information = 1,<br/>ErrorsAndSuccess = 2,<br/>ErrorsOnly = 3|Tracing level
Logging.Record.Attributes|Default: false|Trace all record columns in telemetry (record logical name and id are always traced). Keep disabled if records contain personal data
AppInsights.ConnectionString|InstrumentationKey=...;IngestionEndpoint=...|Application Insights connection string (`AppInsights.Instrumentation.Key` is still read for compatibility but instrumentation key ingestion is retired)
Tds.Port|Default: 1433|TDS endpoint port (1433 or 5558, depending on your firewall)
Tds.CommandTimeout.Seconds|Default: 300|SQL query timeout (TDS endpoint limit is 5 minutes, 2 minutes for `SELECT *` and joins)

#### Multiple environments

Define one entry per Dataverse instance in `connectionStrings` section (`configSource` attribute allows to keep them in a separate file) :

```xml
<connectionStrings>
  <add name="Dev" connectionString="AuthType=ClientSecret;Url=https://dev.crm4.dynamics.com;ClientId=...;ClientSecret=..." />
  <add name="Uat" connectionString="AuthType=ClientSecret;Url=https://uat.crm4.dynamics.com;ClientId=...;ClientSecret=..." />
</connectionStrings>
```

Connection is resolved in following order :

1. Entry named by first command line argument (`Processor` : `YourApp.exe Dev Job1,Job2`) or `Crm.Instance.Name` setting
2. `Crm.ConnectionString` setting
3. Single `connectionStrings` entry, or console selection if several entries are defined

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

4 patterns are available:

- `PickAndProcessDataJobBase` : Query X rows from given QueryExpression and process them then loop as long as data is retrieved.
- `FullProcessDataJobBase` : Query all rows from given QueryExpression, load all data in memory and process.
- `SqlFullProcessDataJobBase` (`Xrm.DataManager.Framework.Tds` package) : Same as `FullProcessDataJobBase` with rows retrieved by a SQL query.
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
