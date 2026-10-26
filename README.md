# Xrm.DataManager.Framework

Nuget framework for massive Microsoft Dataverse data operations.

<p align="center">
    <a href="#repolicense" alt="Repository License">
        <img src="https://img.shields.io/github/license/AymericM78/PowerDataOps?color=yellow&label=License" /></a>
    <a href="#openissues" alt="Open Issues">
        <img src="https://img.shields.io/github/issues-raw/AymericM78/PowerDataOps?label=Open%20Issues" /></a>
    <a href="#openpr" alt="Open Pull Requests">
        <img src="https://img.shields.io/github/issues-pr-raw/AymericM78/PowerDataOps?label=Open%20Pull%20Requests" /></a>
</p>

<p align="center">
    <a href="#watchers" alt="Watchers">
        <img src="https://img.shields.io/github/watchers/AymericM78/PowerDataOps?style=social" /></a>
    <a href="#forks" alt="Forks">
        <img src="https://img.shields.io/github/forks/AymericM78/PowerDataOps?style=social" /></a>
    <a href="#stars" alt="Stars">
        <img src="https://img.shields.io/github/stars/AymericM78/PowerDataOps?style=social" /></a>
</p>

## 🚀 Features

`Xrm.DataManager.Framework` provides folling features :

- **Batching patterns** : Full, Delta, ...
- **Performances** : Perf tuning, multi threading, ...
- **Errors handling and retry** : API Limits catching and waits
- **Tracing** : Supports file output and App Insights...
- **Configuration management** : Simplified conf ...

`Xrm.DataManager.Framework` is designed to simplify dev work by reducing core development and focus to functional implementation.

## 👐 Community contributions

This project is open to community contributions in order to enrich this tooling and helps everyone to bring more capabilities for Microsoft Dataverse data operations.

## ✈️ Installation and configuration

### Installation

Create a new C# console app projet and add [Xrm.DataManager.Framework Nuget package](https://www.nuget.org/packages/Xrm.DataManager.Framework/).

### Configuration

You need to specify following parameters in 'app.config':

Parameter name|Default or available values|Description
--------------|---------------------------|-----------
Crm.ConnectionString|ex: `AuthType=ClientSecret;Url=https://org.crm.dynamics.com;ClientId=...;ClientSecret=...`|Specify connection string to Microsoft Dataverse ([Dataverse ServiceClient format](https://learn.microsoft.com/power-apps/developer/data-platform/xrm-tooling/use-connection-strings-xrm-tooling-connect) : OAuth, ClientSecret or Certificate, `Office365` / `IFD` are no longer supported)
Job.Names|ex: DeleteAccountsJobs, DeleteContactsJob|Specify if you want to run multiple jobs (classe names splitted with ',')
Process.Duration.MaxHours|Default: 8|Max job duration, to prevent nigh batch to run in prod during the day
Process.Query.RecordLimit|Default: 2500|Records count to retrieve for each page (max 5000)
Process.Thread.Number|Default: 5|Specify the number of threads to run (server recommended value is displayed at startup)
Crm.AffinityCookie.Enabled|Default: false|Keep all requests on the same Dataverse node. Disabled by default to spread parallel requests on all nodes
LogLevel|Verbose = 0,<br/>Information = 1,<br/>ErrorsAndSuccess = 2,<br/>ErrorsOnly = 3|Tracing level
Logging.Record.Attributes|Default: false|Trace all record columns in telemetry (record logical name and id are always traced). Keep disabled if records contain personal data
AppInsights.ConnectionString|InstrumentationKey=...;IngestionEndpoint=...|Application Insights connection string (`AppInsights.Instrumentation.Key` is still read for compatibility but instrumentation key ingestion is retired)

### Usage

3 patterns are available:

- `PickAndProcessDataJob` : Query X rows from given QueryExpression and process them then loop as long as data is retrieved.
- `FullProcessDataJob` : Query all rows from given QueryExpression, load all data in memory and process.
- `InputFileProcessDataJob` : Map a csv or txt file to table/columns and process each line.

Theses patterns provide following overridable methods:

- GetName : Set a display name for job.
- IsEnabled : Specify if job is runnable or not.
- OverrideThreadNumber : Specify thread number for current job (For debug only)
- PreOperation : This method is called before proceeding data, you can use it to put data in cache for example or disable audit or whatever.
- GetQuery : Specify the query expression to retrieve data.
- PrepareData : Usefull with `FullProcessDataJob` to apply data aggregation like groupings.
- ProcessRecord : Apply custom operation for a single row.
- PostOperation : This method is called after all data have been processed, you can use it to send notification email or reactivate audit or whatever.





