using System;
using System.Threading.Tasks;

namespace Aqovia.Utilities.SagaMachine
{
    /// <summary>
    /// This is the starting point of the saga, for every message type we will define a saga process
    /// </summary>
    /// <typeparam name="TState">The state DTO for this saga</typeparam>
    public interface ISagaMachine<TState>
        where TState : ISagaIdentifier 
    {
        void WithMessage<TMessage>(Func<ISagaProcessState<TMessage, TState>, TMessage, ISagaDefined> process) where TMessage : ISagaMessageIdentifier;
        Task Handle<TMessage>(TMessage message) where TMessage : ISagaMessageIdentifier;
    }
}