using System;
using Aqovia.Utilities.SagaMachine.StatePersistance;
using FluentAssertions;
using Xunit;

namespace Aqovia.Utilities.SagaMachine.Tests.StatePersistance
{
    public class InMemoryKeyValueStoreTests
    {
        private readonly InMemoryKeyValueStore _inMemoryStore;
        public InMemoryKeyValueStoreTests()
        {
            
            // Object under test
            _inMemoryStore = new InMemoryKeyValueStore();

            AddNoiseToStore();
        }

        private void AddNoiseToStore()
        {
            
        }

        [Fact]
        public void TryToAddNew_KeyValueDoesNotExistInStore_AddSuccessful()
        {
            // Act
            var addResult = _inMemoryStore.TrySetValue("sample-key", "sample-value", null);

            // Assert
            addResult.Should().Be(true);
            _inMemoryStore.InMemoryStore.Should().NotBeNull();
            _inMemoryStore.InMemoryStore.Keys.Should().Contain(k => k == "sample-key");
            _inMemoryStore.InMemoryStore["sample-key"].Value.Should().Be("sample-value");
        }

        [Fact]
        public void TryToAddNew_KeyValueExistInStore_AddFails()
        {
            // Arrange
            _inMemoryStore.InMemoryStore.TryAdd("existing-key", new HashedValue<object> { Value  = "existing-value", Hash = "existing-hash"});

            // Act
            var addResult = _inMemoryStore.TrySetValue("existing-key", "new-value", null);

            // Assert
            addResult.Should().Be(false);
            _inMemoryStore.InMemoryStore.Should().NotBeNull();
            _inMemoryStore.InMemoryStore.Keys.Should().Contain(k => k == "existing-key");
            _inMemoryStore.InMemoryStore["existing-key"].Value.Should().Be("existing-value");
        }

        [Fact]
        public void TryToUpdate_KeyValueExistInStore_AddSuccessful()
        {
            // Arrange
            var oldHash = InMemoryKeyValueStore.GetMmd5Hash("existing-value");
            _inMemoryStore.InMemoryStore.TryAdd("existing-key", new HashedValue<object> { Value = "existing-value", Hash = oldHash });

            // Act
            var addResult = _inMemoryStore.TrySetValue("existing-key", "new-value", oldHash);

            // Assert
            addResult.Should().Be(true);
            _inMemoryStore.InMemoryStore.Should().NotBeNull();
            _inMemoryStore.InMemoryStore.Keys.Should().Contain(k => k == "existing-key");
            _inMemoryStore.InMemoryStore["existing-key"].Value.Should().Be("new-value");
        }

        [Fact]
        public void TryToUpdate_KeyValueExistButValueHasChangedInStore_AddFails()
        {
            // Arrange
            var oldHash = InMemoryKeyValueStore.GetMmd5Hash("changed-value");
            _inMemoryStore.InMemoryStore.TryAdd("existing-key", new HashedValue<object> { Value = "changed-value", Hash = oldHash });

            // Act
            var addResult = _inMemoryStore.TrySetValue("existing-key", "new-value", "different-hash");

            // Assert
            addResult.Should().Be(false);
            _inMemoryStore.InMemoryStore.Should().NotBeNull();
            _inMemoryStore.InMemoryStore.Keys.Should().Contain(k => k == "existing-key");
            _inMemoryStore.InMemoryStore["existing-key"].Value.Should().Be("changed-value");
        }

        [Fact]
        public void GetValue_ExistInStore_GetValueSuccessfully()
        {
            // Arrange
            _inMemoryStore.InMemoryStore.TryAdd("existing-key", new HashedValue<object> { Value = "existing-value", Hash = "existing-hash" });

            // Act
            var value =_inMemoryStore.GetValue<string>("existing-key");

            // Assert
            value.Should().NotBeNull();
            value.Value.Should().Be("existing-value");
            value.Hash.Should().Be("existing-hash");
        }

        [Fact]
        public void GetValue_DoesNotExistInStore_NullValueReturned()
        {
            // Act
            var value = _inMemoryStore.GetValue<string>("missing-key");

            // Assert
            value.Should().BeNull();
        }

        [Fact]
        public void GetValue_ExistInStoreWithDifferentType_ThrowException()
        {
            // Arrange
            _inMemoryStore.InMemoryStore.TryAdd("existing-key", new HashedValue<object> { Value = "existing-value", Hash = "existing-hash" });

            Action act = () =>
            {
                _inMemoryStore.GetValue<DateTime>("existing-key");
            };
            act.ShouldThrow<Exception>();
        }

        [Fact]
        public void RemoveWithHash_ExistingKeyValue_RemoveSuccessful()
        {
            // Arrange
            _inMemoryStore.InMemoryStore.TryAdd("existing-key", new HashedValue<object> { Value = "existing-value", Hash = "existing-hash" });

            // Act
            var removeResult = _inMemoryStore.Remove("existing-key", "existing-hash");

            // Assert
            removeResult.Should().Be(true);
            _inMemoryStore.InMemoryStore.Keys.Should().NotContain(k => k == "existing-key");
        }

        [Fact]
        public void RemoveWithHash_ExistingKeyValueChanged_RemoveFails()
        {
            // Arrange
            _inMemoryStore.InMemoryStore.TryAdd("existing-key", new HashedValue<object> { Value = "changed-value", Hash = "changed-hash" });

            // Act
            var removeResult = _inMemoryStore.Remove("existing-key", "existing-hash");

            // Assert
            removeResult.Should().Be(false);
            _inMemoryStore.InMemoryStore.Keys.Should().Contain(k => k == "existing-key");
        }

        [Fact]
        public void RemoveWithHash_KeyValueDoesNotExist_RemoveFails()
        {
            // Act
            var removeResult = _inMemoryStore.Remove("existing-key", "existing-hash");

            // Assert
            removeResult.Should().Be(false);
        }

        [Fact]
        void RemoveWithoutHash_KeyValueExist_RemoveSuccessful()
        {
            // Arrange
            _inMemoryStore.InMemoryStore.TryAdd("existing-key", new HashedValue<object> { Value = "existing-value", Hash = "existing-hash" });

            // Act
            _inMemoryStore.Remove("existing-key");

            // Assert
            _inMemoryStore.InMemoryStore.Keys.Should().NotContain(k => k == "existing-key");
        }

        [Fact]
        void RemoveWithoutHash_KeyValueDoesNotExist_RemoveSilentFails()
        {
            // Act
            _inMemoryStore.Remove("existing-key");
        }
    }
}
