using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Xrm.DataManager.Framework.UnitTests.Core
{
    public class JobExecutionContextTests
    {
        private static Entity Contact()
        {
            var record = new Entity("contact", Guid.NewGuid());
            record["emailaddress1"] = "john@contoso.com";
            record["fullname"] = "John Doe";
            record["telephone1"] = null;
            return record;
        }

        [Fact]
        public void Only_logical_name_and_id_are_traced_by_default()
        {
            var record = Contact();

            var metrics = new JobExecutionContext(new FakeProxy(), record).DumpMetrics();

            Assert.Equal("contact", metrics["Crm.Record.LogicalName"]);
            Assert.Equal(record.Id.ToString(), metrics["Crm.Record.Id"]);
            Assert.DoesNotContain(metrics.Values, v => v != null && (v.Contains("contoso") || v.Contains("John")));
        }

        [Fact]
        public void Columns_are_traced_when_enabled_and_null_values_are_skipped()
        {
            var metrics = new JobExecutionContext(new FakeProxy(), Contact(), logRecordAttributes: true).DumpMetrics();

            Assert.Equal("john@contoso.com", metrics["Crm.Record.emailaddress1"]);
            Assert.False(metrics.ContainsKey("Crm.Record.telephone1"));
        }

        [Fact]
        public void Formatted_values_without_attribute_are_traced_when_enabled()
        {
            // Record retrieved with SQL
            var record = new Entity("contact", Guid.NewGuid());
            record.FormattedValues["fullname"] = "John Doe";

            var metrics = new JobExecutionContext(new FakeProxy(), record, logRecordAttributes: true).DumpMetrics();

            Assert.Equal("John Doe", metrics["Crm.Record.fullname"]);
        }

        [Fact]
        public void Duplicate_metrics_are_overwritten()
        {
            var context = new JobExecutionContext(new FakeProxy(), Contact());

            context.PushMetrics(new Dictionary<string, string> { { "Context.Job.Name", "A" } });
            context.PushMetrics(new Dictionary<string, string> { { "Context.Job.Name", "B" } });
            context.PushMetric("Email", "x");
            context.PushMetric("Email", "y");

            Assert.Equal("B", context.DumpMetrics()["Context.Job.Name"]);
            Assert.Equal("y", context.DumpMetrics()["Email"]);
        }
    }
}
