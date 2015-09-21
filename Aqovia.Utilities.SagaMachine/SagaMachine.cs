using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aqovia.Utilities.SagaMachine.Logging;
using Aqovia.Utilities.SagaMachine.StatePersistance;

namespace Aqovia.Utilities.SagaMachine
{
    public class SagaMachine<TState> : ISagaMachine<TState> where TState : ISagaIdentifier
    {
        private readonly Dictionary<Type, Action<ISagaMessageIdentifier>> _processRegistry;
        private readonly IKeyValueStore _keyValueStore;
        private readonly Func<IEnumerable<ISagaMessageIdentifier>, Task> _messagePublisher;
        private readonly IEventLoggerFactory _eventLoggerFactory;

        public SagaMachine(IKeyValueStore keyValueStore, Func<IEnumerable<ISagaMessageIdentifier>, Task> messagePublisher, IEventLoggerFactory eventLoggerFactory)
        {
            _keyValueStore = keyValueStore;
            _messagePublisher = messagePublisher;
            _eventLoggerFactory = eventLoggerFactory;
            _processRegistry = new Dictionary<Type, Action<ISagaMessageIdentifier>>();
        }

        public void WithMessage<TMessage>(Func<ISagaProcessState<TMessage, TState>, TMessage, ISagaDefined> process) where TMessage : ISagaMessageIdentifier
        {
            Action<ISagaMessageIdentifier> value = msg => { process(new KeyValueSagaProcessState<TMessage, TState>((TMessage)msg, _keyValueStore, _messagePublisher, _eventLoggerFactory), (TMessage)msg); };

            _processRegistry[typeof (TMessage)] = value;
        }

        public async Task Handle<TMessage>(TMessage message) where TMessage : ISagaMessageIdentifier
        {
            await Task.Run(() => ProcessMessage(message));
        }

        private void ProcessMessage<TMessage>(TMessage message) where TMessage : ISagaMessageIdentifier
        {
            int retryFailLimit = 30;
            while (retryFailLimit>0)
            {                
                try
                {
                    _processRegistry[typeof(TMessage)](message);
                    return;
                }
                catch (SagaStateStaleException)
                {
                    //Key value store we read was stale. Try again.
                    retryFailLimit--;
                    if (retryFailLimit < 1)
                        throw;
                }
            }            
        }
    }
}