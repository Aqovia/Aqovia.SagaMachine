namespace Aqovia.Utilities.SagaMachine
{
    /// <summary>
    /// All saga messages should be decorated with this, so that we can identify the correct saga state from the message
    /// </summary>
    public interface ISagaMessageIdentifier : ISagaIdentifier
    {
        
    }
}