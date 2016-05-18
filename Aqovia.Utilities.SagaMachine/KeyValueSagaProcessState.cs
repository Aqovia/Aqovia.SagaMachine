using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aqovia.Utilities.SagaMachine.Logging;
using Aqovia.Utilities.SagaMachine.StatePersistance;

namespace Aqovia.Utilities.SagaMachine
{    
    public class KeyValueSagaProcessState<TIncomingMessage, TState> : ISagaProcessState<TIncomingMessage, TState> where TIncomingMessage : ISagaMessageIdentifier where TState : ISagaIdentifier
    {

        private readonly TIncomingMessage _incomingMessage;
        private readonly IKeyValueStore _keyValueStore;
        private readonly Func<IEnumerable<ISagaMessageIdentifier>, Task> _messagePublisher;
        private HashedValue<TState> _currentState;
        private bool _needToSaveState;
        private bool _needToDeleteState;
        private readonly List<ISagaMessageIdentifier> _messagesToPublish;
        private readonly SagaLogState _logState;
        private IEventLoggerFactory _eventLoggerFactory;

        public KeyValueSagaProcessState(TIncomingMessage incomingMessage, IKeyValueStore keyValueStore, Func<IEnumerable<ISagaMessageIdentifier>, Task> messagePublisher, IEventLoggerFactory eventLoggerFactory)
        {
            _incomingMessage = incomingMessage;
            _keyValueStore = keyValueStore;
            _messagePublisher = messagePublisher;
            _eventLoggerFactory = eventLoggerFactory;
            _messagesToPublish = new List<ISagaMessageIdentifier>();
            _logState = new SagaLogState();
        }

        public ISagaProcessState<TIncomingMessage, TState> InitialiseState(Func<TIncomingMessage, TState> initFunc)
        {
            TState stateInit =  initFunc(_incomingMessage);            
            if (stateInit.SagaInstanceId==default(Guid) )
                throw new SagaException("Expected InitialiseState to set SagaInstanceId");
            _currentState = new HashedValue<TState>
            {
                Value = stateInit,
                Hash = string.Empty
            };                                
            _needToSaveState = true;
            return this;
        }

        public ISagaProcessState<TIncomingMessage, TState> ChangeStateIf(Func<TIncomingMessage, TState, TState> state, Func<TIncomingMessage, TState, bool> conditional)
        {
            LoadStateIfNecessary();

            if (conditional(_incomingMessage, _currentState.Value))
            {
                _currentState.Value = state(_incomingMessage, _currentState.Value);
                _needToSaveState = true;
            }
            return this;
        }

        public ISagaProcessState<TIncomingMessage, TState> ChangeState(Func<TIncomingMessage, TState, TState> state)
        {
            LoadStateIfNecessary();
            _currentState.Value = state(_incomingMessage, _currentState.Value);
            _needToSaveState = true;
            return this;
        }


        public ISagaProcessPublish<TIncomingMessage, TState> PublishIf(Func<TIncomingMessage, TState, IEnumerable<ISagaMessageIdentifier>> state, Func<TIncomingMessage, TState, bool> conditional)
        {
            LoadStateIfNecessary();
            if (conditional(_incomingMessage, _currentState.Value))
            {
                foreach (var sagaMessage in state(_incomingMessage, _currentState.Value))
                {
                    sagaMessage.SagaInstanceId = _currentState.Value.SagaInstanceId;
                    _messagesToPublish.Add(sagaMessage);    
                }                
            }
            return this;
        }

        public ISagaProcessPublish<TIncomingMessage, TState> Publish(Func<TIncomingMessage, TState, IEnumerable<ISagaMessageIdentifier>> state)
        {
            LoadStateIfNecessary();
            foreach (var sagaMessage in state(_incomingMessage, _currentState.Value))
            {
                sagaMessage.SagaInstanceId = _currentState.Value.SagaInstanceId;
                _messagesToPublish.Add(sagaMessage);
            }
            return this;
        }

        public ISagaProcessStop<TIncomingMessage, TState> LogIf(Action<TIncomingMessage, TState, ISagaLogState> logger, Func<TIncomingMessage, TState, bool> state)
        {
            LoadStateIfNecessary();
            if (state(_incomingMessage, _currentState.Value))
            {
                logger(_incomingMessage, _currentState.Value, _logState);
            }
            return this;
        }

        public ISagaProcessStop<TIncomingMessage, TState> Log(Action<TIncomingMessage, TState, ISagaLogState> logger)
        {
            LoadStateIfNecessary();
            logger(_incomingMessage, _currentState.Value,_logState);            
            return this;
        }


        public ISagaProcessStop<TIncomingMessage, TState> StopSagaIf(Func<TIncomingMessage, TState, bool> state)
        {
            LoadStateIfNecessary();
            if (state(_incomingMessage, _currentState.Value))
            {
                _needToDeleteState = true;
            }
            return this;
        }

        public ISagaProcessStop<TIncomingMessage, TState> StopSaga()
        {
            _needToDeleteState = true;
            return this;
        }

        public ISagaDefined Execute()
        {
            return ExecuteAsync().Result;
        }
        public async Task<ISagaDefined> ExecuteAsync()
        {
            LoadStateIfNecessary();

            string uniqueLockToken;
            var isLocked = _keyValueStore.TakeLockWithDefaultExpiryTime(_currentState.Value.SagaInstanceId.ToString(), out uniqueLockToken);
            if (!isLocked)
            {
                throw new SagaHasConcurrentLockException("A concurrent SagaMachine has already locked the saga");
            }

            foreach (var logStateMessage in _logState.GetLogMessages())
            {
                IEventLogger logger = _eventLoggerFactory.GetRequestEventLogger(_incomingMessage.GetType().Name + "SagaMessage");
                if (logStateMessage.Properties != null)
                {
                    foreach (var property in logStateMessage.Properties)
                    {
                        logger[property.Name] = property.Value;
                    }
                }
                if (logStateMessage.Level == SagaLogLevel.Info)
                {
                    logger.LogInfo(logStateMessage.Message);
                }
                else if (logStateMessage.Level == SagaLogLevel.Warn)
                {
                    logger.LogWarn(logStateMessage.Message);
                }
                else
                {
                    logger.LogError(logStateMessage.Message);
                }
            }

            try
            {
                await _messagePublisher(_messagesToPublish).ConfigureAwait(false);

                if (_needToDeleteState)
                {
                    if (!_keyValueStore.Remove(_currentState.Value.SagaInstanceId.ToString(), _currentState.Hash))
                    {
                        throw new SagaStateStaleException("State has since changed. Can't remove");
                    }
                }
                else
                {
                    SaveStateIfNecessary();
                }
            }
            finally
            {
                _keyValueStore.ReleaseLock(_currentState.Value.SagaInstanceId.ToString(), uniqueLockToken);
            }
            
            return null;
        }
        
        private void LoadStateIfNecessary()
        {
            if (_currentState != null) return;
            _currentState = _keyValueStore.GetValue<TState>(_incomingMessage.SagaInstanceId.ToString());
        }

        private void SaveStateIfNecessary()
        {
            if (!_needToSaveState) return;
            if (_currentState == null)
            {
                throw new SagaException("Saga state has been set to NULL");
            }

            if (!_keyValueStore.TrySetValue(_currentState.Value.SagaInstanceId.ToString(), _currentState.Value,_currentState.Hash))
            {
                throw new SagaStateStaleException("State has since changed. Can't save");
            }
            
        }
    }
}