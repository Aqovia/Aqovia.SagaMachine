using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

        private readonly ConcurrentDictionary<string, HashedValue<object>> _inMemoryStore = new ConcurrentDictionary<string, HashedValue<object>>();
        private readonly ConcurrentDictionary<string, LockElement> _lockDictionary = new ConcurrentDictionary<string, LockElement>();

        private static string GetMmd5Hash(object valueElement)
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
            HashedValue<object> valueElement;
            _inMemoryStore.TryGetValue(key, out valueElement);

            if (!(valueElement is HashedValue<T>))
            {
                throw new Exception("Type mismatch between T and retrieved object type");
            }

            return valueElement as HashedValue<T>;
        }

        public bool TrySetValue<T>(string key, T value, string oldHash)
        {
            if (string.IsNullOrEmpty(oldHash))
            {
                return _inMemoryStore.TryAdd(key, new HashedValue<object> { Value = value, Hash = GetMmd5Hash(value) });
            }

            lock (UniqueLockingObj)
            {
                HashedValue<object> existingValue;
                if (_inMemoryStore.TryGetValue(key, out existingValue))
                {
                    if (existingValue.Hash.Equals(oldHash, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return _inMemoryStore.TryUpdate(key,
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
                HashedValue<object> existingValue;
                if (_inMemoryStore.TryGetValue(key, out existingValue))
                {
                    if (existingValue.Hash.Equals(oldHash, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return _inMemoryStore.TryRemove(key, out existingValue);
                    }
                }
            }

            return false;
        }

        public void Remove(string key)
        {
            HashedValue<object> existingValue;
            _inMemoryStore.TryRemove(key, out existingValue);
        }

        public TimeSpan Ping()
        {
            return DateTime.UtcNow.TimeOfDay;
        }

        public bool StoreEmpty()
        {
            return !_inMemoryStore.Any();
        }


        public bool TakeLockWithDefaultExpiryTime(string key, out string lockToken)
        {
            return TakeLock(key, out lockToken, 500);
        }

        public bool TakeLock(string key, out string lockToken, double milliseconds)
        {
            LockElement lockElement;
            if (!_lockDictionary.TryGetValue(key, out lockElement))
            {
                lockElement = new LockElement
                {
                    LockToken = Guid.NewGuid(),
                    Expiry = DateTime.UtcNow.AddMilliseconds(milliseconds)
                };

                lockToken = lockElement.LockToken.ToString();
                _lockDictionary.TryAdd(key, lockElement);

                return true;
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

                    lockToken = lockElement.LockToken.ToString();
                    _lockDictionary.TryUpdate(key, newLockElement, lockElement);

                    return true;
                }
            }

            lockToken = null;
            return false;
        }

        public bool ReleaseLock(string key, string lockToken)
        {
            LockElement token;
            return _lockDictionary.TryRemove(key, out token);
        }
    }
}
