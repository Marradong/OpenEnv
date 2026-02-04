using Microsoft.Extensions.Configuration;

namespace OpenEnv.Configuration
{

    public class SqlCredentials
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class NetworkCredentials
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Domain { get; set; }
    }

    public class EnvironmentConfig
    {
        public string ServerIP { get; set; }
        public SqlCredentials SqlCredentials { get; set; }
        public NetworkCredentials NetworkCredentials { get; set; }
    }

    public class AppConfig
    {
        public Dictionary<string, EnvironmentConfig> Environments { get; set; }
    }
}
