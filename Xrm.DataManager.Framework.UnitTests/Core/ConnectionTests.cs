using System;
using System.Collections.Generic;
using Xunit;

namespace Xrm.DataManager.Framework.UnitTests.Core
{
    public class ConnectionTests
    {
        private static Dictionary<string, string> TwoConnectionStrings() => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Dev", "Url=https://dev.crm4.dynamics.com" },
            { "Uat", "Url=https://uat.crm4.dynamics.com" }
        };

        private static string NoSelector(List<string> names) => throw new InvalidOperationException("Selector should not be called");

        [Fact]
        public void Requested_connection_is_case_insensitive_and_returns_entry_name()
        {
            Assert.Equal("Uat", ProcessorBase.ResolveConnectionName("uat", TwoConnectionStrings(), NoSelector));
        }

        [Fact]
        public void Single_entry_is_used_without_selection()
        {
            var connectionStrings = new Dictionary<string, string> { { "Only", "Url=https://only.crm4.dynamics.com" } };

            Assert.Equal("Only", ProcessorBase.ResolveConnectionName(null, connectionStrings, NoSelector));
        }

        [Fact]
        public void Several_entries_are_selected_by_user()
        {
            Assert.Equal("Uat", ProcessorBase.ResolveConnectionName(null, TwoConnectionStrings(), names => names[1]));
        }

        [Fact]
        public void Unknown_connection_lists_available_entries()
        {
            var exception = Assert.Throws<Exception>(() => ProcessorBase.ResolveConnectionName("Prod", TwoConnectionStrings(), NoSelector));

            Assert.Contains("'Prod'", exception.Message);
            Assert.Contains("Dev, Uat", exception.Message);
        }

        [Fact]
        public void Missing_configuration_is_explicit()
        {
            var exception = Assert.Throws<Exception>(() => ProcessorBase.ResolveConnectionName(null, new Dictionary<string, string>(), NoSelector));

            Assert.Contains("connectionStrings", exception.Message);
        }

        [Fact]
        public void AppInsights_entry_is_not_a_Dataverse_connection()
        {
            var connectionStrings = TwoConnectionStrings();
            connectionStrings.Add("appinsights", "InstrumentationKey=00000000-0000-0000-0000-000000000000");

            var dataverseConnectionStrings = JobSettings.GetDataverseConnectionStrings(connectionStrings);

            Assert.Equal(new[] { "Dev", "Uat" }, dataverseConnectionStrings.Keys);
            Assert.Equal(3, connectionStrings.Count);
        }

        [Fact]
        public void AppInsights_connection_string_falls_back_to_Azure_environment_variable()
        {
            const string variable = "APPLICATIONINSIGHTS_CONNECTION_STRING";
            var previousValue = Environment.GetEnvironmentVariable(variable);
            try
            {
                Environment.SetEnvironmentVariable(variable, "InstrumentationKey=11111111-1111-1111-1111-111111111111");
                var settings = new JobSettings();

                Assert.True(settings.ApplicationInsightsEnabled);
                Assert.Equal("InstrumentationKey=11111111-1111-1111-1111-111111111111", settings.AppInsightsConnectionString);
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, previousValue);
            }
        }

        [Fact]
        public void Connection_manager_rejects_unknown_connection_without_connecting()
        {
            var connections = new ProxiesPoolManager(TwoConnectionStrings(), new MemoryLogger());

            var exception = Assert.Throws<Exception>(() => connections.GetProxiesPool("Prod"));

            Assert.Contains("Available : Dev, Uat", exception.Message);
            Assert.Equal("Url=https://uat.crm4.dynamics.com", connections.GetConnectionString("UAT"));
        }

        [Theory]
        [InlineData("AuthType=ClientSecret;Url=https://org.crm4.dynamics.com;ClientId=a;ClientSecret=b", "https://org.crm4.dynamics.com")]
        [InlineData("authtype=OAuth;url=https://org.crm.dynamics.com/;", "https://org.crm.dynamics.com/")]
        [InlineData("AuthType=ClientSecret;ServiceUri=https://org.crm4.dynamics.com", "https://org.crm4.dynamics.com")]
        [InlineData("AuthType=OAuth; Service Uri = https://org.crm4.dynamics.com ;", "https://org.crm4.dynamics.com")]
        [InlineData("AuthType=OAuth;Server=https://org.crm4.dynamics.com", "https://org.crm4.dynamics.com")]
        [InlineData("AuthType=ClientSecret;ClientSecret='ab=c;d';Url=https://org.crm4.dynamics.com", "https://org.crm4.dynamics.com")]
        public void Url_is_extracted_from_connection_string(string connectionString, string expectedUrl)
        {
            Assert.Equal(expectedUrl, ProxiesPool.ExtractUrlFromConnectionString(connectionString));
        }

        [Theory]
        [InlineData("AuthType=ClientSecret;ClientId=a;ClientSecret=TopSecret")]
        [InlineData("AuthType='unterminated;ClientSecret=TopSecret")]
        public void Invalid_connection_string_error_does_not_expose_secret(string connectionString)
        {
            var exception = Assert.Throws<ArgumentException>(() => ProxiesPool.ExtractUrlFromConnectionString(connectionString));

            Assert.DoesNotContain("TopSecret", exception.Message);
        }

        [Fact]
        public void Pool_evicts_proxy_not_ready_and_disposes_pooled_proxies()
        {
            var pool = (ProxiesPool)CreateUninitialized(typeof(ProxiesPool));
            typeof(ProxiesPool).GetField("AvailableProxies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(pool, new System.Collections.Concurrent.ConcurrentBag<IManagedTokenOrganizationServiceProxy>());

            // Not ready (no ServiceClient) : disposed, then a new clone is requested (fails without main proxy)
            var deadProxy = new FakeProxy();
            pool.ReleaseProxy(deadProxy);
            Assert.ThrowsAny<Exception>(() => pool.GetProxy());
            Assert.True(deadProxy.Disposed);

            var proxy1 = new FakeProxy();
            var proxy2 = new FakeProxy();
            pool.ReleaseProxy(proxy1);
            pool.ReleaseProxy(proxy2);
            pool.Dispose();
            Assert.True(proxy1.Disposed && proxy2.Disposed);
        }

        private static object CreateUninitialized(Type type)
        {
#if NETFRAMEWORK
            return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
#else
            return System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
#endif
        }
    }
}
