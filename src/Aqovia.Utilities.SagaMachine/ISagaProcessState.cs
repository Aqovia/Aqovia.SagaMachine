using System;

namespace Aqovia.Utilities.SagaMachine
{
    /// <summary>
    /// The State changing parting of the saga process
    /// </summary>
    /// <typeparam name="TIncomingMessage">The message type to handle</typeparam>
    /// <typeparam name="TState">The state DTO for this saga</typeparam>
    public interface ISagaProcessState<out TIncomingMessage, TState> : ISagaProcessPublish<TIncomingMessage, TState>
        where TIncomingMessage : ISagaMessageIdentifier
        where TState : ISagaIdentifier 
        
    {
        ISagaProcessState<TIncomingMessage, TState> InitialiseState(Func<TIncomingMessage, TState> initFunc);
        ISagaProcessState<TIncomingMessage, TState> ChangeStateIf(Func<TIncomingMessage, TState, TState> state, Func<TIncomingMessage, TState, bool> conditional);
        ISagaProcessState<TIncomingMessage, TState> ChangeState(Func<TIncomingMessage, TState, TState> state);
    }
}