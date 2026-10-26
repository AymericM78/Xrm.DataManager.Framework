# Xrm.DataManager.Framework.Tds

Retrieve Microsoft Dataverse records with SQL ([TDS endpoint](https://learn.microsoft.com/power-apps/developer/data-platform/dataverse-sql-query)) in [Xrm.DataManager.Framework](https://www.nuget.org/packages/Xrm.DataManager.Framework/) batch jobs.

[![NuGet](https://img.shields.io/nuget/v/Xrm.DataManager.Framework.Tds?label=NuGet)](https://www.nuget.org/packages/Xrm.DataManager.Framework.Tds/)
[![License](https://img.shields.io/github/license/AymericM78/Xrm.DataManager.Framework?color=yellow&label=License)](https://github.com/AymericM78/Xrm.DataManager.Framework/blob/master/LICENSE)

SQL simplifies and speeds up data retrieval (joins, aggregates, filters on linked tables) : records are then processed by the framework (multi threading, retries, progress file, logging).

## Getting started

### Prerequisites

- `Enable TDS endpoint` setting enabled on the Dataverse environment
- Dataverse connection with Entra ID authentication (OAuth, ClientSecret or Certificate) : its access token is used for SQL connection
- Network access to TDS endpoint port (1433 or 5558)

### Write a job

```csharp
using Xrm.DataManager.Framework;

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

Run it like any other job :

```csharp
using (var processor = new JobProcessor())
{
    processor.Execute<AnonymizeContactsJob>();
}
```

### How records are built

- The query is parsed before execution : it must be a single `SELECT` statement.
- Primary key column of the records table (first table of `FROM` clause, or `GetSqlEntityName()`) is added if missing and used as record id.
- Other columns are stored in `Record.FormattedValues` (culture invariant strings) and read with `GetSqlValue<T>(column)` / `GetSqlEntityReference(column, table)` : `Attributes` stay empty, so the record can be updated without sending SQL columns back.
- Lookups and choices are returned as `{column}` (id or value) and `{column}name` (label), dates are UTC.

### Direct SQL access

`TdsClient` retrieves records or opens a SQL connection outside of `SqlFullProcessDataJobBase` (ex : in `PreOperation`, or on another connection) :

```csharp
// Optional parameters : command timeout (seconds) and port
var tdsClient = new TdsClient(proxy, Logger);
var accounts = tdsClient.RetrieveRecords("SELECT name, accountnumber FROM account WHERE statecode = 0");
```

### Settings

Used by `SqlFullProcessDataJobBase` :

Key (`appSettings`)|Default|Description
-------------------|-------|-----------
`Tds.Port`|1433|TDS endpoint port (1433 or 5558, depending on your firewall)
`Tds.CommandTimeout.Seconds`|300|SQL query timeout

### Limits

- Read only : 5 minutes query timeout (2 minutes for `SELECT *` and joins).
- SQL queries don't trigger RetrieveMultiple plugins.
- Elastic tables aren't supported, `NOLOCK` hint should be avoided.

## Documentation

- [Complete documentation](https://github.com/AymericM78/Xrm.DataManager.Framework#readme)
- [Dataverse SQL query documentation](https://learn.microsoft.com/power-apps/developer/data-platform/dataverse-sql-query)

## Feedback

Questions, bugs and suggestions : [GitHub issues](https://github.com/AymericM78/Xrm.DataManager.Framework/issues).
