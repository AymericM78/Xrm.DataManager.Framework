using Microsoft.Data.SqlClient;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Xrm.DataManager.Framework
{
    /// <summary>
    /// Query Dataverse with SQL (TDS endpoint : read only, 'Enable TDS endpoint' environment setting is required)
    /// Authentication uses access token of given proxy
    /// </summary>
    public class TdsClient
    {
        /// <summary>
        /// TDS endpoint timeout is 5 minutes (2 minutes for SELECT *, nested FROM and joins)
        /// </summary>
        public const int DefaultCommandTimeoutSeconds = 300;

        protected IManagedTokenOrganizationServiceProxy Proxy;
        protected ILogger Logger;
        protected int CommandTimeoutSeconds;
        protected int Port;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="proxy">Dataverse connection (OAuth, ClientSecret or Certificate)</param>
        /// <param name="logger"></param>
        /// <param name="commandTimeoutSeconds"></param>
        /// <param name="port">TDS endpoint port : 1433 (default) or 5558</param>
        public TdsClient(IManagedTokenOrganizationServiceProxy proxy, ILogger logger, int commandTimeoutSeconds = DefaultCommandTimeoutSeconds, int port = 0)
        {
            Proxy = proxy ?? throw new ArgumentNullException(nameof(proxy));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            CommandTimeoutSeconds = commandTimeoutSeconds;
            Port = port;
        }

        /// <summary>
        /// Open SQL connection to TDS endpoint (ex : to run custom queries)
        /// </summary>
        /// <returns></returns>
        public SqlConnection CreateConnection()
        {
            var host = Proxy.ServiceClient.ConnectedOrgUriActual.Host;
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = (Port > 0) ? $"{host},{Port}" : host,
                ApplicationName = "Xrm.DataManager.Framework"
            };
            var connection = new SqlConnection(builder.ConnectionString)
            {
                AccessToken = Proxy.ServiceClient.CurrentAccessToken
            };
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Validate SQL query and add primary key column if missing
        /// </summary>
        /// <param name="sql">Single SELECT statement</param>
        /// <param name="entityName">Table of the records to process (default : first table of FROM clause)</param>
        /// <returns></returns>
        public TdsQuery Prepare(string sql, string entityName = null) => TdsQuery.Prepare(sql, entityName, GetPrimaryKey);

        /// <summary>
        /// Retrieve records with SQL : primary key is used as record id, other columns are available with GetSqlValue
        /// </summary>
        /// <param name="sql">Single SELECT statement</param>
        /// <param name="entityName">Table of the records to process (default : first table of FROM clause)</param>
        /// <returns></returns>
        public List<Entity> RetrieveRecords(string sql, string entityName = null)
        {
            var query = Prepare(sql, entityName);
            if (query.PrimaryKeyAdded)
            {
                Logger.LogInformation($"Primary key column '{query.PrimaryKey}' has been added to SQL query");
            }
            Logger.LogVerbose($"SQL query : {query.Sql}");

            List<Entity> records;
            using (var connection = CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = query.Sql;
                command.CommandTimeout = CommandTimeoutSeconds;
                using (var reader = command.ExecuteReader())
                {
                    records = SqlEntityMapper.ToEntities(reader, query.EntityName, query.PrimaryKey);
                }
            }

            var duplicates = records.Count - records.Select(r => r.Id).Distinct().Count();
            if (duplicates > 0)
            {
                Logger.LogInformation($"{duplicates} SQL rows share the same '{query.PrimaryKey}' (joins?) : these records will be processed several times!");
            }
            return records;
        }

        /// <summary>
        /// Get primary key column from table metadata (ex : activityid for email)
        /// </summary>
        protected virtual string GetPrimaryKey(string entityName)
        {
            var request = new RetrieveEntityRequest
            {
                LogicalName = entityName,
                EntityFilters = EntityFilters.Entity
            };
            var response = (RetrieveEntityResponse)Proxy.Execute(request);
            return response.EntityMetadata.PrimaryIdAttribute;
        }
    }
}
