using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;

namespace Xrm.DataManager.Framework
{
    public abstract class DataJobBase
    {
        protected JobSettings JobSettings
        {
            get;
        }
        protected Dictionary<string, object> JobProperties { get; set; } = new Dictionary<string, object>();
        protected ProxiesPool ProxiesPool
        {
            get;
        }
        protected ConsoleHelper ConsoleHelper
        {
            get;
        }
        protected ILogger Logger
        {
            get;
        }
        protected Guid CallerId
        {
            get;
        }

        private int? overrideThreadNumber;
        private static readonly object padlock = new object();
        protected virtual int? OverrideThreadNumber
        {
            get
            {
                lock (padlock)
                {
                    return overrideThreadNumber;
                }
            }
            set
            {
                lock (padlock)
                {
                    overrideThreadNumber = value;
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="jobSettings"></param>
        public DataJobBase(JobSettings jobSettings, JobProcessParameters parameters)
        {
            JobSettings = jobSettings;
            ProxiesPool = parameters.ProxiesPool;
            Logger = parameters.Logger;
            CallerId = parameters.CallerId;
            ConsoleHelper = new ConsoleHelper(Logger);
        }

        /// <summary>
        /// Specify a custom job name for tracing
        /// </summary>
        /// <returns></returns>
        public abstract string GetName();

        /// <summary>
        /// Specify if this job could be run in production
        /// Set to false if testing are not fully completed
        /// </summary>
        public virtual bool IsAllowedToRunInProduction() => false;

        /// <summary>
        /// Specify if this job is visible and available for execution
        /// </summary>
        public virtual bool IsEnabled => true;

        /// <summary>
        /// Specify task to run before record processing
        /// </summary>
        public virtual void PreOperation(ManagedTokenOrganizationServiceProxy proxy)
        {
            // Nothing to do here
        }

        /// <summary>
        /// Specify task to run after record retrieve operation in order to allow collection operation (ie grouping ...)
        /// </summary>
        /// <param name="records"></param>
        /// <returns></returns>
        public virtual IEnumerable<Entity> PrepareData(IEnumerable<Entity> records) => records;

        /// <summary>
        /// Specify task to run after record processing
        /// </summary>
        public virtual void PostOperation(ManagedTokenOrganizationServiceProxy proxy)
        {
            // Nothing to do here
        }

        public abstract bool Run();
    }
}
