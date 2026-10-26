using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Xrm.DataManager.Framework.UnitTests.Core
{
    public class CompareDataJobTests
    {
        private static readonly HashSet<string> Excluded = new HashSet<string>(CompareDataJobBase.DefaultExcludedAttributes, StringComparer.OrdinalIgnoreCase);

        private static bool AreEqual(string attribute, object source, object target) => Equals(CompareDataJobBase.NormalizeValue(source), CompareDataJobBase.NormalizeValue(target));

        private static Entity Account(Guid id, string name, string accountNumber = null)
        {
            var account = new Entity("account", id);
            account["accountid"] = id;
            account["name"] = name;
            if (accountNumber != null)
            {
                account["accountnumber"] = accountNumber;
            }
            return account;
        }

        private static List<DataCompareResult> Compare(IEnumerable<Entity> source, IEnumerable<Entity> target, Func<Entity, string> getKey = null, List<string> warnings = null)
        {
            return CompareDataJobBase.CompareRecords(source, target, getKey ?? (r => r.Id.ToString()), Excluded, AreEqual, m => warnings?.Add(m));
        }

        [Fact]
        public void Records_are_classified_by_status()
        {
            Guid equalId = Guid.NewGuid(), differentId = Guid.NewGuid(), sourceOnlyId = Guid.NewGuid(), targetOnlyId = Guid.NewGuid();
            var source = new[] { Account(equalId, "Equal"), Account(differentId, "Before"), Account(sourceOnlyId, "Source only") };
            var target = new[] { Account(equalId, "Equal"), Account(differentId, "After"), Account(targetOnlyId, "Target only") };

            var results = Compare(source, target).ToDictionary(r => r.Key);

            Assert.Equal(DataCompareStatus.Equal, results[equalId.ToString()].Status);
            Assert.Equal(DataCompareStatus.Different, results[differentId.ToString()].Status);
            Assert.Equal(new[] { "name" }, results[differentId.ToString()].DifferentAttributes);
            Assert.Equal(DataCompareStatus.OnlyInSource, results[sourceOnlyId.ToString()].Status);
            Assert.Null(results[sourceOnlyId.ToString()].TargetRecord);
            Assert.Equal(DataCompareStatus.OnlyInTarget, results[targetOnlyId.ToString()].Status);
            Assert.Null(results[targetOnlyId.ToString()].SourceRecord);
        }

        [Fact]
        public void Values_are_compared_by_meaning()
        {
            var id = Guid.NewGuid();
            var parentId = Guid.NewGuid();
            var source = Account(id, "Contoso");
            source["parentaccountid"] = new EntityReference("account", parentId) { Name = "Parent" };
            source["industrycode"] = new OptionSetValue(1);
            source["revenue"] = new Money(10.5m);
            source["description"] = string.Empty;
            source["modifiedon"] = DateTime.UtcNow; // excluded by default
            var target = Account(id, "Contoso");
            target["parentaccountid"] = new EntityReference("account", parentId); // name is not compared
            target["industrycode"] = new OptionSetValue(1);
            target["revenue"] = new Money(10.5m);
            // description : missing on target equals empty string on source

            Assert.Equal(DataCompareStatus.Equal, Compare(new[] { source }, new[] { target }).Single().Status);

            target["industrycode"] = new OptionSetValue(2);
            target["parentaccountid"] = new EntityReference("account", Guid.NewGuid());
            Assert.Equal(new[] { "industrycode", "parentaccountid" }, Compare(new[] { source }, new[] { target }).Single().DifferentAttributes);
        }

        [Fact]
        public void Records_can_be_matched_with_another_key_when_ids_differ()
        {
            var source = new[] { Account(Guid.NewGuid(), "Contoso", "A001") };
            var target = new[] { Account(Guid.NewGuid(), "Contoso", "a001") };

            var result = Compare(source, target, r => r.GetAttributeValue<string>("accountnumber")).Single();

            // Primary key (accountid) is ignored, account number matching is case insensitive
            Assert.Equal(DataCompareStatus.Different, result.Status);
            Assert.Equal(new[] { "accountnumber" }, result.DifferentAttributes);
        }

        [Fact]
        public void Empty_and_duplicate_keys_are_ignored_with_warning()
        {
            var warnings = new List<string>();
            var source = new[] { Account(Guid.NewGuid(), "A", "K1"), Account(Guid.NewGuid(), "B", "K1"), Account(Guid.NewGuid(), "C") };

            var results = Compare(source, new Entity[0], r => r.GetAttributeValue<string>("accountnumber"), warnings);

            Assert.Single(results);
            Assert.Equal("A", results[0].SourceRecord["name"]);
            Assert.Equal(2, warnings.Count);
        }

        [Fact]
        public void Report_lists_differences_and_escapes_values()
        {
            var id = Guid.NewGuid();
            var source = Account(id, "Contoso; \"Group\"");
            source["statuscode"] = new OptionSetValue(1);
            source.FormattedValues["statuscode"] = "Active";
            var target = Account(id, "Contoso");
            var equalId = Guid.NewGuid();
            var results = Compare(new[] { source, Account(equalId, "Same") }, new[] { target, Account(equalId, "Same") });

            var writer = new StringWriter();
            CompareDataJobBase.WriteReport(writer, results, ';', includeEqualRecords: false);
            var lines = writer.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal("Status;Key;Source Id;Target Id;Differences", lines[0]);
            Assert.Equal(2, lines.Length);
            Assert.Equal($"Different;{id};{id};{id};\"name : Contoso; \"\"Group\"\" => Contoso | statuscode : Active (1) => (empty)\"", lines[1]);
        }

        [Fact]
        public void Source_connection_must_be_defined_and_different_from_target()
        {
            var job = new TestCompareJob("Dev");

            Assert.Contains("Source.Connection.Name", Assert.Throws<Exception>(() => job.Source(null)).Message);
            Assert.Contains("must be different", Assert.Throws<Exception>(() => job.Source("dev")).Message);
            Assert.Contains("Available : Dev, Uat", Assert.Throws<Exception>(() => job.Source("Prod")).Message);
        }

        private class TestCompareJob : CompareDataJobBase
        {
            public TestCompareJob(string targetConnectionName) : base(new JobSettings { SelectedConnectionName = targetConnectionName }, new JobProcessParameters
            {
                Logger = new MemoryLogger(),
                Connections = new ProxiesPoolManager(new Dictionary<string, string> { { "Dev", "Url=https://dev.crm4.dynamics.com" }, { "Uat", "Url=https://uat.crm4.dynamics.com" } }, new MemoryLogger())
            })
            {
            }

            public ProxiesPool Source(string connectionName) => GetSourceProxiesPool(connectionName);
            public override string GetName() => "Test";
            public override QueryExpression GetQuery(Guid callerId) => new QueryExpression("account");
        }
    }
}
