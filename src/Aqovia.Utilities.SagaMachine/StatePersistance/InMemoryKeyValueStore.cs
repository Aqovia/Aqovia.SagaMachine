using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Aqovia.Utilities.SagaMachine.StatePersistance
{
    public class InMemoryKeyValueStore : IKeyValueStore
    {
        private static readonly object UniqueLockingObj = new object();

        internal class LockElement
        {
            internal Guid LockToken { get; set; }
            internal DateTime Expiry { get; set; }
        }

        public readonly ConcurrentDictionary<string, HashedValue<object>> InMemoryStore = new ConcurrentDictionary<string, HashedValue<object>>();
        private readonly ConcurrentDictionary<string, LockElement> _lockDictionary = new ConcurrentDictionary<string, LockElement>();

        public static string GetMmd5Hash(object valueElement)
        {
            var serializedValue = JsonConvert.SerializeObject(valueElement).GetHashCode().ToString();
            using (MD5 md5Hash = MD5.Create())
            {
                byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(serializedValue));

                StringBuilder sBuilder = new StringBuilder();
                foreach (var b in data)
                {
                    sBuilder.Append(b.ToString("x2")); // x2 -> print as two upercase hexadecimal characters
                }

                return sBuilder.ToString();
            }
        }

        public HashedValue<T> GetValue<T>(string key)
        {
            InMemoryStore.TryGetValue(key, out var valueElement);

            if (valueElement != null && !(valueElement.Value is T))
            {
                throw new Exception("Type mismatch between T and retrieved object type");
            }

            return valueElement == null ? null : new HashedValue<T>{Value = (T)valueElement.Value, Hash = valueElement.Hash};
        }

        public bool TrySetValue<T>(string key, T value, string oldHash)
        {
            if (string.IsNullOrEmpty(oldHash))
            {
                return InMemoryStore.TryAdd(key, new HashedValue<object> { Value = value, Hash = GetMmd5Hash(value) });
            }

            lock (UniqueLockingObj)
            {
                if (InMemoryStore.TryGetValue(key, out var existingValue))
                {
                    if (existingValue.Hash.Equals(oldHash, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return InMemoryStore.TryUpdate(key,
                            new HashedValue<object> {Value = value, Hash = GetMmd5Hash(value)}, existingValue);
                    }
                }
            }

            return false;
        }

        public bool Remove(string key, string oldHash)
        {
            lock (UniqueLockingObj)
            {
                if (InMemoryStore.TryGetValue(key, out var existingValue))
                {
                    if (existingValue.Hash.Equals(oldHash, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return InMemoryStore.TryRemove(key, out existingValue);
                    }
                }
            }

            return false;
        }

        public void Remove(string key)
        {
            HashedValue<object> existingValue;
            InMemoryStore.TryRemove(key, out existingValue);
        }

        public TimeSpan Ping()
        {
            return DateTime.UtcNow.TimeOfDay;
        }

        public bool StoreEmpty()
        {
            return !InMemoryStore.Any();
        }

        public async Task<bool> TakeLockWithDefaultExpiryTime(string key, Guid lockToken)
        {
            return await TakeLock(key, lockToken, 500);
        }

        public async Task<bool> TakeLock(string key, Guid lockToken, double milliseconds)
        {
            if (!_lockDictionary.TryGetValue(key, out var lockElement))
            {
                lockElement = new LockElement
                {
                    LockToken = lockToken,
                    Expiry = DateTime.UtcNow.AddMilliseconds(milliseconds)
                };

                var result = _lockDictionary.TryAdd(key, lockElement);

                return result;
            }
            else
            {
                if (lockElement.Expiry >= DateTime.UtcNow)
                {
                    var newLockElement = new LockElement
                    {
                        LockToken = Guid.NewGuid(),
                        Expiry = DateTime.UtcNow.AddMilliseconds(milliseconds)
                    };

                    var result = _lockDictionary.TryUpdate(key, newLockElement, lockElement);

                    return result;
                }
            }

            return false;
        }

        public async Task<bool> ReleaseLock(string key, Guid lockToken)
        {
            LockElement token;
            return _lockDictionary.TryRemove(key, out token);
        }
    }
}
