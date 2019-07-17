using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Aqovia.Utilities.SagaMachine.StatePersistance
{
    public class RedisKeyValueStore : IKeyValueStore, IDisposable
    {
        private const double DefaultLockExpiryTime = 30000;
        private readonly ConnectionMultiplexer _redis;

        public RedisKeyValueStore(string redisConnectionString)
        {
            _redis = ConnectionMultiplexer.Connect(redisConnectionString);
        }

        public RedisKeyValueStore(ConfigurationOptions redisConfiguration)
        {
           _redis = ConnectionMultiplexer.Connect(redisConfiguration);
        }

        private IDatabase GetDatabase()
        {
            IDatabase db = _redis.GetDatabase();
            return db;
        }

        public HashedValue<T> GetValue<T>(string key)
        {
            IDatabase db = GetDatabase();
            ITransaction trans;
            Task<RedisValue> result;
            string currentHash;
            do
            {
                currentHash = db.HashGet(key, "hash");
                trans = db.CreateTransaction();
                trans.AddCondition(Condition.HashEqual(key, "hash", currentHash));
                result = trans.HashGetAsync(key, "value");

            } while (!trans.Execute());

            return new HashedValue<T>
            {
                Value = ((string)result.Result) != null ? JsonConvert.DeserializeObject<T>(result.Result) : default(T),
                Hash = currentHash
            };
        }

        public bool TrySetValue<T>(string key, T value, string oldHash)
        {
            IDatabase db = GetDatabase();

            if (string.IsNullOrEmpty(oldHash))
            {//We are creating a new entry
                Guid hashGuid = Guid.NewGuid();

                var trans = db.CreateTransaction();
                trans.AddCondition(Condition.KeyNotExists(key));
                trans.HashSetAsync(key, "value", JsonConvert.SerializeObject(value));
                trans.HashSetAsync(key, "hash", hashGuid.ToString());
                return trans.Execute();
            }
            else
            {//try and update instead
                Guid newHashGuid = Guid.NewGuid();

                var trans = db.CreateTransaction();
                trans.AddCondition(Condition.HashEqual(key, "hash", oldHash));
                trans.HashSetAsync(key, "value", JsonConvert.SerializeObject(value));
                trans.HashSetAsync(key, "hash", newHashGuid.ToString());
                return trans.Execute();
            }

        }

        public bool Remove(string key, string oldHash)
        {
            IDatabase db = GetDatabase();
            var trans = db.CreateTransaction();
            trans.AddCondition(Condition.HashEqual(key, "hash", oldHash));
            trans.KeyDeleteAsync(key);
            return trans.Execute();
        }

        public TimeSpan Ping()
        {
            IDatabase db = GetDatabase();
            return db.Ping();
        }

        public void Remove(string key)
        {
            IDatabase db = GetDatabase();
            db.KeyDelete(key);
        }

        /// <summary>
        /// Request lock against key value element
        /// Lock lifespan 500 milliseconds
        /// </summary>
        /// <param name="key"></param>
        /// <param name="lockToken"></param>
        /// <returns></returns>
        public async Task<bool> TakeLockWithDefaultExpiryTime(string key, Guid lockToken)
        {
            return await TakeLock(key, lockToken, DefaultLockExpiryTime);
        }

        /// <summary>
        /// Request lock against key value element
        /// Lock has lifespan specified in milliseconds
        /// </summary>
        /// <param name="key"></param>
        /// <param name="lockToken"></param>
        /// <param name="milliseconds"></param>
        /// <returns></returns>
        public async Task<bool> TakeLock(string key, Guid lockToken, double milliseconds)
        {
            var maxNumberOfAttempts = 3;
            var currentRetry = 0;
            var db = GetDatabase();

            while(true)
            {
                currentRetry++;

                try
                {
                    var redisLockKey = string.Format("lock_{0}", key);
                    
                    var trans = db.CreateTransaction();
                    trans.AddCondition(Condition.KeyNotExists(redisLockKey));
                    var acquireLockTask = trans.LockTakeAsync(redisLockKey, lockToken.ToString(), TimeSpan.MaxValue).ConfigureAwait(false);
                    var setExpiryTimeTask = trans.KeyExpireAsync(redisLockKey, TimeSpan.FromMilliseconds(milliseconds)).ConfigureAwait(false); /* This is added to workaround bug discovered in the Redis client. Remove this line if bug resolved. Detail can be found here: https://github.com/StackExchange/StackExchange.Redis/issues/415 */
                    trans.Execute();

                    var hasLock = await acquireLockTask;
                    var hasSucceedToSetExpiryTime = await setExpiryTimeTask;

                    if (!hasSucceedToSetExpiryTime)
                    {
                        // Note: this should never happen, however to be safe we handle this very improbable case
                        throw new Exception(string.Format("Unable to set expiry time for Redis key \"{0}\". Key needs to be removed manually from Redis.", redisLockKey));
                    }

                    if (currentRetry > maxNumberOfAttempts)
                    {
                        return hasLock;
                    }

                    if (hasLock)
                    {
                        return true;
                    }
                }
                catch (TaskCanceledException)
                {
                    // LockTakeAsync throws a TaskCanceledException when it fails to acquire the lock
                    // If failed then retry based on the logic in the error detection strategy
                    // Determine whether to retry the operation, as well as how 
                    // long to wait, based on the retry strategy.
                    if (currentRetry > maxNumberOfAttempts)
                    {
                        return false;
                    }
                }

                // Wait to retry the operation.
                // Consider calculating an exponential delay here and 
                // using a strategy best suited for the operation and fault.
                await Task.Delay(((int)Math.Pow(3, currentRetry)) * 500).ConfigureAwait(false);
            }
        }

        public async Task<bool> ReleaseLock(string key, Guid lockToken)
        {
            var db = GetDatabase();

            var redisLockKey = string.Format("lock_{0}", key);
            return await db.LockReleaseAsync(redisLockKey, lockToken.ToString()).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _redis.Close();
        }
    }
}