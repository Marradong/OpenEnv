using System;
using System.Net;
using System.Net.Sockets;

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

            return IPAddress.Parse(GetEnvironmentConfig(key).ServerIP);
        }

        public static IPAddress GetUiIP()
        {
            string key;

            switch (HostingMode)
            {
                case Deployment.Production:
                    key = _productionKey;
                    break;
                case Deployment.Testing:
                    key = _testingKey;
                    break;
                case Deployment.Development_Ui_Test_Api:
                case Deployment.Development:
                default:
                    key = _developmentKey;
                    break;
            }

            return IPAddress.Parse(GetEnvironmentConfig(key).ServerIP);
        }

        public static bool IsPortAvailable(int port, bool api=true)
        {
            TcpListener tcpListener = null;
            var ip = api ? GetApiIP() : GetUiIP();
            try
            {
                tcpListener = new TcpListener(ip, port);
                tcpListener.Start();
                Console.WriteLine($"local {ip} : port {port} is available.");
                return true;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"local {ip} : port {port} is not available. Exception: {ex.Message}");
                return false;
            }
            finally
            {
                tcpListener?.Stop();
            }
        }
    }
}
