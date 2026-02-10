using System.Net;
using System.Collections.Generic;

namespace OpenEnv.Configuration
{
    public sealed class SqlCredentials
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class NetworkCredential
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
    }

    public sealed class EnvironmentConfig
    {
        public string ServerIP { get; set; } = string.Empty;
        public string BackupLocation { get; set; } = string.Empty;
        public SqlCredentials SqlCredentials { get; set; }
        public NetworkCredential NetworkCredentials { get; set; }
        public System.Net.NetworkCredential ToSysNetNetworkCredential()
        => new System.Net.NetworkCredential(NetworkCredentials.Username, NetworkCredentials.Password, NetworkCredentials.Domain);
    }

    public sealed class AppConfig
    {
        public Dictionary<string, EnvironmentConfig> Environments { get; set; }
    }
}
