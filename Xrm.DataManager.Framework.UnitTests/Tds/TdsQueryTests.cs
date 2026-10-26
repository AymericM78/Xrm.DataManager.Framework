using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Xrm.DataManager.Framework.UnitTests.Tds
{
    public class TdsQueryTests
    {
        private static readonly Dictionary<string, string> PrimaryKeys = new Dictionary<string, string>
        {
            { "contact", "contactid" },
            { "account", "accountid" },
            { "activityparty", "activitypartyid" },
            { "email", "activityid" }
        };

        private static string PrimaryKey(string entityName) => PrimaryKeys[entityName];

        private static void AssertValidSql(string sql)
        {
            new TSql160Parser(true).Parse(new StringReader(sql), out IList<ParseError> errors);
            Assert.Empty(errors);
        }

        /// <summary>
        /// Generated SQL is formatted on several lines
        /// </summary>
        private static string SingleLine(string sql) => Regex.Replace(sql, @"\s+", " ");

        [Fact]
        public void Primary_key_is_added_when_missing()
        {
            var query = TdsQuery.Prepare("SELECT fullname, emailaddress1 FROM contact WHERE statecode = 0", null, PrimaryKey);

            Assert.True(query.PrimaryKeyAdded);
            Assert.Equal("contact", query.EntityName);
            Assert.Equal("contactid", query.PrimaryKey);
            Assert.Contains("SELECT contact.[contactid], fullname, emailaddress1 FROM contact WHERE statecode = 0", SingleLine(query.Sql));
            AssertValidSql(query.Sql);
        }

        [Theory]
        [InlineData("SELECT contactid, fullname FROM contact")]
        [InlineData("SELECT ContactId FROM contact")]
        [InlineData("SELECT * FROM contact")]
        [InlineData("SELECT c.* FROM contact c")]
        [InlineData("SELECT TOP 10 c.contactid, c.fullname FROM contact AS c ORDER BY c.fullname")]
        [InlineData("SELECT fullname, parentcustomerid AS contactid FROM contact")]
        public void Query_is_kept_when_primary_key_is_selected(string sql)
        {
            var query = TdsQuery.Prepare(sql, null, PrimaryKey);

            Assert.False(query.PrimaryKeyAdded);
            Assert.Equal(sql, query.Sql);
        }

        [Fact]
        public void Primary_key_uses_table_alias_with_joins()
        {
            var query = TdsQuery.Prepare("SELECT c.fullname, a.name FROM contact c INNER JOIN account a ON c.parentcustomerid = a.accountid", null, PrimaryKey);

            Assert.Equal("contact", query.EntityName);
            Assert.Contains("SELECT c.[contactid], c.fullname, a.name", SingleLine(query.Sql));
            AssertValidSql(query.Sql);
        }

        [Fact]
        public void Explicit_table_can_be_the_joined_one()
        {
            var query = TdsQuery.Prepare("SELECT c.fullname, a.name FROM contact c INNER JOIN account a ON c.parentcustomerid = a.accountid", "Account", PrimaryKey);

            Assert.Equal("account", query.EntityName);
            Assert.Contains("a.[accountid]", query.Sql);
        }

        [Fact]
        public void Column_with_same_name_from_another_table_is_not_the_primary_key()
        {
            var query = TdsQuery.Prepare("SELECT a.primarycontactid AS name, a.accountid FROM contact c INNER JOIN account a ON a.primarycontactid = c.contactid", "contact", PrimaryKey);
            Assert.True(query.PrimaryKeyAdded);

            // Another table column aliased as primary key : real primary key is added, result is then rejected (duplicate column) instead of using wrong ids
            query = TdsQuery.Prepare("SELECT a.accountid AS contactid FROM contact c INNER JOIN account a ON a.primarycontactid = c.contactid", "contact", PrimaryKey);
            Assert.True(query.PrimaryKeyAdded);
        }

        [Fact]
        public void Aliased_primary_key_is_added_again_with_its_name()
        {
            var query = TdsQuery.Prepare("SELECT contactid AS id, fullname FROM contact", null, PrimaryKey);

            Assert.True(query.PrimaryKeyAdded);
            Assert.Contains("contact.[contactid]", query.Sql);
        }

        [Fact]
        public void Quoted_alias_is_kept()
        {
            var query = TdsQuery.Prepare("SELECT [to].partyidname FROM activityparty AS [to] WHERE [to].participationtypemask = 2", null, PrimaryKey);

            Assert.Contains("[to].[activitypartyid]", query.Sql);
            AssertValidSql(query.Sql);
        }

        [Fact]
        public void Entity_name_is_lower_case_for_metadata()
        {
            string requested = null;
            TdsQuery.Prepare("SELECT subject FROM Email", null, name => { requested = name; return "activityid"; });

            Assert.Equal("email", requested);
        }

        [Fact]
        public void Union_is_kept_but_requires_entity_name()
        {
            const string sql = "SELECT contactid FROM contact WHERE statecode = 0 UNION SELECT contactid FROM contact WHERE statecode = 1";

            Assert.Throws<ArgumentException>(() => TdsQuery.Prepare(sql, null, PrimaryKey));
            var query = TdsQuery.Prepare(sql, "contact", PrimaryKey);
            Assert.False(query.PrimaryKeyAdded);
            Assert.Equal(sql, query.Sql);
        }

        [Theory]
        [InlineData("SELECT fullname FROM", "invalid")]
        [InlineData("SELECT fullname FROM contact; SELECT name FROM account", "single SELECT")]
        [InlineData("DELETE FROM contact", "single SELECT")]
        [InlineData("SELECT fullname INTO #tmp FROM contact", "single SELECT")]
        [InlineData("SELECT parentcustomerid, COUNT(*) AS total FROM contact GROUP BY parentcustomerid", "GROUP BY")]
        [InlineData("", "not defined")]
        public void Unsupported_queries_are_rejected(string sql, string expectedMessage)
        {
            var exception = Assert.Throws<ArgumentException>(() => TdsQuery.Prepare(sql, null, PrimaryKey));

            Assert.Contains(expectedMessage, exception.Message);
        }

        [Fact]
        public void Explicit_table_must_be_in_FROM_clause()
        {
            var exception = Assert.Throws<ArgumentException>(() => TdsQuery.Prepare("SELECT fullname FROM contact", "account", PrimaryKey));

            Assert.Contains("'account'", exception.Message);
        }
    }
}
