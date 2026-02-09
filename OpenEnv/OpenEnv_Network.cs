using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using OpenEnv.Configuration;

namespace OpenEnv
{
    public static partial class OpenEnv
    {
        public static IPAddress GetApiIP()
        {
            string key;

            switch (HostingMode)
            {
                case Deployment.Production:
                    key = ProductionKey;
                    break;
                case Deployment.Development_Ui_Test_Api:
                case Deployment.Testing:
                    key = TestingKey;
                    break;
                case Deployment.Development:
                default:
                    key = DevelopmentKey;
                    break;
            }

            EnvironmentConfig cg = null;

            try
            {
                cg = GetEnvironmentConfig(key);
                IPAddress ipAddress = IPAddress.Parse(cg.ServerIP);
                return ipAddress;
            }
            catch (FormatException ex)
            {
                if (cg == null)
                {
                    throw new KeyNotFoundException($"{key} environment configuration not found.");
                }
                else
                {
                    throw new InvalidOperationException($"Invalid IP address format in configuration for key '{key}': '{cg.ServerIP}'. Exception: {ex.Message}");
                }
            }
        }

        public static IPAddress GetUiIP()
        {
            string key;

            switch (HostingMode)
            {
                case Deployment.Production:
                    key = ProductionKey;
                    break;
                case Deployment.Testing:
                    key = TestingKey;
                    break;
                case Deployment.Development_Ui_Test_Api:
                case Deployment.Development:
                default:
                    key = DevelopmentKey;
                    break;
            }

            EnvironmentConfig cg = null;

            try
            {
                cg = GetEnvironmentConfig(key);
                IPAddress ipAddress = IPAddress.Parse(cg.ServerIP);
                return ipAddress;
            }
            catch (FormatException ex)
            {
                if (cg == null)
                {
                    throw new KeyNotFoundException($"{key} environment configuration not found.");
                }
                else
                {
                    throw new InvalidOperationException($"Invalid IP address format in configuration for key '{key}': '{cg.ServerIP}'. Exception: {ex.Message}");
                }
            }
        }

        public static bool IsPortAvailable(int port, bool api=true)
        {
            TcpListener tcpListener = null;
            var ip = api ? GetApiIP() : GetUiIP();
            try
            {
                tcpListener = new TcpListener(ip, port);
                tcpListener.Start();
                _log?.Invoke($"\nlocal {ip} : port {port} is available.");
                return true;
            }
            catch (SocketException ex)
            {
                _log?.Invoke($"\nlocal {ip} : port {port} is not available. Exception: {ex.Message}");
                return false;
            }
            finally
            {
                tcpListener?.Stop();
            }
        }
    }
}
