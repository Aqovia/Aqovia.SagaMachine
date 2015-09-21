using System.Collections.Generic;

namespace Aqovia.Utilities.SagaMachine.Logging
{
    public interface ISagaLogState
    {
        void LogWarn(string message, List<SagaLogStateMessageProperty> properties = null);
        void LogInfo(string message, List<SagaLogStateMessageProperty> properties = null);
        void LogError(string message, List<SagaLogStateMessageProperty> properties = null);
    }
}