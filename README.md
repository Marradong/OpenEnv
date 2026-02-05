# OpenEnv

<div align="center">
  <img width="512" height="512" alt="OpenEnv Icon" src="https://github.com/user-attachments/assets/b06eeaa8-2916-4af6-8bf0-cd1ca6eb4157" />
</div>

---

**OpenEnv** is a lightweight library for managing **development**, **testing**, and **production** environments across .NET applications.

It centralises environment-specific configuration (server IPs, credentials, backups, etc.) and applies them at application startup—removing the need to hard-code or manually swap settings between builds.

---

## Features

- Single configuration file for all environments
- Supports **.NET Framework 4.8** and **.NET (Core / 8 / 9+)**
- Automatically updates connection strings at runtime
- Works with EF / EF Visual Editor
- Keeps sensitive environment data out of source code

---

## Installation

Install via NuGet:

```powershell
Install-Package OpenEnv
```

---

## Configuration: `EnvironmentConfig.json`

Add an `EnvironmentConfig.json` file to your project containing the configuration for each environment.

⚠️ **Important**  
This file contains sensitive information.  
Do **not** commit it to public repositories. Add it to `.gitignore`.

### Example

```json
{
  "Environments": {
    "Development": {
      "ServerIP": "",
      "SqlCredentials": {
        "Username": "",
        "Password": ""
      },
      "NetworkCredentials": {
        "Username": "",
        "Password": "",
        "Domain": ""
      },
      "BackupLocation": ""
    },
    "Testing": {
      "ServerIP": "",
      "SqlCredentials": {
        "Username": "",
        "Password": ""
      },
      "NetworkCredentials": {
        "Username": "",
        "Password": "",
        "Domain": ""
      },
      "BackupLocation": ""
    },
    "Production": {
      "ServerIP": "",
      "SqlCredentials": {
        "Username": "",
        "Password": ""
      },
      "NetworkCredentials": {
        "Username": "",
        "Password": "",
        "Domain": ""
      },
      "BackupLocation": ""
    }
  }
}
```

---

## Application Startup

OpenEnv must be initialised **before** your application starts using configuration or database connections.

### .NET Framework 4.8

```csharp
static class Program
{
    static void Main()
    {
        // Initialise using a file path
        HostingEnvironment.Initialise("Path/To/Your/EnvironmentConfig.json");

        // OR initialise from raw JSON
        var json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "EnvironmentConfig.json")
        );
        HostingEnvironment.InitialiseFromJson(json);

        // Continue application startup
    }
}
```

---

### .NET / ASP.NET Core

```csharp
public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Initialise using a file path
        HostingEnvironment.Initialise(
            "Path/To/Your/EnvironmentConfig.json",
            builder
        );

        // OR initialise from raw JSON
        var json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "EnvironmentConfig.json")
        );
        HostingEnvironment.InitialiseFromJson(json, builder);

        // Continue application startup
    }
}
```

---

## Entity Framework (EF) Visual Editor

When using the **EF Visual Editor**, the connection string name must follow a specific convention.

### Step 1: Connection String Name

Set the **Connection String Name** to:

```
<MyDatabaseName>Connection
```

The name **must end with `Connection`**.

<img width="415" height="202" alt="EF connection string name example" src="https://github.com/user-attachments/assets/fd7223f6-e72b-4ab4-bb81-30ede24c3093" />

### Step 2: Config File Entry

Add the connection string to your `app.config` or `web.config`:

```xml
<connectionStrings>
  <add name="MyDBNameConnection"
       connectionString="REPLACEDBYOPENENV"
       providerName="System.Data.SqlClient" />
</connectionStrings>
```

OpenEnv will replace the placeholder value at runtime based on the active environment.

---

## Security Notes

- Treat `EnvironmentConfig.json` as a **secret**
- Store securely (e.g. local only, secured deployme
