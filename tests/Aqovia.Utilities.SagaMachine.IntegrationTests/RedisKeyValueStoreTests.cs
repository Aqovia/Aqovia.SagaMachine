using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using FluentAssertions;
using Aqovia.Utilities.SagaMachine.StatePersistance;
using StackExchange.Redis;
using Xunit;

namespace Aqovia.Utilities.SagaMachine.IntegrationTests
{
    public class RedisKeyValueStoreTests : IDisposable
    {

        private readonly RedisKeyValueStore _store;
        private readonly List<string> _cleanupKeys;

        public RedisKeyValueStoreTests()
        {
            _store = new RedisKeyValueStore();
            _cleanupKeys = new List<string>();
        }

        [Fact(DisplayName = "Should not allow duplicate keys")]
        public void StoreShouldNotAllowDuplicateCreation()
        {
            string uniqueKey = Guid.NewGuid().ToString();
            _cleanupKeys.Add(uniqueKey);

            _store.TrySetValue(uniqueKey, "initState", string.Empty).Should().BeTrue();
            _store.TrySetValue(uniqueKey, "initState", string.Empty).Should().BeFalse();
        }

        [Fact(DisplayName = "Should store updates when valid hash is used")]
        public void StoreShouldUpdateWithValidHash()
        {
            //Arrange
            string uniqueKey = Guid.NewGuid().ToString();
            _cleanupKeys.Add(uniqueKey);

            //Act

            //Create state
            _store.TrySetValue(uniqueKey, "initState", string.Empty);
            //Get current state
            HashedValue<string> value = _store.GetValue<string>(uniqueKey);
            //Save modified version
            _store.TrySetValue(uniqueKey, "modifiedState", value.Hash).Should().BeTrue();

            //Assert
            HashedValue<string> modifiedvalue = _store.GetValue<string>(uniqueKey);
            modifiedvalue.Value.Should().Be("modifiedState");
        }

        [Fact(DisplayName = "Should not update when old hash is used")]
        public void StoreShouldNotAllowOldHash()
        {
            //Arrange
            string uniqueKey = Guid.NewGuid().ToString();
            _cleanupKeys.Add(uniqueKey);

            //Act

            //Create state
            _store.TrySetValue(uniqueKey, "initState", string.Empty);
            //Get current state
            HashedValue<string> value = _store.GetValue<string>(uniqueKey);
            //Save modified version
            _store.TrySetValue(uniqueKey, "modifiedState", value.Hash).Should().BeTrue();
            _store.TrySetValue(uniqueKey, "modifiedStateAgain", value.Hash).Should().BeFalse();
            //Assert
            HashedValue<string> modifiedvalue = _store.GetValue<string>(uniqueKey);
            modifiedvalue.Value.Should().Be("modifiedState");
        }

        [Fact(DisplayName = "Should delete when valid hash is used")]
        public void DeleteShouldDeleteValidHash()
        {
            //Arrange
            string uniqueKey = Guid.NewGuid().ToString();
            _cleanupKeys.Add(uniqueKey);

            //Act

            //Create state
            _store.TrySetValue(uniqueKey, "initState", string.Empty);
            //Get current state
            HashedValue<string> value = _store.GetValue<string>(uniqueKey);
            _store.Remove(uniqueKey, value.Hash).Should().BeTrue();

            //Assert
            HashedValue<string> modifiedvalue = _store.GetValue<string>(uniqueKey);
            modifiedvalue.Value.Should().BeNull();
        }

        [Fact(DisplayName = "Should not delete when old hash is used")]
        public void DeleteShouldNotDeleteOldHash()
        {
            //Arrange
            string uniqueKey = Guid.NewGuid().ToString();
            _cleanupKeys.Add(uniqueKey);

            //Act

            //Create state
            _store.TrySetValue(uniqueKey, "initState", string.Empty);
            //Get current state
            HashedValue<string> value = _store.GetValue<string>(uniqueKey);
            _store.TrySetValue(uniqueKey, "modifiedState", value.Hash).Should().BeTrue();
            _store.Remove(uniqueKey, value.Hash).Should().BeFalse();

            //Assert
            HashedValue<string> modifiedvalue = _store.GetValue<string>(uniqueKey);
            modifiedvalue.Value.Should().Be("modifiedState");
        }

