using System;
using Aqovia.Utilities.SagaMachine.Logging;

namespace Aqovia.Utilities.SagaMachine
{

    /// <summary>
    /// The end of the saga process
    /// </summary>
    /// <typeparam name="TMessage">The message type to handle</typeparam>
    /// <typeparam name="TState">The state DTO for this saga</typeparam>
    public interface ISagaProcessStop<TMessage, TState>
        where TMessage : ISagaMessageIdentifier
        where TState : ISagaIdentifier
    {
        ISagaProcessStop<TMessage, TState> LogIf(Action<TMessage, TState,ISagaLogState> logger, Func<TMessage, TState, bool> state);
        ISagaProcessStop<TMessage, TState> Log(Action<TMessage, TState,ISagaLogState> logger);
        ISagaProcessStop<TMessage, TState> StopSagaIf(Func<TMessage, TState,bool> state);
        ISagaProcessStop<TMessage, TState> StopSaga();
        ISagaDefined Execute();
    }
}