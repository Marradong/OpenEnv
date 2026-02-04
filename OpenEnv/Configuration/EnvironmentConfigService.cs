using Microsoft.Extensions.Options;

namespace OpenEnv.Configuration
{
    public class EnvironmentConfigService
    {
        private const string _productionKey = "Production";
        private const string _testingKey = "Testing";
        private const string _developmentKey = "Development";

        private readonly AppConfig _config;
        public EnvironmentConfigService(IOptions<AppConfig> config)
        {
            _config = config.Value;
        }

        public EnvironmentConfig GetEnvironmentConfig(string key)
        {
            if (!_config.Environments.ContainsKey(key))
            {
                throw new KeyNotFoundException($"{key} environment configuration not found.");
            }

            var env = _config.Environments[key];

            return new EnvironmentConfig
            {
                ServerIP = env.ServerIP,
                SqlCredentials = env.SqlCredentials,
                NetworkCredentials = env.NetworkCredentials
            };
        }

        /*
         Register in program.cs: builder.Services.Configure<AppConfig>(builder.Configuration.GetSection("AppConfig"));
         */
    }

    public sealed class SqlCredentials
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public sealed class NetworkCredentials
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Domain { get; set; }
    }

    public sealed class EnvironmentConfig
    {
        public string ServerIP { get; set; }
        public SqlCredentials SqlCredentials { get; set; }
        public NetworkCredentials NetworkCredentials { get; set; }
    }

    public sealed class AppConfig
    {
        public Dictionary<string, EnvironmentConfig> Environments { get; set; }
    }
}
