
using System;
using System.Collections.Generic;

namespace Xrm.DataManager.Framework
{
    public class JobProcessParameters
    {
        public ProxiesPool ProxiesPool
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