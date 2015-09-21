using System;
using System.Collections.Generic;

namespace Aqovia.Utilities.SagaMachine
{
    /// <summary>
    /// This is the publishing part of the saga process
    /// </summary>
    /// <typeparam name="TIncomingMessage">The message type to handle</typeparam>
    /// <typeparam name="TState">The state DTO for this saga</typeparam>
    public interface ISagaProcessPublish<TIncomingMessage, TState> : ISagaProcessStop<TIncomingMessage, TState>
        where TIncomingMessage : ISagaMessageIdentifier
        where TState : ISagaIdentifier 
    {
        ISagaProcessPublish<TIncomingMessage, TState> PublishIf(Func<TIncomingMessage, TState, IEnumerable<ISagaMessageIdentifier>> state, Func<TIncomingMessage, TState, bool> conditional);
        ISagaProcessPublish<TIncomingMessage, TState> Publish(Func<TIncomingMessage, TState, IEnumerable<ISagaMessageIdentifier>> state);        
    }
}