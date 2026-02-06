using System;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

#if NET48
using System.Configuration;
using System.Data.SqlClient;
#endif

#if NET8_0_OR_GREATER
using Microsoft.Data.SqlClient;
#endif

namespace OpenEnv
{
    public static partial class OpenEnv
    {
        /*
    
        •	Pooling=true: Enables connection pooling, which reuses existing connections to the database instead of creating new ones for every request. 
            This improves performance by reducing the overhead of opening and closing connections repeatedly.
        •	Max Pool Size=100: Limits the maximum number of connections that can be maintained in the pool. 
            If the pool reaches this limit, additional connection requests will wait until a connection becomes available.
        •	MultipleActiveResultSets=true provides flexibility for concurrent operations on a single connection but should be used judiciously to avoid performance or contention issues.
            MARS allows multiple queries or commands to be executed simultaneously on the same database connection. Without MARS, only one query or command can be active at a time per connection.
    
         */
        private static SqlConnectionStringBuilder BuildConnectionString(DBConfig dbConfig, string initialCatalog)
        {
            var connectionString = $@"Data Source={dbConfig.DataSource};Initial Catalog={initialCatalog};Persist Security Info=True;User ID={dbConfig.Environment.SqlCredentials.Username};Password={dbConfig.Environment.SqlCredentials.Password};MultipleActiveResultSets=true;TrustServerCertificate=true;";
            var connection = new SqlConnectionStringBuilder(connectionString)
            {
                DataSource = dbConfig.DataSource,
                InitialCatalog = dbConfig.DBName
            };

            return connection;
        }

        public static SqlConnectionStringBuilder GetConnectionStringFromDbName(string dbName)
        {
            var config = GetCurrentDbConfig(dbName);
            var catalog = GetCurrentDbCatalog(dbName);

            return BuildConnectionString(config, catalog);
        }

        public static string ConnectionStringNameToDbName(string connectionStringName)
        {
            return connectionStringName.Replace("Connection", string.Empty);
        }

        public static string ObtainConnectionStringForHostingType(string connectionStringName, bool allowProductionOverride = false)
        {

            string dbName = ConnectionStringNameToDbName(connectionStringName);
            var csBuilder = GetConnectionStringFromDbName(dbName);

            if (!allowProductionOverride)
            {
                // Only allow production to access live database when reading for clone back to Test or Development!!
                if (IsProduction() == false && HostingMode == Deployment.Production)
                    throw new InvalidOperationException("<NOT ALLOWED> ONLY ALLOW PRODUCTION TO ACCESS LIVE DATABASE <NOT ALLOWED> \n<ERROR> NO PRODUCTION.INI file! <ERROR>");

                if (Debugger.IsAttached && HostingMode == Deployment.Production)
                    Debug.Assert(false, "< CAUTION > ONLY ALLOW PRODUCTION TO ACCESS LIVE DATABASE < CAUTION >");
            }

            return csBuilder.ToString();
        }

        private static string GetCurrentDbCatalog(string dbName)
        {
            string suffix;

            switch (HostingMode)
            {
                case Deployment.Production:
                    suffix = "_Live";
                    break;
                case Deployment.Testing:
                    suffix = "_Test";
                    break;
                case Deployment.Development_Ui_Test_Api:
                case Deployment.Development:
                default:
                    suffix = "_Dev";
                    break;
            }

            return dbName + suffix;
        }

#if NET48
        private static void UpdateConfigurationManagerConnectionString(string name, string connectionString)
        {
            // Reflection to set change readonly connection string to <DEBUG> or [PRODUCTION]
            // Update the connection string in ConfigurationManager
            // Assuming the connection string exists in the configuration
            var settings = ConfigurationManager.ConnectionStrings[name];
            if (settings == null)
            {
                // Unset readonly on the ConnectionStrings collection
                var settingsCollection = ConfigurationManager.ConnectionStrings;
                var collectionType = typeof(ConfigurationElementCollection);
                var readOnlyField = collectionType.GetField("bReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
                readOnlyField.SetValue(settingsCollection, false);

                // Create and add the new connection string
                var newSettings = new ConnectionStringSettings(name, connectionString, "System.Data.SqlClient");
                settingsCollection.Add(newSettings);

                // Revert collection to readonly
                readOnlyField.SetValue(settingsCollection, true);
            }
            else
            {
                // Reflection is required to modify the connection string
                var fi = typeof(ConfigurationElement).GetField("_bReadOnly", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                fi.SetValue(settings, false);

                settings.ConnectionString = connectionString;
                settings.ProviderName = "System.Data.SqlClient";


                // Re-set the read only flag
                fi.SetValue(settings, true);
            }
        }

        public static void UpdateDatabaseConnectionNET48FromDeployment()
        {
            // Pre Update
            _log?.Invoke($"\n==============\r\nPRE UPDATE : Update Database Connections\r\n============");
            foreach (ConnectionStringSettings con in ConfigurationManager.ConnectionStrings)
            {
                _log?.Invoke($"\n{con.Name} = {con}");
            }

            // Updates ONLY those connection strings whose name contains "Connection"
            foreach (ConnectionStringSettings con in ConfigurationManager.ConnectionStrings)
            {
                if (con.Name != null && con.Name.Contains("Connection"))
                {
                    string connectionString = ObtainConnectionStringForHostingType(connectionStringName: con.Name);
                    UpdateConfigurationManagerConnectionString(name: con.Name, connectionString);
                }
            }

            _log?.Invoke($"\n==============\r\n<< POST  UPDATE : Connection Strings >>\r\n============");
            // Post Update
            foreach (ConnectionStringSettings con in ConfigurationManager.ConnectionStrings)
            {
                _log?.Invoke($"\n{con.Name} = {con}");
            }
        }
#endif


#if NET8_0_OR_GREATER
        private static void UpdateDatabaseConnectionNETCoreFromDeployment()
        {
            // Pre Update
            _log?.Invoke($"\n==============\r\nPRE UPDATE : Update Database Connections\r\n============");
            foreach (var connectionStringName in Runtime_Config.ConnectionStrings.Keys.ToList())
            {
                _log?.Invoke($"\n{connectionStringName} = {ObtainConnectionStringForHostingType(connectionStringName)}");
            }


            _log?.Invoke($"\n==============\r\n<< POST  UPDATE : Connection Strings >>\r\n============");
            // Post Update

            // Updates ALL appsettings.json connection strings
            foreach (var connectionStringName in Runtime_Config.ConnectionStrings.Keys.ToList())
            {
                Runtime_Config.ConnectionStrings[connectionStringName] = ObtainConnectionStringForHostingType(connectionStringName);
            }

            // To Do : Where is this used? 
            Runtime_Config.UpdateUrlWithApiIP();
        }
#endif
    }
}