using System;
using System.Collections.Generic;
using FluentAssertions;
using Aqovia.Utilities.SagaMachine.StatePersistance;
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

        public void Dispose()
        {
            foreach (var key in _cleanupKeys)
            {
                _store.Remove(key);
            }
        }
    }
}
