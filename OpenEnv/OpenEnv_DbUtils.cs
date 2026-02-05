using System;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

#if NET48
using System.Configuration;
using System.Data.SqlClient;
using System.Threading;
using System.IO;
#endif

#if NET8_0_OR_GREATER
using Microsoft.Data.SqlClient;
#endif

namespace OpenEnv
{
    public static partial class OpenEnv
    {
#if NET48  // DB Utils only IN NET48
        public static bool CloneDb(DBConfig source, DBConfig dest, Action<string> log = null)
        {
            log?.Invoke($"\t>Starting CloneDb from {source.DBName} to {dest.DBName}");

            string sourceBackupFilename = BackupDatabase(source, log);
            if (string.IsNullOrEmpty(sourceBackupFilename))
            {
                log?.Invoke($"\t>BackupDatabase failed for {source.DBName}");
                return false;
            }

            source.BackupPath = Path.Combine(source.BackupFolder, sourceBackupFilename);
            dest.BackupPath = Path.Combine(dest.BackupFolder, $"{dest.DBName}_{DateTime.Now:yyyyMMddHHmm}.bak");
            log?.Invoke($"\t>Attempting CopyBackupFile from \r\n\t{source.BackupPath}\r\n\t{dest.BackupPath}");

            if (!CopyBackupFile(source, dest, log))
            {
                log?.Invoke($"\t>CopyBackupFile failed from {source.BackupPath} to {dest.BackupPath}");
                return false;
            }

            bool restoreResult = RestoreToDest(dest, log);
            if (restoreResult)
            {
                log?.Invoke($"\t>CloneDb completed successfully from {source.DBName} to {dest.DBName}");
            }
            else
            {
                log?.Invoke($"\t>RestoreToDest failed for {dest.DBName}");
            }

            return restoreResult;
        }

        private static string BackupDatabase(DBConfig dbConfig, Action<string> log)
        {
            log?.Invoke($"\t>Starting BackupDatabase for {dbConfig.DBName} on {dbConfig.DataSource} to {dbConfig.BackupFolder}");

            var connection = BuildConnectionString(dbConfig, "REPLACED");

            string backupFilename = $"{dbConfig.DBName}_{DateTime.Now:yyyyMMddHHmm}.bak";
            string sqlQuery = $"BACKUP DATABASE {dbConfig.DBName} TO DISK = '{dbConfig.BackupFolder}\\{backupFilename}'";

            log?.Invoke($"\t>Backup query: {sqlQuery}");

            using (SqlConnection con = new SqlConnection(connection.ToString()))
            {
                log?.Invoke($"\t>Opening SQL connection to {dbConfig.DataSource}");
                con.Open();
                log?.Invoke($"\t>SQL connection opened");

                using (SqlCommand cmd = new SqlCommand(sqlQuery, con))
                {
                    log?.Invoke($"\t>Executing backup command");
                    cmd.ExecuteNonQuery();
                    log?.Invoke($"\t>Backup successful: {backupFilename}");
                }
            }

            log?.Invoke($"\t>BackupDatabase completed for {dbConfig.DBName}");
            return backupFilename;
        }

