using System.Configuration;
using StackExchange.Redis.Extensions.Core.Configuration;

namespace Aqovia.Utilities.SagaMachine 
{
    public class ConfigurationSettings : IConfigurationSettings
    {
        public string EnvironmentType
        {
            get { return ConfigurationManager.AppSettings["EnvironmentType"]; }
        }

        public string ServiceBus
        {
            get { return ConfigurationManager.AppSettings["ServiceBus"]; }
        }

        public uint SmoketestTimeoutSeconds
        {
            get { return uint.Parse(ConfigurationManager.AppSettings["SmoketestTimeoutSeconds"]); }
        }

        public RedisCachingSectionHandler PersistentRedisHost
        {
            get
            {
                return StackExchange.Redis.Extensions.Core.Configuration.RedisCachingSectionHandler.GetConfig();
            }
        }
    }
}
