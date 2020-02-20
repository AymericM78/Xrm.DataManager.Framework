using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Xrm.DataManager.Framework
{
    public class MultiThreadFileWriter
    {
        private string FilePath
        {
            get; set;
        }
        private int TimeOutInMs
        {
            get; set;
        }

        internal object lockObject;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="timeOutInMs"></param>
        public MultiThreadFileWriter(string filePath, int timeOutInMs = 5000)
        {
            FilePath = filePath;
            TimeOutInMs = timeOutInMs;
            lockObject = new object();
        }

        /// <summary>
        /// Write message to log file
        /// </summary>
        /// <param name="message"></param>
        public void Write(string message)
        {
            if (Monitor.TryEnter(lockObject, TimeOutInMs))
            {
                try
                {
                    var enumerableList = new List<string>() { message };
                    File.AppendAllLines(FilePath, enumerableList);
                }
                finally
                {
                    Monitor.Exit(lockObject);
                }
            }
            else
            {
                throw new Exception($"Timeout ({TimeOutInMs}ms) reached while trying to write to file! (Path : {FilePath})");
            }
        }
    }
}
