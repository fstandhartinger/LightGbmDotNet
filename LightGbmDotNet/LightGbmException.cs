using System;

namespace LightGbmDotNet
{
    public class LightGbmException : Exception
    {
        public string Log { get; }

        public LightGbmException(string message, string log) : base(message)
        {
            Log = log;
        }

        public LightGbmException(string message, string log, Exception innerException) : base(message, innerException)
        {
            Log = log;
        }
    }
}