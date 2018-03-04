using System;

namespace LightGbmDotNet
{
    public class LogMessageEventArgs : EventArgs
    {
        public string LogMessage { get; }

        public LogMessageEventArgs(string logMessage)
        {
            LogMessage = logMessage;
        }
    }
}