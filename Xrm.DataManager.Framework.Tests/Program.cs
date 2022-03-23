using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xrm.DataManager.Framework.Tests
{
    class Program
    {

        static void Main(string[] args)
        {
            var process = new Processor();
            process.Execute(args);

            //var process = new CustomJobProcessor();
            //process.Execute();
        }
    }
}
