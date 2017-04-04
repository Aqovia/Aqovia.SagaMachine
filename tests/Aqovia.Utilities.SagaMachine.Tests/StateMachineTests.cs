using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aqovia.Utilities.SagaMachine.Logging;
using Aqovia.Utilities.SagaMachine.StatePersistance;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aqovia.Utilities.SagaMachine.Tests
{
    public class StateMachineTests
    {
        private readonly Mock<IKeyValueStore> _keyValueStoreMock;
        private readonly Mock<Func<IEnumerable<ISagaMessageIdentifier>, Task>> _mockPublisher;
        private SagaMachine<TestState> _sagaMachine;
        private readonly Mock<IEventLoggerFactory> _mockEventloggerFactory;
        private Mock<IEventLogger> _mockEventLogger;

        private class TestState : ISagaIdentifier
        {
            public Guid SagaInstanceId { get; set; }
            public string SomeStateVariable { get; set; }
            public string DontTouchStateVariable { get; set; }
        }

        private class HelloMessage : ISagaMessageIdentifier
        {
            public Guid SagaInstanceId { get; set; }
            public string Message { get; set; }
        }

        private class SomeOtherMessage : ISagaMessageIdentifier
        {
            public Guid SagaInstanceId { get; set; }
        }

        private class GoodbyeMessage : ISagaMessageIdentifier
        {
            public Guid SagaInstanceId { get; set; }
            public string Message { get; set; }
        }

        private class DontSendMessage : ISagaMessageIdentifier
        {
            public Guid SagaInstanceId { get; set; }
        }

        public StateMachineTests()
        {
            _keyValueStoreMock = new Mock<IKeyValueStore>();
            _mockPublisher = new Mock<Func<IEnumerable<ISagaMessageIdentifier>, Task>>();
            _mockEventloggerFactory = new Mock<IEventLoggerFactory>();
            _mockEventLogger = new Mock<IEventLogger>();
            _mockEventloggerFactory.Setup(o => o.GetRequestEventLogger(It.IsAny<string>())).Returns(_mockEventLogger.Object);
            _sagaMachine = new SagaMachine<TestState>(_keyValueStoreMock.Object, _mockPublisher.Object, _mockEventloggerFactory.Object);
        }

        [Fact(DisplayName = "Should save state from InitialiseState")]
        public async Task SagaMachineShouldInitialiseState()
        {
            //Arrange
            var sagaInstanceId = Guid.NewGuid();
            _keyValueStoreMock.Setup(o => o.TrySetValue(sagaInstanceId.ToString(), It.IsAny<TestState>(), string.Empty)).Returns(true);
            _keyValueStoreMock.Setup(o => o.TakeLockWithDefaultExpiryTime(sagaInstanceId.ToString(), It.IsAny<Guid>())).ReturnsAsync(true);
            _keyValueStoreMock.Setup(o => o.ReleaseLock(sagaInstanceId.ToString(), It.IsAny<Guid>())).ReturnsAsync(true);

            _sagaMachine
                .WithMessage<HelloMessage>((proccess, msg) => proccess
                    .InitialiseState(hello => new TestState
                    {
                        SomeStateVariable = hello.Message,
                        SagaInstanceId = sagaInstanceId
                    })
                    .Execute()
                );

            //Act
            await _sagaMachine.Handle(new HelloMessage
            {
                Message = "Initialised",
                SagaInstanceId = sagaInstanceId
            });

            //Assert
            _keyValueStoreMock.Verify(o => o.TrySetValue(sagaInstanceId.ToString(), It.Is<TestState>(state => state.SomeStateVariable == "Initialised"), string.Empty), Times.AtLeastOnce);
        }

        [Fact(DisplayName = "Should not allow InitialiseState without Id")]
        public void SagaMachineShouldHaveInitialiseStateId()
        {
            _sagaMachine
                .WithMessage<HelloMessage>((proccess, msg) => proccess
                    .InitialiseState(hello => new TestState
                    {
                        SomeStateVariable = hello.Message
                    })
                    .Execute()
                );

            Func<Task> act = async () =>
            {
                await _sagaMachine.Handle(new HelloMessage
                {
                    Message = "Initialised"
                }).ConfigureAwait(false);
            };
            act.ShouldThrow<SagaException>();
        }

        [Fact(DisplayName = "Should only publish conditional met messages")]
        public async Task SagaMachineShouldPublishConditionalMessages()
        {
            //Arrange
            _keyValueStoreMock.Setup(o => o.TakeLockWithDefaultExpiryTime(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);
            _keyValueStoreMock.Setup(o => o.ReleaseLock(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);

            _keyValueStoreMock.Setup(o => o.GetValue<TestState>(It.IsAny<string>()))
                .Returns(new HashedValue<TestState>
                {
                    Hash = "123",
                    Value = new TestState()
                });
            _sagaMachine
               .WithMessage<HelloMessage>((proccess, msg) => proccess
                   .PublishIf((msgForPub, state) => new[] { new GoodbyeMessage() }, (msgForCond, state) => true)
                   .PublishIf((msgForPub, state) => new[] { new DontSendMessage() }, (msgForCond, state) => false)
                   .Execute()
               );

            //Act

            await _sagaMachine.Handle(new HelloMessage());
            //Assert
            _mockPublisher.Verify(pub => pub(It.Is<IEnumerable<ISagaMessageIdentifier>>(p => p.OfType<GoodbyeMessage>().Any())), Times.Once);
            _mockPublisher.Verify(pub => pub(It.Is<IEnumerable<ISagaMessageIdentifier>>(p => p.OfType<DontSendMessage>().Any())), Times.Never);
        }

        [Fact(DisplayName = "Should log conditionaly")]
        public async Task SagaMachineShouldLogConditionaly()
        {
            //Arrange
            _keyValueStoreMock.Setup(o => o.TakeLockWithDefaultExpiryTime(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);
            _keyValueStoreMock.Setup(o => o.ReleaseLock(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);

            _keyValueStoreMock.Setup(o => o.GetValue<TestState>(It.IsAny<string>()))
                .Returns(new HashedValue<TestState>
                {
                    Hash = "123",
                    Value = new TestState()
                });
            _sagaMachine
               .WithMessage<HelloMessage>((proccess, msg) => proccess
                   .LogIf((msgLogFor, stateLogFor, logger) => { logger.LogInfo("Should log this"); }, (msgForCond, state) => true)
                   .LogIf((msgLogFor, stateLogFor, logger) => { logger.LogInfo("Should not log this"); }, (msgForCond, state) => false)
                   .Execute()
               );

            //Act

            await _sagaMachine.Handle(new HelloMessage());
            //Assert

            _mockEventLogger.Verify(o => o.LogInfo(It.Is<string>(m => m == "Should log this")), Times.Once);
            _mockEventLogger.Verify(o => o.LogInfo(It.Is<string>(m => m == "Should not log this")), Times.Never);
        }

        [Fact(DisplayName = "Should populate sagaid on published messages")]
        public async Task SagaMachineShouldPopulateSagaId()
        {
            //Arrange
            var sagaInstanceId = Guid.NewGuid();
            _keyValueStoreMock.Setup(o => o.TakeLockWithDefaultExpiryTime(sagaInstanceId.ToString(), It.IsAny<Guid>())).ReturnsAsync(true);
            _keyValueStoreMock.Setup(o => o.ReleaseLock(sagaInstanceId.ToString(), It.IsAny<Guid>())).ReturnsAsync(true);
            _keyValueStoreMock.Setup(o => o.GetValue<TestState>(It.IsAny<string>()))
                .Returns(new HashedValue<TestState>
                {
                    Hash = "123",
                    Value = new TestState
                    {
                        SagaInstanceId = sagaInstanceId
                    }
                });
            _sagaMachine
               .WithMessage<HelloMessage>((proccess, msg) => proccess
                   .PublishIf((msgForPub, state) => new[] { new GoodbyeMessage()}, (msgForCond, state) => true)
                   .Publish((msgForPub, state) => new[] { new GoodbyeMessage()})
                   .Execute()
               );

            //Act
            await _sagaMachine.Handle(new HelloMessage()
            {
                SagaInstanceId = sagaInstanceId
            });

            //Assert
            _mockPublisher.Verify(pub => pub(It.Is<IEnumerable<ISagaMessageIdentifier>>(p => p.All(o => o.SagaInstanceId == sagaInstanceId))), Times.Once);
        }

        [Fact(DisplayName = "Should run logic for correct message")]
        public async Task SagaMachineShouldRespondToCorrectMessages()
        {
            //Arrange
            _keyValueStoreMock.Setup(o => o.TakeLockWithDefaultExpiryTime(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);
            _keyValueStoreMock.Setup(o => o.ReleaseLock(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);

            _keyValueStoreMock.Setup(o => o.GetValue<TestState>(It.IsAny<string>()))
                .Returns(new HashedValue<TestState>
                {
                    Hash = "123",
                    Value = new TestState()
                });
            _sagaMachine
               .WithMessage<HelloMessage>((proccess, msg) => proccess
                   .Publish((msgForPub, state) => new[] { new GoodbyeMessage() })
                   .Execute()
               );

            _sagaMachine
               .WithMessage<SomeOtherMessage>((proccess, msg) => proccess
                   .Execute()
               );

            //Act
            await _sagaMachine.Handle(new SomeOtherMessage());

            //Assert
            _mockPublisher.Verify(pub => pub(It.Is<IEnumerable<ISagaMessageIdentifier>>(p => p.OfType<GoodbyeMessage>().Any())), Times.Never);
        }

        [Fact(DisplayName = "Should save mutated state")]
        public async Task SagaMachineShouldSaveMutatedState()
        {
            //Arrange
            _keyValueStoreMock.Setup(o => o.TakeLockWithDefaultExpiryTime(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);
            _keyValueStoreMock.Setup(o => o.ReleaseLock(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);

            _keyValueStoreMock.Setup(o => o.GetValue<TestState>(It.IsAny<string>()))
                .Returns(new HashedValue<TestState>
                {
                    Hash = "123",
                    Value = new TestState
                    {
                        DontTouchStateVariable = "Untouched"
                    }
                });

            _keyValueStoreMock.Setup(o => o.TrySetValue(It.IsAny<string>(), It.IsAny<TestState>(), It.IsAny<string>())).Returns(true);

            _sagaMachine
               .WithMessage<HelloMessage>((proccess, msg) => proccess
                   .ChangeStateIf((msgForPub, state) =>
                   {
                       state.SomeStateVariable = "Changed";
                       return state;

                   }, (msgForCond, state) => true)
                   .ChangeStateIf((msgForPub, state) =>
                   {
                       state.DontTouchStateVariable = "Touched";
                       return state;

                   }, (msgForCond, state) => false)
                   .Execute()
               );

            //Act

            await _sagaMachine.Handle(new HelloMessage());
            //Assert
            _keyValueStoreMock.Verify(o => o.TrySetValue(It.IsAny<string>(), It.Is<TestState>(state => state.SomeStateVariable == "Changed"), It.IsAny<string>()), Times.Once);
            _keyValueStoreMock.Verify(o => o.TrySetValue(It.IsAny<string>(), It.Is<TestState>(state => state.DontTouchStateVariable == "Touched"), It.IsAny<string>()), Times.Never);
        }


        [Fact(DisplayName = "Messages will be re-published if there is a stale state exception")]
        public async Task SagaMachineShouldRetryStaleState()
        {
            //Arrange
            var sageInstanceId = Guid.NewGuid();
            var valuesToReturn = new Queue<HashedValue<TestState>>();

            valuesToReturn.Enqueue(new HashedValue<TestState>
            {
                Hash = "123",
                Value = new TestState
                {
                    SomeStateVariable = "StaleVersion"
                }
            });

            valuesToReturn.Enqueue(new HashedValue<TestState>
            {
                Hash = "124",
                Value = new TestState
                {
                    SomeStateVariable = "FreshVersion"
                }
            });

            //Return hash state 123, then return hash state 124
            _keyValueStoreMock.Setup(o => o.GetValue<TestState>(It.IsAny<string>())).Returns(valuesToReturn.Dequeue);

            //Only allow save of hash "124"
            _keyValueStoreMock.Setup(o => o.TrySetValue(It.IsAny<string>(), It.IsAny<TestState>(), It.IsAny<string>()))
                .Returns((string key, TestState value, string hash) => hash == "124");

            var locksReturnSequence = new Queue<bool>();
            locksReturnSequence.Enqueue(false);
            locksReturnSequence.Enqueue(true);

            _keyValueStoreMock.Setup(o => o.TakeLockWithDefaultExpiryTime(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);
            _keyValueStoreMock.Setup(o => o.Remove(It.IsAny<string>(), It.IsAny<string>())).Returns(locksReturnSequence.Dequeue());
            _keyValueStoreMock.Setup(o => o.ReleaseLock(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);

            _sagaMachine
               .WithMessage<HelloMessage>((proccess, msg) => proccess
                   .ChangeStateIf((msgForPub, state) => state, (msgForCond, state) => true)
                   .Publish((pubMsg, state) => new[]{ new GoodbyeMessage
                   {
                       Message = state.SomeStateVariable,
                       SagaInstanceId = sageInstanceId
                   }})
                   .Execute()
               );

            //Act
            await _sagaMachine.Handle(new HelloMessage()
            {
                SagaInstanceId = sageInstanceId
            });

            //Assert
            _mockPublisher.Verify(pub => pub(It.Is<IEnumerable<ISagaMessageIdentifier>>(p => p.OfType<GoodbyeMessage>().Any(m => m.Message == "StaleVersion"))), Times.Once);
            _mockPublisher.Verify(pub => pub(It.Is<IEnumerable<ISagaMessageIdentifier>>(p => p.OfType<GoodbyeMessage>().Any(m => m.Message == "FreshVersion"))), Times.Once);
        }

        [Fact(DisplayName = "Should delete state at the end of the saga")]
        public async Task SagaMachineShouldDeleteStateAtEndOfSaga()
        {
            //Arrange
            _keyValueStoreMock.Setup(o => o.TakeLockWithDefaultExpiryTime(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);
            _keyValueStoreMock.Setup(o => o.ReleaseLock(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);
            _keyValueStoreMock.Setup(o => o.TrySetValue(It.IsAny<string>(), It.IsAny<Object>(), It.IsAny<string>())).Returns(true);
            _keyValueStoreMock.Setup(o => o.Remove(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
            _keyValueStoreMock.Setup(o => o.GetValue<TestState>(It.IsAny<string>()))
                .Returns(new HashedValue<TestState>
                {
                    Hash = Guid.Empty.ToString(),
                    Value = new TestState()
                });

            _sagaMachine
               .WithMessage<HelloMessage>((proccess, msg) => proccess
                   .InitialiseState(inMsg => new TestState
                   {
                       SagaInstanceId = Guid.NewGuid()
                   })
                   .Execute()
               );

            _sagaMachine
               .WithMessage<GoodbyeMessage>((proccess, msg) => proccess
                   .StopSagaIf((msgStop, state) => true)
                   .Execute()
               );

            //Act
            await _sagaMachine.Handle(new HelloMessage());
            await _sagaMachine.Handle(new GoodbyeMessage());
            //Assert
            _keyValueStoreMock.Verify(o => o.Remove(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact(DisplayName = "Saga state is not saved when Saga is both Initialised and Stopped")]
        public async Task SagaMachineStateIsNotSavedWhenSagaIsBothInitialisedAndStopped()
        {
            //Arrange
            _keyValueStoreMock.Setup(o => o.TakeLockWithDefaultExpiryTime(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);
            _keyValueStoreMock.Setup(o => o.ReleaseLock(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);
            _keyValueStoreMock.Setup(o => o.TrySetValue(It.IsAny<string>(), It.IsAny<Object>(), It.IsAny<string>())).Returns(true);
            _keyValueStoreMock.Setup(o => o.Remove(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
            _keyValueStoreMock.Setup(o => o.GetValue<TestState>(It.IsAny<string>()))
                .Returns(new HashedValue<TestState>
                {
                    Hash = Guid.Empty.ToString(),
                    Value = new TestState()
                });

            _sagaMachine
               .WithMessage<HelloMessage>((proccess, msg) => proccess
                   .InitialiseState(inMsg => new TestState
                   {
                       SagaInstanceId = Guid.NewGuid()
                   })
                   .StopSagaIf((msgStop, state) => true)
                   .Execute()
               );

            //Act
            await _sagaMachine.Handle(new HelloMessage());
            //Assert
            _keyValueStoreMock.Verify(o => o.Remove(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void SagaMachine_Should_Not_Stop_Saga_If_An_Exception_Happens()
        {
            //Arrange
            _keyValueStoreMock.Setup(o => o.TakeLockWithDefaultExpiryTime(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);
            _keyValueStoreMock.Setup(o => o.ReleaseLock(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);

            _keyValueStoreMock.Setup(o => o.GetValue<TestState>(It.IsAny<string>()))
                .Returns(new HashedValue<TestState>
                {
                    Hash = Guid.Empty.ToString(),
                    Value = new TestState()
                });

            // We need the below to set the return value
            _keyValueStoreMock.Setup(kv => kv.Remove(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

            Func<IEnumerable<ISagaMessageIdentifier>, Task> publisher = async messages =>
            {
                foreach (var message in messages)
                {
                    if (message is GoodbyeMessage)
                    {
                        await Task.Delay(1000);
                        throw new Exception();
                    }

                    await Task.Delay(500);
                }
            };

            //Act
            _sagaMachine = new SagaMachine<TestState>(_keyValueStoreMock.Object, publisher, _mockEventloggerFactory.Object);
            _sagaMachine
               .WithMessage<HelloMessage>((proccess, msg) => proccess
                   .Publish((msgForPub, state) => new[] { new GoodbyeMessage() })
                   .StopSaga()
                   .Execute()
               );

            Func<Task> act = async () => { await _sagaMachine.Handle(new HelloMessage()).ConfigureAwait(false); };
            act.ShouldThrow<Exception>();

            //Assert
            _keyValueStoreMock.Verify(o => o.TrySetValue(It.IsAny<string>(), It.IsAny<TestState>(), It.IsAny<string>()), Times.Never);
            _keyValueStoreMock.Verify(kv => kv.Remove(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async void SagaMachine_RequestLock_When_Executed()
        {
            // Arrange
            _keyValueStoreMock.Setup(kv => kv.TakeLockWithDefaultExpiryTime(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);

            _keyValueStoreMock.Setup(o => o.GetValue<TestState>(It.IsAny<string>()))
                .Returns(new HashedValue<TestState>
                {
                    Hash = Guid.Empty.ToString(),
                    Value = new TestState()
                });

            // Act
            _sagaMachine.WithMessage<HelloMessage>((proccess, msg) => proccess.Execute());
            await _sagaMachine.Handle(new HelloMessage());

            // Assert

            _keyValueStoreMock.Verify(kv => kv.TakeLockWithDefaultExpiryTime(It.IsAny<string>(), It.IsAny<Guid>()), Times.Once);
        }

        [Fact]
        public async void SagaMachine_WhenSucessfullyAcquiredLock_ReleaseLock()
        {
            // Arrange
            _keyValueStoreMock.Setup(kv => kv.TakeLockWithDefaultExpiryTime(It.IsAny<string>(), It.IsAny<Guid>()))
                .ReturnsAsync(true);

            _keyValueStoreMock.Setup(o => o.GetValue<TestState>(It.IsAny<string>()))
                .Returns(new HashedValue<TestState>
                {
                    Hash = Guid.Empty.ToString(),
                    Value = new TestState()
                });

            // Act
            _sagaMachine.WithMessage<HelloMessage>((proccess, msg) => proccess.Execute());
            await _sagaMachine.Handle(new HelloMessage());

            // Assert
            _keyValueStoreMock.Verify(kv => kv.ReleaseLock(It.IsAny<string>(), It.IsAny<Guid>()), Times.Once);
        }
    }
}
