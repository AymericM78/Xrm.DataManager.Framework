using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xrm.DataManager.Framework.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            var process = new Processor();
            process.Execute(args);
        }
    }
}