        [Fact(DisplayName = "Ping should return true")]
        public void PingShouldSucceed()
        {
            _store.Ping().Should().NotBe(default(TimeSpan));
        }

        [Fact]
        public async void TakingLockShouldSucceed()
        {
            // Arrange
            const string key = "integration-test-key";
            _store.Remove(key);

            _store.TrySetValue(key, "dummy-value", null);
            _cleanupKeys.Add(key);

            // Act
            Guid token = Guid.NewGuid();
            var hasLock = await _store.TakeLock(key, token, 1000);

            await _store.ReleaseLock(key, token);

            // Assert
            hasLock.Should().BeTrue();
        }

        [Fact]
        public async void ConcurrentTakeLockShouldFail()
        {
            // Arrange
            const string key = "integration-test-key";
            _store.Remove(key);

            _store.TrySetValue(key, "dummy-value", null);
            _cleanupKeys.Add(key);

            // Act
            var tokenFirst = Guid.NewGuid();
            await _store.TakeLock(key, tokenFirst, 30000);

            var tokenSecond = Guid.NewGuid();
            var hasLockSecond = await _store.TakeLock(key, tokenSecond, 1);

            await _store.ReleaseLock(key, tokenFirst);

            // Assert
            hasLockSecond.Should().BeFalse();
        }

        [Fact]
        public async void ConcurrentTakeLockWhenLockIsExhaustedShouldSucceed()
        {
            // Arrange
            const string key = "integration-test-key";
            _store.Remove(key);

            _store.TrySetValue(key, "dummy-value", null);
            _cleanupKeys.Add(key);

            // Act
            var tokenFirst = Guid.NewGuid();
            await _store.TakeLock(key, tokenFirst, 10);

            await Task.Delay(100);

            var tokenSecond = Guid.NewGuid();
            var hasLockSecond = await _store.TakeLock(key, tokenSecond, 1);

            await _store.ReleaseLock(key, tokenSecond);

            // Assert
            hasLockSecond.Should().BeTrue();
        }

        [Fact]
        public async void ReleasingLockShouldSucceed()
        {
            // Arrange
            const string key = "integration-test-key";
            _store.Remove(key);

            _store.TrySetValue(key, "dummy-value", null);
            _cleanupKeys.Add(key);

            // Act
            Guid token = Guid.NewGuid();
            await _store.TakeLock(key, token, 1000);

            var hasReleasedLock = await _store.ReleaseLock(key, token);

            // Assert
            hasReleasedLock.Should().BeTrue();
        }

        [Fact]
        public async void ReleasingLockWithWrongTokenShouldFail()
        {
            // Arrange
            const string key = "integration-test-key";
            _store.Remove(key);

            _store.TrySetValue(key, "dummy-value", null);
            _cleanupKeys.Add(key);

            // Act
            Guid token = Guid.NewGuid();
            await _store.TakeLock(key, token, 1000);

            Guid wrongToken = Guid.NewGuid();
            var hasReleasedLock = await _store.ReleaseLock(key, wrongToken);

            // Assert
            hasReleasedLock.Should().BeFalse();
        }

        [Fact]
        public async void ReleasingExhaustedLockShouldFail()
        {
            // Arrange
            const string key = "integration-test-key";
            _store.Remove(key);

            _store.TrySetValue(key, "dummy-value", null);
            _cleanupKeys.Add(key);

            // Act
            Guid token = Guid.NewGuid();
            await _store.TakeLock(key, token, 10);

            await Task.Delay(100);

            var hasReleasedLock = await _store.ReleaseLock(key, token);

            // Assert
            hasReleasedLock.Should().BeFalse();
        }

        [Fact]
        public void ConstructStoreUsingConnectionStringShouldSucceed()
        {
            var connectionString = ConfigurationManager.AppSettings["SagaKeyValueStoreConnectionString"];
            using (var store = new RedisKeyValueStore(connectionString))
            {
                store.Ping();
            }
        }

        public void Dispose()
        {
            foreach (var key in _cleanupKeys)
            {
                _store.Remove(key);
            }
            _store.Dispose();
        }
    }
}
