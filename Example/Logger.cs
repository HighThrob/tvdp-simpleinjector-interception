using System;
using System.Collections.Generic;
using System.Text;

namespace TvdP
{
    public class Logger : ILogger
    {
        public void LogMessage(string message)
        { Console.WriteLine(message); }
    }
}
