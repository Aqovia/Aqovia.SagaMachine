using System;
using System.Collections.Generic;

namespace Aqovia.Utilities.SagaMachine.Logging
{
    public interface IEventLogger
    {
        object this[string key] { get; set; }
        
        void LogInfo(string message);
        void LogWarn(string message);
        void LogError(string message);
    }
}
