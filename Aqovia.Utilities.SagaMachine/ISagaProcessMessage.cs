namespace Aqovia.Utilities.SagaMachine
{
    /// <summary>
    /// The starting point of a saga process, defines how to to handle a particular message
    /// </summary>
    /// <typeparam name="TIncomingMessage">The message type to handle</typeparam>
    /// <typeparam name="TState">The state DTO for this saga</typeparam>
    public interface ISagaProcessMessage<TIncomingMessage, TState>
        where TIncomingMessage : ISagaMessageIdentifier
        where TState : ISagaIdentifier 
    {
        ISagaProcessState<TIncomingMessage, TState> WithMessage(TIncomingMessage message);
    }
 
}
