using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Xrm.DataManager.Framework
{
    public class GrayLogger : BaseLogger
    {
        private HttpClient client;

        /// <summary>
        /// Constructor
        /// </summary>
        public GrayLogger(JobSettings jobSettings) : base(jobSettings)
        {
            JobSettings = jobSettings;
            InitializeClient();
        }

        private void InitializeClient()
        {
            client = new HttpClient()
            {
                Timeout = new TimeSpan(0, 0, 30)
            };
            // Add HTTP headers
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.ConnectionClose = false;
        }

        protected void LogInternal(string message)
        {
            var defaultProperties = Utilities.GetContextProperties(JobSettings);
            LogInternal(message, defaultProperties);
        }

        protected void LogInternal(string message, Dictionary<string, string> properties) => LogInternal(message, false, properties);


        private string CleanGraylogKey(string key)
        {
            var normalizedString = key.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (c != '_')
                {
                    if (unicodeCategory != UnicodeCategory.LowercaseLetter && unicodeCategory != UnicodeCategory.UppercaseLetter)
                    {
                        continue;
                    }
                }
                stringBuilder.Append(c);

            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        protected void LogInternal(string message, bool isError, Dictionary<string, string> properties)
        {
            // Do not change "message" item
            properties.Add("message", message);

            properties.AddOrUpdate("Error", isError.ToString());

            var newProperties = new Dictionary<string, string>();
            foreach (var property in properties)
            {
                var newKey = property.Key.Replace(" ", "_");
                newKey = newKey.Replace(".", "_");
                newKey = CleanGraylogKey(newKey);
                var uniqueKey = newKey;
                var index = 2;
                while (newProperties.ContainsKey(uniqueKey))
                {
                    uniqueKey = $"{newKey}_{index}";
                    index++;
                }
                newProperties.Add(uniqueKey, property.Value);
            }

            try
            {
                var content = new StringContent(JsonConvert.SerializeObject(newProperties), Encoding.UTF8, "application/json");
                client.PostAsync(JobSettings.GrayLogUrl, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error occured: {ex.Message}!");
            }
        }

        public override void LogVerbose(string message)
        {
            if (LogLevel != LogLevel.Verbose)
            {
                return;
            }
            LogInternal(message);
            Console.WriteLine(message);
        }

        public override void LogInformation(string message, bool display = true)
        {
            if (display)
            {
                LogDisplay(message);
            }
            if (LogLevel > LogLevel.Information)
            {
                return;
            }
            LogInternal(message);
        }

        public override void LogSuccess(string message, Dictionary<string, string> properties)
        {
            if (LogLevel > LogLevel.ErrorsAndSuccess)
            {
                return;
            }
            LogInternal(message, properties);
        }

        public override void LogFailure(Exception exception, Dictionary<string, string> properties) => LogInternal(exception.Message, true, properties);
    }
}
