using System;
using System.Threading.Tasks;

namespace Aqovia.Utilities.SagaMachine.StatePersistance
{
    public interface IKeyValueStore
    {
        HashedValue<T> GetValue<T>(string key);
        bool TrySetValue<T>(string key, T value, string oldHash);
        bool Remove(string key, string oldHash);
        void Remove(string key);
        Task<bool> TakeLockWithDefaultExpiryTime(string key, Guid lockToken);
        Task<bool> TakeLock(string key, Guid lockToken, double milliseconds);
        Task<bool> ReleaseLock(string key, Guid lockToken);
        TimeSpan Ping();
    }
}
