
using System;
using System.Collections.Generic;

namespace Xrm.DataManager.Framework
{
    public class JobProcessParameters
    {
        /// <summary>
        /// Target connection
        /// </summary>
        public ProxiesPool ProxiesPool
        {
            get; set;
        }
        /// <summary>
        /// All connections (connectionStrings entries)
        /// </summary>
        public ProxiesPoolManager Connections
        {
            get; set;
        }
        public ILogger Logger
        {
            get; set;
        }
        public Guid CallerId
        {
            get; set;
        }
    }
}