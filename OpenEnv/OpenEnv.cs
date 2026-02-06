using System;
using System.Collections.Generic;

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Threading;
using OpenEnv;
using System.Reflection;
using System.Runtime.InteropServices;

#if NET48
using System.Configuration;
    using System.Data.SqlClient;
//using System.Deployment.Application;
#endif

#if NET8_0_OR_GREATER                   
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Data.SqlClient;

#endif

namespace OpenEnv
{
    public enum Deployment
    {
        Development_Ui_Test_Api = 4,  // UI in Dev environment and Api in Test environment - Pre Testing Release (Release Publish to Testing)
        Development = 3,  // UI and API in Dev environment - Pre Testing Release (Release Publish to Testing)
        Testing = 2,       // Pre Release : Live Ui clone and Testing Db (Release Publish to PRODUCTION)
        Production = 1     // Live Ui and Live Db
    }

    public static partial class OpenEnv
    {
        private static Action<string> _log = null;

        private static Deployment _hostingMode;
        public static Deployment HostingMode
        {
            get
            {
                if (_hostingMode == 0)
                {
                    throw new InvalidOperationException($"HostingMode not Initialised");
                }
                return _hostingMode;
            }
            internal set
            {
                _hostingMode = value;

#if NET48
                UpdateDatabaseConnectionNET48FromDeployment();
#elif NET8_0_OR_GREATER
                UpdateDatabaseConnectionNETCoreFromDeployment();
#endif
            }
        }

        private static void InitialiseEnvironment(bool Dev_Ui_Test_Api, Action<string> log)
        {
            CultureInfo australiaCulture = new CultureInfo("en-AU");
            Thread.CurrentThread.CurrentCulture = australiaCulture;
            Thread.CurrentThread.CurrentUICulture = australiaCulture;

            _log = log;

            if (IsClickOnceDeployed())
            {
                HostingMode = Deployment.Production;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (IsProduction())
                {
                    HostingMode = Deployment.Production;
                }
                else
                {
                    if (Dev_Ui_Test_Api)
                    {
                        HostingMode = Deployment.Development_Ui_Test_Api;
                    }
                    else
                    {
                        HostingMode = Debugger.IsAttached ? Deployment.Development : Deployment.Testing;
                    }
                }
            }
            else
            {
                // MAUI app
                if (Dev_Ui_Test_Api)
                {
                    HostingMode = Deployment.Development_Ui_Test_Api;
                }
                else
                {
                    HostingMode = Debugger.IsAttached ? Deployment.Development : Deployment.Production;
                }
            }

            // Retrieve the enum name (not the numeric value)
            string HostingModeString = Enum.GetName(typeof(Deployment), HostingMode) ?? string.Empty;

            _log?.Invoke($"\n");
            _log?.Invoke($"\n{new string('#', 10)} HOSTING ENVIRONMENT : {HostingModeString} {new string('#', 10)}");
            _log?.Invoke($"\n\tPLATFORM : {Environment.OSVersion.Platform}");
            _log?.Invoke($"\n\tWebApi IP [HostingMode : {HostingMode.ToString()}] : {GetApiIP()}");
            _log?.Invoke($"\n\tUi IP [HostingMode : {HostingMode.ToString()}] : {GetUiIP()}");
            _log?.Invoke($"\n\t{GetEnvironmentInfo()}");
            _log?.Invoke($"\n\t{GetAppName()} : Launched {DateTime.Now:ddd dd MMM HH:mm:ss}");
            _log?.Invoke($"\n{new string('#', 60)}\n");
        }

        /// <summary>
        /// Initialises the hosting environment using the configuration from the specified JSON file path.
        /// </summary>
        /// <param name="jsonPath">Path to .json Config file</param>
        /// <param name="Dev_Ui_Test_Api">Indicates whether to use the Test Web API for UI development</param>
        public static void Initialise(string jsonPath, bool Dev_Ui_Test_Api = false, Action<string> log = null)
        {
            InitialiseConfig(jsonPath);
            InitialiseEnvironment(Dev_Ui_Test_Api, log);
        }

        /// <summary>
        /// Initialises the hosting environment.
        /// </summary>
        /// <param name="json">json string containing the environment variables configuration</param>
        /// <param name="Dev_Ui_Test_Api">Indicates whether to use the Test Web API for UI development</param>
        public static void InitialiseFromJson(string json, bool Dev_Ui_Test_Api = false, Action<string> log = null)
        {
            InitialiseConfigFromJson(json);
            InitialiseEnvironment(Dev_Ui_Test_Api, log);
        }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Initializes the hosting environment for .NET 8.0 or greater.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance.</param>
    /// <param name="jsonPath">Path to .json Config file</param>
    /// <param name="uiDevUseTestWebApi">Indicates whether to use the Test Web API for UI development</param>
    public static void Initialise(WebApplicationBuilder builder, string jsonPath, bool uiDevUseTestWebApi = false, Action<string> log = null)
    {
        string kestrelUrl = builder.Configuration["Url"] ?? string.Empty;

        try
        {
            Runtime_Config.Init_Url(kestrelUrl);
        }
        catch (ArgumentException ex)
        {
            _log?.Invoke($"\nError Initializing URL: {ex.Message}");
        }

        Runtime_Config.Load_Connections(builder.Configuration.GetSection("ConnectionStrings"));


            InitialiseConfig(jsonPath);
            InitialiseEnvironment(uiDevUseTestWebApi, log);
        }

        /// <summary>
        /// Initializes the hosting environment for .NET 8.0 or greater.
        /// </summary>
        /// <param name="builder">The WebApplicationBuilder instance.</param>
        /// <param name="json">json string containing the environment variables configuration</param>
        /// <param name="uiDevUseTestWebApi">Indicates whether to use the Test Web API for UI development</param>
        public static void InitialiseFromJson(WebApplicationBuilder builder, string json, bool uiDevUseTestWebApi = false, Action<string> log = null)
        {
            string kestrelUrl = builder.Configuration["Url"] ?? string.Empty;

            try
            {
                Runtime_Config.Init_Url(kestrelUrl);
            }
            catch (ArgumentException ex)
            {
                _log?.Invoke($"\nError Initializing URL: {ex.Message}");
            }

            Runtime_Config.Load_Connections(builder.Configuration.GetSection("ConnectionStrings"));


            InitialiseConfigFromJson(json);
            InitialiseEnvironment(uiDevUseTestWebApi, log);
        }
#endif
    }
}