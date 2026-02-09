using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenEnv.Configuration;

namespace OpenEnv
{
    public static partial class OpenEnv
    {
        private const string _productionKey = "Production";
        private const string _testingKey = "Testing";
        private const string _developmentKey = "Development";

        private static bool _initialised = false;
        private static readonly object _lock = new object();
        private static AppConfig _config;
        public static AppConfig Config
        {
            get
            {
                if (!_initialised)
                    throw new InvalidOperationException("InitialiseConfig() must be called before use.");
                return _config;
            }
        }

        /// <summary>
        /// Initialise from file path. Can be called once.
        /// </summary>
        public static void InitialiseConfig(string jsonFilePath)
        {
            if (_initialised) throw new InvalidOperationException("EnvironmentConfig is already initialised.");

            lock (_lock)
            {
                if (_initialised) throw new InvalidOperationException("EnvironmentConfig is already initialised.");
                try
                {
                    string json = File.ReadAllText(jsonFilePath);
                    LoadFromJson(json);
                }
                catch (FileNotFoundException ex)
                {
                    throw new InvalidOperationException($"Could not FIND .json Config file at {jsonFilePath}: {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new InvalidOperationException($"Could not ACCESS .json Config file at {jsonFilePath}: {ex.Message}");
                }
                catch (IOException ex)
                {
                    throw new InvalidOperationException($"IO Error with .json Config file at {jsonFilePath}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Initialize from a JSON string. Can be called once.
        /// </summary>
        public static void InitialiseConfigFromJson(string json)
        {
            if (_initialised) throw new InvalidOperationException("EnvironmentConfig is already initialized.");

            lock (_lock)
            {
                if (_initialised) throw new InvalidOperationException("EnvironmentConfig is already initialised.");
                LoadFromJson(json);
            }
        }

        private static void LoadFromJson(string json)
        {
            try
            {
                var config = JsonConvert.DeserializeObject<AppConfig>(json);
                if (config == null) throw new InvalidOperationException("EnvironmentConfig is invalid. Please ensure json is in a valid format.");
                _config = config;
                _initialised = true;
            }
            catch (JsonReaderException ex)
            {
                throw new InvalidOperationException($"Invalid JSON Config format: {ex.Message}");
            }
            catch (JsonSerializationException ex)
            {
                throw new InvalidOperationException($"JSON deserialization error: {ex.Message}");
            }
        }

        public static string GetEnvironmentKey(Deployment mode)
        {
            string envKey = string.Empty;

            switch (mode)
            {
                case Deployment.Production:
                    envKey = OpenEnv._productionKey;
                    break;
                case Deployment.Testing:
                    envKey = OpenEnv._testingKey;
                    break;
                case Deployment.Development:
                    envKey = OpenEnv._developmentKey;
                    break;
                default:
                    envKey = OpenEnv._developmentKey;
                    break;
            }

            return envKey;
        }

        public static EnvironmentConfig GetEnvironmentConfig(string key)
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

        public static EnvironmentConfig GetCurrentEnvironmentConfig()
        {
            string key;

            switch (HostingMode)
            {
                case Deployment.Production:
                    key = _productionKey;
                    break;
                case Deployment.Development_Ui_Test_Api:
                case Deployment.Testing:
                    key = _testingKey;
                    break;
                case Deployment.Development:
                default:
                    key = _developmentKey;
                    break;
            }

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

        public static DBConfig GetCurrentDbConfig(string dbName)
        {
            var env = GetCurrentEnvironmentConfig();

            var dbConfig = new DBConfig()
            {
                DataSource = env.ServerIP,
                DBName = dbName,
                Environment = env
            };

            return dbConfig;
        }

        public sealed class DBConfig
        {
            public string DBName { get; set; } = string.Empty;
            public string DataSource { get; set; } = string.Empty;
            public string BackupFolder { get; set; } = string.Empty;
            public string BackupPath { get; set; } = string.Empty;
            public EnvironmentConfig Environment { get; set; }
        }
    }
}
