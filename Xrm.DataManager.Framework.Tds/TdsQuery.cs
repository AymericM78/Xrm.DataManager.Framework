using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xrm.DataManager.Framework
{
    /// <summary>
    /// SQL query ready to be executed on TDS endpoint : primary key of the records to process is always returned
    /// </summary>
    public class TdsQuery
    {
        /// <summary>
        /// SQL query to execute
        /// </summary>
        public string Sql { get; }

        /// <summary>
        /// Table of the records to process
        /// </summary>
        public string EntityName { get; }

        /// <summary>
        /// Primary key column of EntityName (used as record id)
        /// </summary>
        public string PrimaryKey { get; }

        /// <summary>
        /// True if primary key column has been added to the original query
        /// </summary>
        public bool PrimaryKeyAdded { get; }

        internal TdsQuery(string sql, string entityName, string primaryKey, bool primaryKeyAdded)
        {
            Sql = sql;
            EntityName = entityName;
            PrimaryKey = primaryKey;
            PrimaryKeyAdded = primaryKeyAdded;
        }

        /// <summary>
        /// Validate SQL query and add primary key column of the records to process if missing
        /// </summary>
        /// <param name="sql">Single SELECT statement</param>
        /// <param name="entityName">Table of the records to process (default : first table of FROM clause)</param>
        /// <param name="primaryKeyResolver">Get primary key column from table logical name</param>
        /// <returns></returns>
        public static TdsQuery Prepare(string sql, string entityName, Func<string, string> primaryKeyResolver)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                throw new ArgumentException("SQL query is not defined!");
            }

            var parser = new TSql160Parser(initialQuotedIdentifiers: true);
            TSqlFragment fragment;
            using (var reader = new StringReader(sql))
            {
                fragment = parser.Parse(reader, out IList<ParseError> errors);
                if (errors.Count > 0)
                {
                    var details = string.Join(" / ", errors.Select(e => $"line {e.Line}, column {e.Column} : {e.Message}"));
                    throw new ArgumentException($"SQL query is invalid! ({details})");
                }
            }

            var statements = ((TSqlScript)fragment).Batches.SelectMany(b => b.Statements).ToList();
            if (statements.Count != 1 || !(statements[0] is SelectStatement select) || select.Into != null)
            {
                throw new ArgumentException("SQL query must contain a single SELECT statement!");
            }

            // UNION, EXCEPT... : primary key can't be added, result is checked at execution
            var query = select.QueryExpression as QuerySpecification;
            var mainTable = (query?.FromClause == null) ? null : GetMainTable(query.FromClause.TableReferences.FirstOrDefault());

            entityName = string.IsNullOrWhiteSpace(entityName) ? mainTable?.SchemaObject.BaseIdentifier.Value : entityName;
            if (string.IsNullOrWhiteSpace(entityName))
            {
                throw new ArgumentException("Table of the records to process can't be found in SQL query : specify it explicitly!");
            }
            // Dataverse logical names are lower case
            entityName = entityName.Trim().ToLowerInvariant();
            var primaryKey = primaryKeyResolver(entityName);

            if (query == null)
            {
                return new TdsQuery(sql, entityName, primaryKey, false);
            }

            var table = GetTables(query.FromClause?.TableReferences)
                .FirstOrDefault(t => string.Equals(t.SchemaObject.BaseIdentifier.Value, entityName, StringComparison.OrdinalIgnoreCase));
            if (table == null)
            {
                throw new ArgumentException($"Table '{entityName}' is not defined in FROM clause of SQL query!");
            }

            if (SelectsPrimaryKey(query, table, primaryKey))
            {
                return new TdsQuery(sql, entityName, primaryKey, false);
            }

            if (query.GroupByClause != null)
            {
                throw new ArgumentException($"SQL query with GROUP BY must select primary key column '{primaryKey}' of table '{entityName}'!");
            }

            // Add table.primarykey as first column
            var qualifier = table.Alias ?? table.SchemaObject.BaseIdentifier;
            var columnReference = new ColumnReferenceExpression { MultiPartIdentifier = new MultiPartIdentifier() };
            columnReference.MultiPartIdentifier.Identifiers.Add(new Identifier { Value = qualifier.Value, QuoteType = qualifier.QuoteType });
            columnReference.MultiPartIdentifier.Identifiers.Add(new Identifier { Value = primaryKey, QuoteType = QuoteType.SquareBracket });
            query.SelectElements.Insert(0, new SelectScalarExpression { Expression = columnReference });

            new Sql160ScriptGenerator().GenerateScript(select, out var preparedSql);
            return new TdsQuery(preparedSql, entityName, primaryKey, true);
        }

        /// <summary>
        /// First table of FROM clause (left table of joins)
        /// </summary>
        private static NamedTableReference GetMainTable(TableReference tableReference)
        {
            switch (tableReference)
            {
                case NamedTableReference namedTable:
                    return namedTable;
                case JoinTableReference join:
                    return GetMainTable(join.FirstTableReference);
                case JoinParenthesisTableReference parenthesis:
                    return GetMainTable(parenthesis.Join);
                default:
                    return null;
            }
        }

        private static IEnumerable<NamedTableReference> GetTables(IEnumerable<TableReference> tableReferences)
        {
            foreach (var tableReference in tableReferences ?? Enumerable.Empty<TableReference>())
            {
                switch (tableReference)
                {
                    case NamedTableReference namedTable:
                        yield return namedTable;
                        break;
                    case JoinTableReference join:
                        foreach (var table in GetTables(new[] { join.FirstTableReference, join.SecondTableReference }))
                        {
                            yield return table;
                        }
                        break;
                    case JoinParenthesisTableReference parenthesis:
                        foreach (var table in GetTables(new[] { parenthesis.Join }))
                        {
                            yield return table;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Check if a result column named as primary key is returned from given table
        /// </summary>
        private static bool SelectsPrimaryKey(QuerySpecification query, NamedTableReference table, string primaryKey)
        {
            foreach (var selectElement in query.SelectElements)
            {
                switch (selectElement)
                {
                    // * or table.*
                    case SelectStarExpression star:
                        if (star.Qualifier == null || IsTable(star.Qualifier.Identifiers.Last().Value, table))
                        {
                            return true;
                        }
                        break;

                    case SelectScalarExpression scalar:
                        var identifiers = (scalar.Expression as ColumnReferenceExpression)?.MultiPartIdentifier?.Identifiers;
                        var outputName = scalar.ColumnName?.Value ?? identifiers?.Last().Value;
                        if (!string.Equals(outputName, primaryKey, StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }
                        // Column of another table
                        if (identifiers != null && identifiers.Count > 1 && !IsTable(identifiers[identifiers.Count - 2].Value, table))
                        {
                            break;
                        }
                        return true;
                }
            }
            return false;
        }

        private static bool IsTable(string qualifier, NamedTableReference table)
        {
            return string.Equals(qualifier, table.Alias?.Value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(qualifier, table.SchemaObject.BaseIdentifier.Value, StringComparison.OrdinalIgnoreCase);
        }
    }
}