        private static bool CopyBackupFile(DBConfig source, DBConfig dest, Action<string> log)
        {
            log?.Invoke($"\t>Starting CopyBackupFile from {source.BackupFolder} to {dest.BackupFolder}");
            Directory.CreateDirectory(Path.GetDirectoryName(dest.BackupFolder));
            log?.Invoke($"\t>Destination directory created or already exists: {Path.GetDirectoryName(dest.BackupFolder)}");

            const int maxRetries = 5;
            const int delayMilliseconds = 2000;
            int retries = 0;

            log?.Invoke($"\t>Using source credentials: {source.Environment.NetworkCredentials.UserName ?? "None"}");
            log?.Invoke($"\t>Using destination credentials: {dest.Environment.NetworkCredentials.UserName ?? "None"}");

            while (true)
            {
                try
                {
                    log?.Invoke($"\t>Attempting to copy file. Retry {retries}/{maxRetries}");

                    // Use the selected credentials to provide the username and password
                    using (new NetworkConnection(Path.GetDirectoryName(source.BackupPath), source.Environment.NetworkCredentials))
                    using (new NetworkConnection(Path.GetDirectoryName(dest.BackupPath), dest.Environment.NetworkCredentials))
                    {
                        File.Copy(source.BackupPath, dest.BackupPath, true);
                    }

                    log?.Invoke($"\t>File copy successful from {source.BackupPath} to {dest.BackupPath}");
                    return true;
                }
                catch (IOException ex) when (retries < maxRetries)
                {
                    Debug.Print(ex.ToString());
                    retries++;
                    log?.Invoke($"\t>Retry {retries}/{maxRetries}: Unable to copy file. Retrying in {delayMilliseconds / 1000} seconds...");
                    Thread.Sleep(delayMilliseconds);
                }
                catch (Exception ex)
                {
                    log?.Invoke($"\t>Unexpected error: {ex.Message}");
                    throw;
                }
            }
        }

        private static bool RestoreToDest(DBConfig dest, Action<string> log)
        {
            var dbConnection = BuildConnectionString(dest, "REPLACED");

            string mdfLogicalName = "";
            string ldfLogicalName = "";

            string fileListQuery = $"RESTORE FILELISTONLY FROM DISK = '{dest.BackupPath}'";

            using (SqlConnection con = new SqlConnection(dbConnection.ToString()))
            {
                con.Open();

                using (SqlCommand cmd = new SqlCommand(fileListQuery, con))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader["Type"].ToString() == "D")
                        {
                            mdfLogicalName = reader["LogicalName"].ToString();
                        }
                        else if (reader["Type"].ToString() == "L")
                        {
                            ldfLogicalName = reader["LogicalName"].ToString();
                        }
                    }
                }

                string mdfFileName = $"{dest.DBName}.mdf";
                string ldfFileName = $"{dest.DBName}_log.ldf";

                // Set the database to single-user mode to forcefully disconnect any existing connections
                string setSingleUserQuery = $"ALTER DATABASE [{dest.DBName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
                using (SqlCommand cmd = new SqlCommand(setSingleUserQuery, con))
                {
                    cmd.ExecuteNonQuery();
                    log?.Invoke($"\t>Database {dest.DBName} set to single-user mode.");
                }

                string restoreQuery = $@"
            RESTORE DATABASE {dest.DBName} 
            FROM DISK = '{dest.BackupPath}' 
            WITH REPLACE,
                 MOVE '{mdfLogicalName}' TO '\\{dest.DataSource}\MsSqlDb\{mdfFileName}', 
                 MOVE '{ldfLogicalName}' TO '\\{dest.DataSource}\MsSqlDb\{ldfFileName}'";

                using (SqlCommand cmd = new SqlCommand(restoreQuery, con))
                {
                    cmd.ExecuteNonQuery();
                    log?.Invoke($"\t>Backup restored successfully to {dest.DBName}.");
                }

                string renameLogicalFile = $"ALTER DATABASE [{dest.DBName}] MODIFY FILE ( NAME = {mdfLogicalName}, NEWNAME = {dest.DBName} );";
                using (SqlCommand cmd = new SqlCommand(renameLogicalFile, con))
                {
                    cmd.ExecuteNonQuery();
                    log?.Invoke($"\t>Logical name {mdfLogicalName} renamed to {dest.DBName}");
                }

                string renameLogicalFileLog = $"ALTER DATABASE [{dest.DBName}] MODIFY FILE ( NAME = {ldfLogicalName}, NEWNAME = {dest.DBName}_log );";
                using (SqlCommand cmd = new SqlCommand(renameLogicalFileLog, con))
                {
                    cmd.ExecuteNonQuery();
                    log?.Invoke($"\t>Logical name {ldfLogicalName} renamed to {dest.DBName}_log");
                }

