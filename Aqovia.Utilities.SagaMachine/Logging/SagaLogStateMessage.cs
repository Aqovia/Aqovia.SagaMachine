using System.Collections.Generic;

namespace Aqovia.Utilities.SagaMachine.Logging
{
    public class SagaLogStateMessage
    {
        public SagaLogLevel Level { get; set; }
        public string Message { get; set; }
        public List<SagaLogStateMessageProperty> Properties { get; set; }
    }
}