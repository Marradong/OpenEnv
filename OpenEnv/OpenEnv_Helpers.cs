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
            try
            {
                return File.Exists(iniFile);
            }
            catch (UnauthorizedAccessException ex)
            {
                _log?.Invoke($"\nAccess denied checking production marker: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _log?.Invoke($"\nError checking production status: {ex.Message}");
                return false;
            }
        }

        private static string GetEnvironmentInfo() =>
            string.Join(Environment.NewLine, new[]
            {
            $"Environment Info:",
            $"\tMachine Name : {Environment.MachineName}",
            $"\tDomain       : {Environment.UserDomainName}",
            $"\tUser         : {Environment.UserName}",
            $"\tOS x64       : {Environment.Is64BitOperatingSystem}",
            $"\tProcess x64  : {Environment.Is64BitProcess}",
            $"\t.NET         : {Environment.Version}",
            $"\tCulture      : {CultureInfo.CurrentCulture.DisplayName} ({CultureInfo.CurrentCulture.Name})",
            $"\tUtc Offset   : {TimeZoneInfo.Local.GetUtcOffset(DateTime.Now)}",
            $"\tTime Zone    : {TimeZoneInfo.Local.StandardName}",
            $"\tAssembly     : {typeof(OpenEnv).Assembly.Location}",
            $"\tCurrent Dir  : {Environment.CurrentDirectory}",
#if NET8_0_OR_GREATER
            $"\tUtc Now      : {DateTime.UtcNow}",
            $"\tUptime       : {TimeSpan.FromMilliseconds(Environment.TickCount64)}",
            $"\tCPU Count    : {Environment.ProcessorCount}",
            $"\tOS Version   : {Environment.OSVersion} ({RuntimeInformation.OSDescription.Trim()})",
            $"\tProcess      : [PID={Environment.ProcessId}] {Process.GetCurrentProcess()?.MainModule?.FileName}",
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
            _log?.Invoke($"\nEnum Name: {hostingModeString}");

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
        EFDbHeading = "MsSql : " + string.Join(" ", Runtime_Config.ConnectionStrings?.Keys?.Select(n => GetCurrentDbCatalog(n)));
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


            _log?.Invoke($"\nCaption generated: {caption}");

            return (caption, true);
        }
    }
}
