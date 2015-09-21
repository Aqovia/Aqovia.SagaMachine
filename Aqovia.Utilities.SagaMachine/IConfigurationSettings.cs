using StackExchange.Redis.Extensions.Core.Configuration;

namespace Aqovia.Utilities.SagaMachine
{
    public interface IConfigurationSettings
    {
        string EnvironmentType { get; }
        string ServiceBus { get; }
        uint SmoketestTimeoutSeconds { get; }
        RedisCachingSectionHandler PersistentRedisHost { get; }
    }
}
