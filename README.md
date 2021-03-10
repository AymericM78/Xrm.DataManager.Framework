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
- **Tracing** : Supports file output, App Insights and Graylog...
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
Crm.ConnectionString|Specify connection string to Microsoft Dataverse
Job.Names|ex: DeleteAccountsJobs, DeleteContactsJob|Specify if you want to run multiple jobs (classe names splitted with ',')
Process.Duration.MaxHours|Par défaut: 8|Max job duration, to prevent nigh batch to run in prod during the day
Process.Query.RecordLimit|Par défaut: 2500|Records count to retrieve for each page (max 5000)
Process.Thread.Number|Par défaut: 10|Specify the number of threads to run
LogLevel|Verbose = 0,<br/>Information = 1,<br/>ErrorsAndSuccess = 2,<br/>ErrorsOnly = 3|Tracing level
AppInsights.Instrumentation.Key|Guid|Application Insights telemetry key
Graylog.Url|URL|Graylog Gelf endpoint URI

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





