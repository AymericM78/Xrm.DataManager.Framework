using Microsoft.Xrm.Sdk;
using System;
using System.Data;
using System.Globalization;
using System.Threading;
using Xunit;

namespace Xrm.DataManager.Framework.UnitTests.Tds
{
    public class SqlEntityMapperTests
    {
        private enum ContactState
        {
            Active = 0,
            Inactive = 1
        }

        private static DataTable ContactTable()
        {
            var table = new DataTable();
            table.Columns.Add("contactid", typeof(Guid));
            table.Columns.Add("fullname", typeof(string));
            table.Columns.Add("creditlimit", typeof(decimal));
            table.Columns.Add("createdon", typeof(DateTime));
            table.Columns.Add("donotemail", typeof(bool));
            table.Columns.Add("statecode", typeof(int));
            table.Columns.Add("parentcustomerid", typeof(Guid));
            table.Columns.Add("parentcustomeridname", typeof(string));
            table.Columns.Add("telephone1", typeof(string));
            return table;
        }

        [Fact]
        public void Rows_are_converted_to_entities_without_attributes()
        {
            var id = Guid.NewGuid();
            var parentId = Guid.NewGuid();
            var table = ContactTable();
            table.Rows.Add(id, "John Doe", 1234.56m, new DateTime(2026, 10, 26, 8, 30, 0), true, 1, parentId, "Contoso", DBNull.Value);

            var currentCulture = Thread.CurrentThread.CurrentCulture;
            Entity record;
            try
            {
                // Values must not depend on current culture (decimal separator, date format)
                Thread.CurrentThread.CurrentCulture = new CultureInfo("fr-FR");
                record = Assert.Single(SqlEntityMapper.ToEntities(table.CreateDataReader(), "contact", "contactid"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = currentCulture;
            }

            Assert.Equal("contact", record.LogicalName);
            Assert.Equal(id, record.Id);
            Assert.Empty(record.Attributes);
            Assert.Equal("1234.56", record.FormattedValues["creditlimit"]);
            Assert.Equal("2026-10-26T08:30:00.0000000Z", record.FormattedValues["createdon"]);
            Assert.False(record.FormattedValues.ContainsKey("telephone1"));
            Assert.False(record.FormattedValues.ContainsKey("contactid"));

            Assert.Equal("John Doe", record.GetSqlValue<string>("fullname"));
            Assert.Equal(1234.56m, record.GetSqlValue<decimal>("creditlimit"));
            var createdOn = record.GetSqlValue<DateTime>("createdon");
            Assert.Equal(new DateTime(2026, 10, 26, 8, 30, 0), createdOn);
            Assert.Equal(DateTimeKind.Utc, createdOn.Kind);
            Assert.True(record.GetSqlValue<bool>("donotemail"));
            Assert.Equal(1, record.GetSqlValue<int>("statecode"));
            Assert.Equal(ContactState.Inactive, record.GetSqlValue<ContactState>("statecode"));
            Assert.Null(record.GetSqlValue<string>("telephone1"));
            Assert.Null(record.GetSqlValue<int?>("missingcolumn"));

            var parent = record.GetSqlEntityReference("parentcustomerid", "account");
            Assert.Equal(parentId, parent.Id);
            Assert.Equal("account", parent.LogicalName);
            Assert.Equal("Contoso", parent.Name);
            Assert.Null(record.GetSqlEntityReference("telephone1", "account"));
        }

        [Fact]
        public void Primary_key_column_is_found_case_insensitively()
        {
            var table = new DataTable();
            table.Columns.Add("ContactId", typeof(Guid));
            table.Rows.Add(Guid.NewGuid());

            Assert.Single(SqlEntityMapper.ToEntities(table.CreateDataReader(), "contact", "contactid"));
        }

        [Fact]
        public void Missing_primary_key_is_rejected()
        {
            var table = new DataTable();
            table.Columns.Add("fullname", typeof(string));

            var exception = Assert.Throws<InvalidOperationException>(() => SqlEntityMapper.ToEntities(table.CreateDataReader(), "contact", "contactid"));
            Assert.Contains("'contactid'", exception.Message);
        }

        [Fact]
        public void Null_primary_key_is_rejected()
        {
            var table = new DataTable();
            table.Columns.Add("contactid", typeof(Guid));
            table.Rows.Add(DBNull.Value);

            Assert.Throws<InvalidOperationException>(() => SqlEntityMapper.ToEntities(table.CreateDataReader(), "contact", "contactid"));
        }

        [Theory]
        [InlineData("name", "Name")]
        [InlineData("name", "")]
        public void Duplicate_or_empty_column_names_are_rejected(string column1, string column2)
        {
            var reader = new ColumnsOnlyDataReader("contactid", column1, column2);

            Assert.Throws<InvalidOperationException>(() => SqlEntityMapper.ToEntities(reader, "contact", "contactid"));
        }
    }
}
