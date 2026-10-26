# Xrm.DataManager.Framework.ApplicationInsights

Application Insights logger for [Xrm.DataManager.Framework](https://www.nuget.org/packages/Xrm.DataManager.Framework/) (Microsoft Dataverse batch jobs).

[![NuGet](https://img.shields.io/nuget/v/Xrm.DataManager.Framework.ApplicationInsights?label=NuGet)](https://www.nuget.org/packages/Xrm.DataManager.Framework.ApplicationInsights/)
[![License](https://img.shields.io/github/license/AymericM78/Xrm.DataManager.Framework?color=yellow&label=License)](https://github.com/AymericM78/Xrm.DataManager.Framework/blob/master/LICENSE)

Job executions are traced in Application Insights :

- **Traces** : job progress and information messages
- **Custom events** : processed records (successes)
- **Exceptions** : failures, with job and record context

Built on Application Insights SDK 3.x (OpenTelemetry based) : telemetry is flushed at the end of each job.

## Getting started

### Configuration

Add the Application Insights connection string in `connectionStrings` section of `app.config`, with the reserved name `AppInsights` :

```xml
<connectionStrings>
  <add name="Dev" connectionString="AuthType=ClientSecret;Url=https://dev.crm4.dynamics.com;ClientId=...;ClientSecret=..." />
  <add name="AppInsights" connectionString="InstrumentationKey=...;IngestionEndpoint=https://..." />
</connectionStrings>
```

The `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable is used when `AppInsights` entry is not defined (ex : Azure hosting).

### Usage

Provide the logger to the processor constructor :

```csharp
using (var processor = new JobProcessor(settings => new ApplicationInsightLogger(settings)))
{
    processor.Execute<MyDataJob>();
}
```

Or use Application Insights only when it is configured :

```csharp
ILogger CreateLogger(JobSettings settings) => settings.ApplicationInsightsEnabled ? new ApplicationInsightLogger(settings) : new FileLogger(settings);

using (var processor = new Processor(CreateLogger))
{
    processor.Execute(args);
}
```

### Settings

Key (`appSettings`)|Default|Description
-------------------|-------|-----------
`LogLevel`|1|0 : Verbose, 1 : Information, 2 : ErrorsAndSuccess, 3 : ErrorsOnly
`Logging.Record.Attributes`|false|Trace all record columns (record logical name and id are always traced). Keep disabled if records contain personal data

## Documentation

- [Complete documentation](https://github.com/AymericM78/Xrm.DataManager.Framework#readme)
- [Upgrade guide](https://github.com/AymericM78/Xrm.DataManager.Framework/blob/master/UPGRADE.md) from 2022.x versions (instrumentation key is replaced by the connection string)

## Feedback

Questions, bugs and suggestions : [GitHub issues](https://github.com/AymericM78/Xrm.DataManager.Framework/issues).
