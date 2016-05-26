using System.Collections.Generic;

namespace Aqovia.Utilities.SagaMachine.Logging
{
    public class SagaLogState : ISagaLogState
    {
        private readonly List<SagaLogStateMessage> _logList;

        public SagaLogState()
        {
            _logList = new List<SagaLogStateMessage>();
        }

        public List<SagaLogStateMessage>  GetLogMessages()
        {
            return _logList;
        }

        public void LogWarn(string message, List<SagaLogStateMessageProperty> properties = null)
        {
            _logList.Add(new SagaLogStateMessage
            {
                Level = SagaLogLevel.Warn,
                Message = message,
                Properties = properties
            });
        }

        public void LogInfo(string message, List<SagaLogStateMessageProperty> properties = null)
        {
            _logList.Add(new SagaLogStateMessage
            {
                Level = SagaLogLevel.Info,
                Message = message,
                Properties = properties
            });
        }

        public void LogError(string message, List<SagaLogStateMessageProperty> properties = null)
        {
            _logList.Add(new SagaLogStateMessage
            {
                Level = SagaLogLevel.Error,
                Message = message,
                Properties = properties
            });
        }
    }
}