                string setOnlineQuery = $"ALTER DATABASE [{dest.DBName}] SET ONLINE";
                using (SqlCommand cmd = new SqlCommand(setOnlineQuery, con))
                {
                    cmd.ExecuteNonQuery();
                    log?.Invoke($"\t>Database {dest.DBName} is now online.");
                }

                string setMultiUserQuery = $"ALTER DATABASE [{dest.DBName}] SET MULTI_USER";
                using (SqlCommand cmd = new SqlCommand(setMultiUserQuery, con))
                {
                    cmd.ExecuteNonQuery();
                    log?.Invoke($"\t>Database {dest.DBName} set to multi-user mode.");
                }

                return true;
            }
        }

        public static List<string> GetAllDbNames(DBConfig dbConfig)
        {
            List<string> DatabaseNames = new List<string>();

            // Create a connection to the master database to check if the destination database exists
            var masterBuilder = new SqlConnectionStringBuilder
            {
                DataSource = dbConfig.DataSource,
                InitialCatalog = "master",
                UserID = dbConfig.Environment.SqlCredentials.Username,
                Password = dbConfig.Environment.SqlCredentials.Password
            };

            using (SqlConnection masterConn = new SqlConnection(masterBuilder.ConnectionString))
            {
                masterConn.Open();
                Debug.WriteLine("Connected to master database.");
                string checkDbExistsQuery = $"SELECT Name FROM sys.databases";
                using (SqlCommand cmd = new SqlCommand(checkDbExistsQuery, masterConn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string dbName = reader.GetString(0);
                        if (!dbName.StartsWith("sys") &&
                            !dbName.StartsWith("model") &&
                            !dbName.StartsWith("master") &&
                            !dbName.StartsWith("tempdb") &&
                            !dbName.StartsWith("msdb") &&
                            !dbName.EndsWith("_dev", StringComparison.OrdinalIgnoreCase) &&
                            !dbName.EndsWith("_test", StringComparison.OrdinalIgnoreCase) &&
                            !dbName.EndsWith("_live", StringComparison.OrdinalIgnoreCase))
                        {
                            DatabaseNames.Add(dbName);
                        }
                    }
                }
            }
            return DatabaseNames;
        }
#endif

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
        public static void UpdateDatabaseConnectionNET48FromDeployment()
        {
            // Pre Update
            Debug.WriteLine("==============\r\nPRE UPDATE : Update Database Connections\r\n============");
            foreach (ConnectionStringSettings con in ConfigurationManager.ConnectionStrings)
            {
                Debug.WriteLine($"{con.Name} = {con}");
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

            Debug.WriteLine("==============\r\n<< POST  UPDATE : Connection Strings >>\r\n============");
            // Post Update
            foreach (ConnectionStringSettings con in ConfigurationManager.ConnectionStrings)
            {
                Debug.WriteLine($"{con.Name} = {con}");
            }
        }
#endif


#if NET8_0_OR_GREATER
    private static void UpdateDatabaseConnectionNETCoreFromDeployment()
    {
        // Pre Update
        Console.WriteLine("==============\r\nPRE UPDATE : Update Database Connections\r\n============");
        foreach (var connectionStringName in Runtime_Config.ConnectionStrings.Keys.ToList())
        {
            Console.WriteLine($"{connectionStringName} = {ObtainConnectionStringForHostingType(connectionStringName)}");
        }


        Console.WriteLine("==============\r\n<< POST  UPDATE : Connection Strings >>\r\n============");
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
#endif

        public static SqlConnectionStringBuilder GetConnectionStringFromDbName(string dbName)
        {
            var config = GetCurrentDbConfig(dbName);
            var catalog = GetCurrentDbCatalog(dbName);

            return BuildConnectionString(config, catalog);
        }
    }
}