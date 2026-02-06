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

    using System.Net;
    using System.Net.Sockets;

    public static class Runtime_Config
    {
        private static readonly object _lock = new object();

        public static Dictionary<string, string> ConnectionStrings { get; private set; } = new();

        private static Uri _url;
        public static Uri Url
        {
            get
            {
                lock (_lock)
                {
                    return _url;
                }
            }
            private set
            {
                lock (_lock)
                {
                    _url = value;
                }
            }
        }

        /// <summary>
        /// Loads connection strings from configuration section.
        /// </summary>
        /// <param name="configSection">The configuration section containing connection strings.</param>
        /// <exception cref="ArgumentNullException">Thrown when configSection is null.</exception>
        public static void Load_Connections(IConfigurationSection configSection)
        {
            if (configSection == null)
            {
                throw new ArgumentNullException(nameof(configSection));
            }

            if (!configSection.Exists())
            {
                return;
            }

            lock (_lock)
            {
                foreach (var c in configSection.GetChildren())
                {
                    if (string.IsNullOrWhiteSpace(c.Key))
                    {
                        continue; // Skip invalid keys
                    }

                    string con_name = c.Key;
                    string base_name = con_name.Replace("Connection", string.Empty);

                    // More robust splitting with validation
                    var parts = base_name.Split('_', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        base_name = parts[0];
                    }

                    // Avoid overwriting existing keys without logging
                    if (!ConnectionStrings.ContainsKey(base_name))
                    {
                        ConnectionStrings[base_name] = c.Value ?? string.Empty;
                    }
                }
            }
        }

        /// <summary>
        /// Updates the URL with the current API IP address.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when API IP cannot be retrieved.</exception>
        public static void UpdateUrlWithApiIP()
        {
            try
            {
                // Ensure URL is initialized
                if (Url == null)
                {
                    Init_Url("http://localhost");
                }

                var ip = OpenEnv.GetApiIP();

                if (ip == null)
                {
                    throw new InvalidOperationException("Failed to retrieve API IP address.");
                }

                var builder = new UriBuilder(Url)
                {
                    Host = ip.ToString(),
                    Port = Url.IsDefaultPort ? -1 : Url.Port
                };

                Init_Url(builder.Uri.ToString());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to update URL with API IP.", ex);
            }
        }

        /// <summary>
        /// Initializes the URL with the specified string.
        /// </summary>
        /// <param name="url">The URL string to initialize. If null or empty, uses local IP address.</param>
        /// <exception cref="ArgumentException">Thrown when the URL is invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when local IP cannot be determined.</exception>
        public static void Init_Url(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    var localIp = GetLocalIPAddress();
                    url = $"http://{localIp}";
                }

                if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl))
                {
                    throw new ArgumentException($"Invalid URL format: {url}", nameof(url));
                }

                // Validate scheme
                if (parsedUrl.Scheme != Uri.UriSchemeHttp && parsedUrl.Scheme != Uri.UriSchemeHttps)
                {
                    throw new ArgumentException($"URL must use HTTP or HTTPS scheme. Provided: {parsedUrl.Scheme}", nameof(url));
                }

                Url = parsedUrl;
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                throw new InvalidOperationException("Failed to initialize URL.", ex);
            }
        }

        /// <summary>
        /// Gets the local machine's IPv4 address on the LAN.
        /// By default, "localhost" does not route to the LAN (Local Area Network). 
        /// It is only accessible from the local machine and not from other devices on the network. 
        /// If you want to make a service accessible on the LAN, you need to bind it to the machine's LAN IP address instead of "localhost".
        /// </summary>
        /// <returns>The local IPv4 address as a string.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no IPv4 address is found.</exception>
        private static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());

                // Prioritize non-loopback addresses
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }

                // Fallback to any IPv4 address if no non-loopback found
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }

                throw new InvalidOperationException("No network adapters with an IPv4 address found in the system.");
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException("Failed to retrieve local IP address due to network error.", ex);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException("An unexpected error occurred while retrieving local IP address.", ex);
            }
        }

        /// <summary>
        /// Clears all connection strings. Useful for testing or reinitialization.
        /// </summary>
        public static void ClearConnectionStrings()
        {
            lock (_lock)
            {
                ConnectionStrings.Clear();
            }
        }

        /// <summary>
        /// Gets a connection string by name.
        /// </summary>
        /// <param name="name">The name of the connection string.</param>
        /// <returns>The connection string value, or null if not found.</returns>
        public static string GetConnectionString(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            lock (_lock)
            {
                return ConnectionStrings.TryGetValue(name, out var value) ? value : null;
            }
        }
    }
#endif
}
