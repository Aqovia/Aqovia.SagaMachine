using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Configuration;

namespace Aqovia.Utilities.SagaMachine.StatePersistance
{
    public class RedisKeyValueStore : IKeyValueStore
    {

        private readonly IConfigurationSettings _configuration;
        private readonly ConnectionMultiplexer _redis;

        public RedisKeyValueStore(IConfigurationSettings configuration)
        {
            _configuration = configuration;
            var configurationOptions = new ConfigurationOptions
            {
                ConnectTimeout = _configuration.PersistentRedisHost.ConnectTimeout,
                Ssl = _configuration.PersistentRedisHost.Ssl,

            };

            foreach (RedisHost redisHost in _configuration.PersistentRedisHost.RedisHosts)
            {
                configurationOptions.EndPoints.Add(redisHost.Host, redisHost.CachePort);
            }
            _redis = ConnectionMultiplexer.Connect(configurationOptions);
            
        }

        private IDatabase GetDatabase()
        {            
            IDatabase db = _redis.GetDatabase(_configuration.PersistentRedisHost.Database);            
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
                Value =  ((string) result.Result)!=null? JsonConvert.DeserializeObject<T>(result.Result):default(T),
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
        
    }
}