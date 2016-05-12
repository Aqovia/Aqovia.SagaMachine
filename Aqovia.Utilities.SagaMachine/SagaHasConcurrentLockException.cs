namespace Aqovia.Utilities.SagaMachine
{
    public class SagaHasConcurrentLockException : SagaException
    {
        public SagaHasConcurrentLockException(string message)
            : base(message)
        {
        }
    }
}
