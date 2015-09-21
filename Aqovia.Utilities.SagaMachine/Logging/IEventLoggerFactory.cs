namespace Aqovia.Utilities.SagaMachine.Logging
{
    public interface IEventLoggerFactory
    {
        IEventLogger GetRequestEventLogger(string eventName);
    }
}
