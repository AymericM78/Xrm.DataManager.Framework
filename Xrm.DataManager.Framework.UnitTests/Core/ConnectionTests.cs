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
        public void Crm_ConnectionString_is_used_when_no_instance_is_requested()
        {
            var connection = ProcessorBase.ResolveConnectionString(null, "Url=https://setting.crm4.dynamics.com", TwoConnectionStrings(), NoSelector);

            Assert.Null(connection.Name);
            Assert.Equal("Url=https://setting.crm4.dynamics.com", connection.ConnectionString);
        }

        [Fact]
        public void Requested_instance_wins_over_Crm_ConnectionString_and_is_case_insensitive()
        {
            var connection = ProcessorBase.ResolveConnectionString("uat", "Url=https://setting.crm4.dynamics.com", TwoConnectionStrings(), NoSelector);

            Assert.Equal("uat", connection.Name);
            Assert.Equal("Url=https://uat.crm4.dynamics.com", connection.ConnectionString);
        }

        [Fact]
        public void Single_entry_is_used_without_selection()
        {
            var connectionStrings = new Dictionary<string, string> { { "Only", "Url=https://only.crm4.dynamics.com" } };

            var connection = ProcessorBase.ResolveConnectionString(null, null, connectionStrings, NoSelector);

            Assert.Equal("Only", connection.Name);
        }

        [Fact]
        public void Several_entries_are_selected_by_user()
        {
            var connection = ProcessorBase.ResolveConnectionString(null, null, TwoConnectionStrings(), names => names[1]);

            Assert.Equal("Uat", connection.Name);
        }

        [Fact]
        public void Unknown_instance_lists_available_entries()
        {
            var exception = Assert.Throws<Exception>(() => ProcessorBase.ResolveConnectionString("Prod", null, TwoConnectionStrings(), NoSelector));

            Assert.Contains("'Prod'", exception.Message);
            Assert.Contains("Dev, Uat", exception.Message);
        }

        [Fact]
        public void Missing_configuration_is_explicit()
        {
            var exception = Assert.Throws<Exception>(() => ProcessorBase.ResolveConnectionString(null, null, new Dictionary<string, string>(), NoSelector));

            Assert.Contains("Crm.ConnectionString", exception.Message);
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
