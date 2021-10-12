using System;
using Xrm.DataManager.Framework;

namespace Xrm.DataManager.Framework.Tests
{
    public class JobProcessor : ProcessorBase
    {
        public JobProcessor()
        {
            InitializeOrganizationServiceManager();
        }

        public void Execute<T>() where T : DataJobBase
        {
            var parameters = new JobProcessParameters
            {
                ProxiesPool = ProxiesPool,
                Logger = Logger,
                CallerId = CallerId
            };

            var constructor = typeof(T).GetConstructor(new Type[] { typeof(JobSettings), typeof(JobProcessParameters) });
            var dataJobInstance = constructor?.Invoke(new object[] { JobSettings, parameters }) as DataJobBase;

            try
            {
                RunJob(dataJobInstance);
            }
            catch (Exception ex)
            {
                Logger.LogFailure(ex);
                Console.WriteLine($"Job failed : {dataJobInstance.GetName()} => Catastrophic error : {ex.Message}!");
            }
        }
    }
}
