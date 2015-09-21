using System;

namespace Aqovia.Utilities.SagaMachine.StatePersistance
{
    public interface IKeyValueStore
    {
        HashedValue<T> GetValue<T>(string key);
        bool TrySetValue<T>(string key, T value, string oldHash);
        bool Remove(string key, string oldHash);
        void Remove(string key);
        TimeSpan Ping();
    }
}
