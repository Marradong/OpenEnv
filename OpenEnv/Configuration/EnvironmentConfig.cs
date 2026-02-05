using System.Net;
using System.Collections.Generic;

namespace OpenEnv.Configuration
{
    public sealed class SqlCredentials
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class EnvironmentConfig
    {
        public string ServerIP { get; set; } = string.Empty;
        public string BackupLocation { get; set; } = string.Empty;
        public SqlCredentials SqlCredentials { get; set; }
        public NetworkCredential NetworkCredentials { get; set; }
    }

    public sealed class AppConfig
    {
        public Dictionary<string, EnvironmentConfig> Environments { get; set; }
    }
}
