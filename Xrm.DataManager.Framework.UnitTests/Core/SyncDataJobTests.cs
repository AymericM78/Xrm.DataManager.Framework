using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Xrm.DataManager.Framework.UnitTests.Core
{
    public class SyncDataJobTests
    {
        private static readonly HashSet<string> TargetAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "name", "telephone1", "parentaccountid", "primarycontactid", "ownerid", "statecode", "statuscode", "createdon", "new_onlyontarget"
        };

        private static Entity SourceAccount()
        {
            var account = new Entity("account", Guid.NewGuid());
            account["accountid"] = account.Id;
            account["name"] = "Contoso";
            account["telephone1"] = "0102030405";
            account["parentaccountid"] = new EntityReference("account", Guid.NewGuid());
            account["ownerid"] = new EntityReference("systemuser", Guid.NewGuid());
            account["statecode"] = new OptionSetValue(1);
            account["statuscode"] = new OptionSetValue(2);
            account["createdon"] = DateTime.UtcNow;
            account["new_onlyonsource"] = "x";
            account["contact.fullname"] = new AliasedValue("contact", "fullname", "John Doe");
            return account;
        }

        private static HashSet<string> Excluded(params string[] attributes) => new HashSet<string>(attributes, StringComparer.OrdinalIgnoreCase);

        [Fact]
        public void Only_updatable_target_attributes_are_copied_with_same_id()
        {
            var source = SourceAccount();

            var target = SyncDataJobBase.BuildTargetRecord(source, Excluded(SyncDataJobBase.DefaultExcludedAttributes), TargetAttributes, includeEntityReferences: false);

            Assert.Equal(source.Id, target.Id);
            Assert.Equal("account", target.LogicalName);
            // Excluded by default : createdon, ownerid / missing on target : new_onlyonsource / read only : accountid / lookups disabled / state applied separately / linked entity
            Assert.Equal(new[] { "name", "telephone1" }, target.Attributes.Keys.OrderBy(k => k));
        }

        [Fact]
        public void Lookups_are_copied_when_enabled()
        {
            var target = SyncDataJobBase.BuildTargetRecord(SourceAccount(), Excluded(SyncDataJobBase.DefaultExcludedAttributes), TargetAttributes, includeEntityReferences: true);

            Assert.True(target.Contains("parentaccountid"));
            Assert.False(target.Contains("ownerid"));
        }

        [Fact]
        public void Exclusions_are_case_insensitive_and_replace_defaults()
        {
            var excluded = Excluded(SyncDataJobBase.ParseExcludedAttributes(" Telephone1 , name ,").ToArray());

            var target = SyncDataJobBase.BuildTargetRecord(SourceAccount(), excluded, TargetAttributes, includeEntityReferences: true);

            Assert.Equal(new[] { "createdon", "ownerid", "parentaccountid" }, target.Attributes.Keys.OrderBy(k => k));
        }

        [Fact]
        public void Exclusions_setting_defaults()
        {
            Assert.Equal(SyncDataJobBase.DefaultExcludedAttributes, SyncDataJobBase.ParseExcludedAttributes(null));
            Assert.Empty(SyncDataJobBase.ParseExcludedAttributes(""));
        }
    }
}
