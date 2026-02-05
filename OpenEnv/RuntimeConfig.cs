#if NET8_0_OR_GREATER 
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
#endif


namespace OpenEnv
{
    /// <summary>
    /// Made for making .NET 8.0 easier to work with since appsettings.json is read-only at runtime
    /// </summary>
#if NET8_0_OR_GREATER

    public static class Runtime_Config
    {
        public static Dictionary<string, string> ConnectionStrings { get; private set; } = new();
        public static Uri Url { get; set; }

        public static void Load_Connections(IConfigurationSection configSection)
        {
            if (!configSection.Exists()) return;
            foreach (var c in configSection.GetChildren())
            {
                string con_name = c.Key;
                string base_name = con_name.Replace("Connection", String.Empty);
                base_name = base_name.Split('_')[0];
                
                ConnectionStrings[base_name] = c.Value ?? string.Empty;
            }
        }

        /// <summary>
        /// Updates the URL with the current API IP address.
        /// </summary>
        public static void UpdateUrlWithApiIP()
        {
            // Runtime Config , updates server and gets port from file, kind of like connection string
            
            if (Url == null)
            {
                Init_Url("http://localhost"); // Provide a default URL if none is set
            }

            var ip = OpenEnv.GetApiIP();
            var builder = new UriBuilder(Url)
            {
                Host = ip.ToString(), // Update only the host
                Port = Url.IsDefaultPort ? -1 : Url.Port // Retain the original port if specified
            };
            Init_Url(builder.Uri.ToString()); // Use InitializeUrl to set the updated URL
        }

        /// <summary>
        /// Initialises the URL with the specified string.
        /// </summary>
        /// <param name="url">The URL string to initialize.</param>
        /// <exception cref="ArgumentException">Thrown when the URL is invalid.</exception>
        public static void Init_Url(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                url = $"http://{GetLocalIPAddress()}";  // Get the machine's LAN IP address
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl))
            {
                throw new ArgumentException($"Invalid URL: {url}");
            }

            Url = parsedUrl;
        }
        /// <summary>
        /// By default, "localhost" does not route to the LAN (Local Area Network). 
        /// It is only accessible from the local machine and not from other devices on the network. 
        /// If you want to make a service accessible on the LAN, you need to bind it to the machine's LAN IP address instead of "localhost".
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }
#endif
}
