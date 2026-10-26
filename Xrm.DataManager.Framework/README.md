# Xrm.DataManager.Framework

Framework for massive Microsoft Dataverse data operations (batch jobs, data migrations, purges, synchronizations...).

You write the query and the processing of a single record : the framework handles paging, multi threading, retries, progress and logging.

[![Build](https://github.com/AymericM78/Xrm.DataManager.Framework/actions/workflows/build.yml/badge.svg)](https://github.com/AymericM78/Xrm.DataManager.Framework/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Xrm.DataManager.Framework?label=NuGet)](https://www.nuget.org/packages/Xrm.DataManager.Framework/)
[![License](https://img.shields.io/github/license/AymericM78/Xrm.DataManager.Framework?color=yellow&label=License)](https://github.com/AymericM78/Xrm.DataManager.Framework/blob/master/LICENSE)

## Features

- **Batching patterns** : pick and process, full process (with resume), input file, SQL retrieval
- **Multi instance** : query an instance and process on another one, synchronize or compare tables between instances
- **Performances** : multi threading, connection pool, server recommended degree of parallelism displayed at startup
- **Errors handling** : network errors and service protection limits are retried (`Retry-After` delay honored)
- **Tracing** : file, Application Insights or any Microsoft.Extensions.Logging provider
- **Configuration** : connections and settings in `app.config`

Only depends on the Dataverse ServiceClient (`Microsoft.PowerPlatform.Dataverse.Client`).

## Getting started

### Prerequisites

- Console application targeting .NET Framework 4.8 or .NET 10
- Dataverse connection string in [ServiceClient format](https://learn.microsoft.com/power-apps/developer/data-platform/xrm-tooling/use-connection-strings-xrm-tooling-connect) : OAuth, ClientSecret or Certificate

### Configuration

Define connections and settings in `app.config` :

```xml
<configuration>
  <connectionStrings>
    <add name="Dev" connectionString="AuthType=ClientSecret;Url=https://dev.crm4.dynamics.com;ClientId=...;ClientSecret=..." />
  </connectionStrings>
  <appSettings>
    <add key="Target.Connection.Name" value="Dev" />
    <add key="Process.Thread.Number" value="10" />
  </appSettings>
</configuration>
```

### Write a job

```csharp
using Microsoft.Xrm.Sdk.Query;
using System;
using Xrm.DataManager.Framework;

public class DeleteInactiveContactsJob : PickAndProcessDataJobBase
{
    public DeleteInactiveContactsJob(JobSettings jobSettings, JobProcessParameters parameters) : base(jobSettings, parameters) { }

    public override string GetName() => "Delete inactive contacts";

    // Records are retrieved page by page, as long as the query returns records
    public override QueryExpression GetQuery(Guid callerId)
    {
        var query = new QueryExpression("contact");
        query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 1);
        return query;
    }

    // Called once per record, on several threads
    public override void ProcessRecord(JobExecutionContext context) => context.Proxy.Delete(context.Record.LogicalName, context.Record.Id);
}
```

### Run it

```csharp
// Default logger : FileLogger ({RunId}.log file in current directory)
using (var processor = new JobProcessor())
{
    processor.Execute<DeleteInactiveContactsJob>();
}
```

Or select connection and jobs from command line (`YourApp.exe Dev DeleteInactiveContactsJob,OtherJob`) or `Target.Job.Names` setting :

```csharp
using (var processor = new Processor())
{
    processor.Execute(args);
}
```

## Patterns

Base class|Usage
----------|-----
`PickAndProcessDataJobBase`|Query a page of records, process them, then loop as long as records are retrieved
`FullProcessDataJobBase`|Query all records, load them in memory and process them (progress file to resume)
`InputFileProcessDataJobBase`|Map a csv or txt file to table / columns and process each line
`CrossInstanceDataJobBase`|Query records on a source instance and process them on the target instance
`SyncDataJobBase`|Copy records from a source instance to the target instance (configurable attribute exclusions)
`CompareDataJobBase`|Compare records between two instances : only in source, only in target, different, equal (CSV report)
`SqlFullProcessDataJobBase`|Query records with SQL on Dataverse TDS endpoint ([Xrm.DataManager.Framework.Tds](https://www.nuget.org/packages/Xrm.DataManager.Framework.Tds/) package)

Overridable methods : `PreOperation` (before processing, ex : disable audit), `PrepareData` (ex : groupings), `PostOperation` (after processing, ex : notification), `IsEnabled`, `OverrideThreadNumber`...

## Main settings

Key (`appSettings`)|Default|Description
-------------------|-------|-----------
`Target.Connection.Name`|—|`connectionStrings` entry where jobs run (single entry or console selection if not defined)
`Source.Connection.Name`|—|`connectionStrings` entry to read records from (multi instance patterns)
`Target.Job.Names`|—|Jobs run by `Processor` (class names separated with ',')
`Process.Thread.Number`|5|Number of threads
`Process.Query.RecordLimit`|2500|Records retrieved per page (max 5000)
`LogLevel`|1|0 : Verbose, 1 : Information, 2 : ErrorsAndSuccess, 3 : ErrorsOnly

See the [complete settings list](https://github.com/AymericM78/Xrm.DataManager.Framework#settings).

## Additional packages

Package|Description
-------|-----------
[Xrm.DataManager.Framework.ApplicationInsights](https://www.nuget.org/packages/Xrm.DataManager.Framework.ApplicationInsights/)|Application Insights logger
[Xrm.DataManager.Framework.Tds](https://www.nuget.org/packages/Xrm.DataManager.Framework.Tds/)|Retrieve records with SQL (Dataverse TDS endpoint)

## Documentation

- [Complete documentation](https://github.com/AymericM78/Xrm.DataManager.Framework#readme) : logging, SQL retrieval, multi instance processing, synchronization, comparison
- [Upgrade guide](https://github.com/AymericM78/Xrm.DataManager.Framework/blob/master/UPGRADE.md) from 2022.x versions (ServiceClient, connections, renamed settings)

## Feedback

Questions, bugs and suggestions : [GitHub issues](https://github.com/AymericM78/Xrm.DataManager.Framework/issues). Community contributions are welcome !
