using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Xrm.DataManager.Framework.UnitTests.Core
{
    public class UtilitiesTests
    {
        [Theory]
        [InlineData("AuthType=ClientSecret;Url=https://org.crm4.dynamics.com;ClientId=abc;ClientSecret=S3cr3t=;", "S3cr3t", "AuthType=ClientSecret;Url=https://org.crm4.dynamics.com;ClientId=abc;ClientSecret=********;")]
        [InlineData("AuthType=OAuth; Username=jdoe@contoso.com; password = 'p;w=d'; Url=https://org.crm.dynamics.com", "p;w", "AuthType=OAuth; Username=jdoe@contoso.com;password=********; Url=https://org.crm.dynamics.com")]
        [InlineData("Password=abc;Secret=def;AuthType=ClientSecret", "abc", "Password=********;Secret=********;AuthType=ClientSecret")]
        public void Connection_string_secrets_are_masked(string connectionString, string secret, string expected)
        {
            var masked = Utilities.MaskConnectionStringSecrets(connectionString);

            Assert.DoesNotContain(secret, masked);
            Assert.Equal(expected, masked);
        }

        [Fact]
        public void Masking_keeps_null()
        {
            Assert.Null(Utilities.MaskConnectionStringSecrets(null));
        }

        [Fact]
        public void Connection_string_is_masked_in_context_properties()
        {
            var settings = new JobSettings { SelectedConnectionString = "AuthType=ClientSecret;Url=https://org.crm4.dynamics.com;ClientSecret=TopSecret" };

            var properties = Utilities.GetContextProperties(settings);

            Assert.DoesNotContain("TopSecret", properties["Context.Crm.ConnectionString"]);
        }

        [Fact]
        public void Framework_version_is_package_version()
        {
            Assert.StartsWith(typeof(Utilities).Assembly.GetName().Version.Major + ".", Utilities.GetFrameworkVersion());
        }

        [Fact]
        public void Merge_overwrites_with_second_dictionary()
        {
            var merged = new Dictionary<string, string> { { "a", "1" }, { "b", "2" } }.MergeWith(new Dictionary<string, string> { { "b", "3" } });

            Assert.Equal("1", merged["a"]);
            Assert.Equal("3", merged["b"]);
        }

        [Fact]
        public void Console_selection_rejects_invalid_choices()
        {
            var output = Console.Out;
            var input = Console.In;
            try
            {
                Console.SetOut(TextWriter.Null);
                Console.SetIn(new StringReader("0\n-1\nabc\n3\n2\n"));

                var selected = new ConsoleHelper(new MemoryLogger()).ConnectionStringSelect(new List<string> { "Dev", "Uat" });

                Assert.Equal("Uat", selected);
            }
            finally
            {
                Console.SetOut(output);
                Console.SetIn(input);
            }
        }
    }
}
