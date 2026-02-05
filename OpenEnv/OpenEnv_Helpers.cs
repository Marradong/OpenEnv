using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

#if NET48
using System.Data.SqlClient;
using System.Configuration;
#endif

#if NET8_0_OR_GREATER                   
    using System.Runtime.InteropServices;
#endif

namespace OpenEnv
{
    public static partial class OpenEnv
    {
        /// <summary>
        /// Determines if the application is network deployed.
        /// </summary>
        /// <returns><c>true</c> if the application is network deployed; otherwise, <c>false</c>.</returns>
        public static bool IsClickOnceDeployed()
        {
            // ClickOnce Deploy Api doesn't exist in .NET Core
            // Use path to detect as it works in all versions of .NET
            var filename = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (filename.Contains("AppData\\Local\\Apps\\2.0\\"))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Determines if the application is deployed on the production server.
        /// </summary>
        /// <returns><c>true</c> if the application is on the production server; otherwise, <c>false</c>.</returns>
        public static bool IsProduction()
        {
            string iniFile = @"C:\PRODUCTION.ini";
            if (File.Exists(iniFile))
            {
                return true;
            }
            return false;
        }

        private static string GetEnvironmentInfo() =>
            string.Join(Environment.NewLine, new[]
            {
            $"\tEnvironment Info:",
            $"\t\tMachine Name : {Environment.MachineName}",
            $"\t\tDomain       : {Environment.UserDomainName}",
            $"\t\tUser         : {Environment.UserName}",
            $"\t\tOS x64       : {Environment.Is64BitOperatingSystem}",
            $"\t\tProcess x64  : {Environment.Is64BitProcess}",
            $"\t\t.NET         : {Environment.Version}",
            $"\t\tCulture      : {CultureInfo.CurrentCulture.DisplayName} ({CultureInfo.CurrentCulture.Name})",
            $"\t\tUtc Offset   : {TimeZoneInfo.Local.GetUtcOffset(DateTime.Now)}",
            $"\t\tTime Zone    : {TimeZoneInfo.Local.StandardName}",
            $"\t\tAssembly     : {typeof(OpenEnv).Assembly.Location}",
            $"\t\tCurrent Dir  : {Environment.CurrentDirectory}",
#if NET8_0_OR_GREATER
            $"\t\tUtc Now      : {DateTime.UtcNow}",
            $"\t\tUptime       : {TimeSpan.FromMilliseconds(Environment.TickCount64)}",
            $"\t\tCPU Count    : {Environment.ProcessorCount}",
            $"\t\tOS Version   : {Environment.OSVersion} ({RuntimeInformation.OSDescription.Trim()})",
            $"\t\tProcess      : [PID={Environment.ProcessId}] {Process.GetCurrentProcess()?.MainModule?.FileName}",
#endif
            });

        public static string GetAppName()
        {
            var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;

            if (!string.IsNullOrEmpty(entryAssemblyName))
            {
                return entryAssemblyName;
            }

            return Process.GetCurrentProcess().ProcessName;
        }

        public static (string caption, bool visible) GetBanner()
        {
            // Retrieve the enum name (not the numeric value)
            string hostingModeString = Enum.GetName(typeof(Deployment), HostingMode) ?? string.Empty;
            Debug.WriteLine($"Enum Name: {hostingModeString}");

            // Determine valid connection strings
            string EFDbHeading = "MsSql : ";
            List<string> dbNames = new List<string>();

#if NET48
            // Updates ONLY those connection strings whose name contains "Connection"
            foreach (ConnectionStringSettings con in ConfigurationManager.ConnectionStrings)
            {
                if (con.Name != null && con.Name.Contains("Connection"))
                {
                    var builder = new SqlConnectionStringBuilder(con.ConnectionString);
                    dbNames.Add(builder.InitialCatalog);
                }
            }

            EFDbHeading = string.Join(" & ", dbNames);
#endif

#if NET8_0_OR_GREATER
        EFDbHeading = "MsSql : " + string.Join(" ", Runtime_Config.ConnectionStrings.Keys.Select(n => GetCurrentDbCatalog(n)));
#endif

            if (IsClickOnceDeployed())
            {
                return ($"{hostingModeString} | {EFDbHeading}", false); // Hide banner for installed apps
            }

            if (IsProduction())
            {
                return ($"{hostingModeString} | Live", false); // Hide banner in production
            }

            string caption = HostingMode == Deployment.Development_Ui_Test_Api
                ? $"['DEV Ui' & 'TEST Api'] | '{hostingModeString}' | {EFDbHeading}"
                : $"{(Debugger.IsAttached ? "[DEV]" : "[TEST]")} | '{hostingModeString}' | {EFDbHeading}";

            switch (HostingMode)
            {
                case Deployment.Testing:
                    caption = $"[TEST] | '{hostingModeString}' | {EFDbHeading}";
                    break;
                case Deployment.Development_Ui_Test_Api:
                    caption = $"['DEV Ui' & 'TEST Api'] | '{hostingModeString}' | {EFDbHeading}";
                    break;
                case Deployment.Development:
                default:
                    caption = $"[DEV] | '{hostingModeString}' | {EFDbHeading}";
                    break;
            }


            Debug.WriteLine($"Caption generated: {caption}");

            return (caption, true);
        }
    }
}
