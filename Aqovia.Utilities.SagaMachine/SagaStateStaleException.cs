namespace Aqovia.Utilities.SagaMachine
{
    public class SagaStateStaleException : SagaException
    {
        public SagaStateStaleException(string message) : base(message)
        {
        }
    }
}