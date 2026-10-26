# Upgrade guide : 2022.x ➜ 2026.0.0

Version 2026.0.0 is a major release : the framework now relies on the Dataverse ServiceClient, its dependencies are reduced to the minimum and the configuration has been simplified. This guide lists every change required to upgrade an application using `Xrm.DataManager.Framework` 2022.x.

> Versioning : packages now follow semantic versioning starting at 2026.0.0 (a lower major version such as 3.0.0 would be considered older than 2022.x by NuGet).

## Checklist

1. [Update packages and target framework](#1-packages-and-target-framework)
2. [Move connections to `connectionStrings`](#2-connections)
3. [Rename or remove settings](#3-settings)
4. [Choose the logger in code](#4-logging)
5. [Fix compilation errors](#5-api-changes)
6. [Check behavior changes](#6-behavior-changes) and test on a development instance first

## 1. Packages and target framework

Before|After
------|-----
`lib/net462`|`lib/net48` and `lib/net10.0` : application must target .NET Framework 4.8 or .NET 10
`Microsoft.CrmSdk.CoreAssemblies`, `Microsoft.CrmSdk.XrmTooling.CoreAssembly`, `Microsoft.CrmSdk.Deployment`, `Microsoft.CrmSdk.Workflow`, `Microsoft.CrmSdk.Extensions`, `Microsoft.IdentityModel.Clients.ActiveDirectory`|`Microsoft.PowerPlatform.Dataverse.Client` (`Microsoft.Xrm.Sdk` namespaces are unchanged)
`Microsoft.ApplicationInsights` 2.x, `Serilog`, `Serilog.Sinks.Console`, `Serilog.Sinks.File`, `Newtonsoft.Json`|Removed from core package
Application Insights logger in core package|`Xrm.DataManager.Framework.ApplicationInsights` package (optional)
—|`Xrm.DataManager.Framework.Tds` package (optional) : SQL retrieval

Steps :

- Remove `Microsoft.CrmSdk.*` and `Microsoft.IdentityModel.Clients.ActiveDirectory` packages from your application if they were referenced directly.
- Add `Xrm.DataManager.Framework.ApplicationInsights` if you use Application Insights.
- Dependencies are resolved transitively : SDK style projects (`<PackageReference>`) are recommended. Remove manual binding redirects from 'app.config' and let the build generate them (`AutoGenerateBindingRedirects`, enabled by default for SDK style executables).

## 2. Connections

`Crm.ConnectionString` setting, `Instances.xml` file and `Refresh-InstanceList.ps1` script are removed : Dataverse connections are defined in `connectionStrings` section.

Before :

```xml
<appSettings>
  <add key="Crm.ConnectionString" value="AuthType=Office365;Url=https://dev.crm4.dynamics.com;Username=...;Password=..." />
</appSettings>
```

After :

```xml
<connectionStrings>
  <add name="Dev" connectionString="AuthType=ClientSecret;Url=https://dev.crm4.dynamics.com;ClientId=...;ClientSecret=..." />
  <add name="Prod" connectionString="AuthType=ClientSecret;Url=https://prod.crm4.dynamics.com;ClientId=...;ClientSecret=..." />
  <add name="AppInsights" connectionString="InstrumentationKey=...;IngestionEndpoint=https://..." />
</connectionStrings>
<appSettings>
  <add key="Target.Connection.Name" value="Dev" />
</appSettings>
```

- `AuthType=Office365` and `AuthType=IFD` are not supported by the ServiceClient : use `ClientSecret` or `Certificate` (application user) or `OAuth` ([connection string format](https://learn.microsoft.com/power-apps/developer/data-platform/xrm-tooling/use-connection-strings-xrm-tooling-connect)).
- Each former `Instances.xml` entry becomes a `connectionStrings` entry (entry name = former instance name).
- Target connection : first command line argument (`Processor`), `Target.Connection.Name` setting, single entry, or console selection.
- `AppInsights` is a reserved name (not a Dataverse connection).
- Use `configSource` attribute to keep connection strings out of source control : `<connectionStrings configSource="ConnectionStrings.config" />`.

## 3. Settings

Renamed or removed `appSettings` keys :

Before|After
------|-----
`Crm.ConnectionString`, `Xrm`|`connectionStrings` entry
`Crm.Instance.Name`|`Target.Connection.Name`
`Job.Names`|`Target.Job.Names`
`Crm.User.Name`, `Crm.User.Password`|Removed : credentials are part of the connection string
`AppInsights.ConnectionString`|`connectionStrings` entry named `AppInsights` (or `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable)
`AppInsights.Instrumentation.Key`, `APPINSIGHTS_INSTRUMENTATIONKEY`|Removed : instrumentation key ingestion is retired, use the Application Insights connection string
`Graylog.Url`|Removed (see [Logging](#4-logging))

New settings (all optional) :

Key|Default|Description
---|-------|-----------
`Source.Connection.Name`|—|Connection to read records from (`CrossInstanceDataJobBase`, `SyncDataJobBase`, `CompareDataJobBase`)
`Sync.ExcludedAttributes`|System and ownership columns|Attributes never copied by `SyncDataJobBase`
`Compare.ExcludedAttributes`|System, ownership and technical columns|Attributes never compared by `CompareDataJobBase`
`Logging.Record.Attributes`|`false`|Trace record columns in telemetry
`Crm.AffinityCookie.Enabled`|`false`|Keep requests on the same Dataverse node
`Process.ConnectionOptimizations.Enabled`|`true`|Apply process wide connection settings
`Tds.Port`, `Tds.CommandTimeout.Seconds`|`1433`, `300`|SQL retrieval (`Xrm.DataManager.Framework.Tds`)

## 4. Logging

The logger is now chosen in code, by the processor constructor. Without parameter, `FileLogger` is used (even if Application Insights is configured : a warning is displayed at startup).

Before :

```csharp
var processor = new JobProcessor(); // Application Insights used when instrumentation key was defined
processor.Execute<MyDataJob>();
```

After :

```csharp
// Xrm.DataManager.Framework.ApplicationInsights package
using (var processor = new JobProcessor(settings => new ApplicationInsightLogger(settings)))
{
    processor.Execute<MyDataJob>();
}
```

- `GrayLogger` is removed : use `ExtensionsLogger` with any Microsoft.Extensions.Logging provider (ex : Serilog with a Graylog sink).
- `FileLogger` no longer uses Serilog : same `{RunId}.log` file, successes are written as `[SUC]` (formerly `[WRN]`).
- `ILogger` has a new `Flush()` method : add it if you implement `ILogger` without inheriting `BaseLogger`.
- Application Insights SDK 3.x (OpenTelemetry based) : telemetry is flushed at the end of each job.

## 5. API changes

Before|After
------|-----
`IManagedTokenOrganizationServiceProxy.CrmServiceClient` (`CrmServiceClient`)|`ServiceClient` (`Microsoft.PowerPlatform.Dataverse.Client.ServiceClient`), `CrmServiceClient` is kept as obsolete alias
`CrmServiceClient.LastCrmException`, `LastCrmError`, `GetMyCrmUserId()`|`ServiceClient.LastException`, `LastError`, `GetMyUserId()` (`Microsoft.PowerPlatform.Dataverse.Client.Extensions`)
`ProxiesPool.GetProxy()` then `proxy.Dispose()`|`ProxiesPool.GetProxy()` then `ProxiesPool.ReleaseProxy(proxy)` to reuse connections (`Dispose()` still works)
`ProxiesPool.UserName`, `ProxiesPool.Password`|Removed
`new JobProcessParameters { ProxiesPool, Logger, CallerId }` in custom processors|`CreateJobProcessParameters()` (also provides `Connections`)
`ProcessorBase.InitializeOrganizationServiceManager(Instance)`|`InitializeOrganizationServiceManager()` (configuration) or `InitializeOrganizationServiceManager(string connectionString)`
`Instance`, `InstanceList`, `InstancePicker`, `ConsoleHelper.InstanceSelect`|Removed (`ConsoleHelper.ConnectionStringSelect`)
`JobSettings.CrmConnectionString`, `ConnectionStringDefined`|Removed (`SelectedConnectionString`)
`JobSettings.CrmInstanceName`, `CrmInstanceNameDefined`|`TargetConnectionName`
`JobSettings.SelectedInstanceName`|`SelectedConnectionName`
`JobSettings.JobNames`, `JobNamesDefined`|`TargetJobNames`, `TargetJobNamesDefined`
`JobSettings.CrmUserName`, `CrmUserPassword`, `AppInsightsInstrumentationKey`, `GrayLogUrl`, `GrayLogEnabled`|Removed
`GrayLogger`|Removed

Processors (`JobProcessor`, `Processor`, `ProcessorBase`) are now `IDisposable` : use them in a `using` block to close Dataverse connections.

New extension points :

- `DataJobBase.Connections` : other connections by name (`Connections.GetProxiesPool("Prod")`), `RetrieveAll(proxiesPool, query)` helper.
- `FullProcessDataJobBase.RetrieveRecords()` and `GetProgressFilePath()` are overridable.
- `CrossInstanceDataJobBase` : query records on a source instance and process them on the target instance.
- `SyncDataJobBase` : copy records between instances.
- `CompareDataJobBase` : compare records between instances (CSV report, optional processing of results).
- `SqlFullProcessDataJobBase` and `TdsClient` (`Xrm.DataManager.Framework.Tds`) : retrieve records with SQL.

## 6. Behavior changes

Change|Impact
------|------
`callerId` parameter of `GetQuery` is now the connected user id (it was always `Guid.Empty`)|Check queries using `callerId` (ex : condition on `ownerid`)
Record columns are no longer sent to telemetry|Set `Logging.Record.Attributes=true` to restore (personal data)
Connection string secrets (`Password`, `ClientSecret`) are masked in logs|—
Affinity cookie disabled : parallel requests are spread on all Dataverse nodes|Set `Crm.AffinityCookie.Enabled=true` to restore
TLS version is no longer forced to 1.2 (operating system default, TLS 1.3 allowed)|—
Only network errors and service protection limits are retried (generic exceptions were retried with a 30-60 s delay), `Retry-After` delay is honored|Faster failures on functional errors
`JobExecutionContext.PushMetric` overwrites existing keys (formerly an exception)|—
`InputFileProcessDataJobBase` : `context.Record` contains the record found by `SearchRecord` (formerly always null)|—
Proxies are reused between batches (pool) instead of being cloned for each batch|—
Default `Process.Thread.Number` is 5 (documentation said 10)|Documentation fix only
