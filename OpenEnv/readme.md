# OpenEnv

Manage development, testing, and production environments for your applications

## Installation

To install the OpenEnv library, use the following NuGet command:

```
Install-Package OpenEnv
```


## Program.cs
Set the Environment


Net48
```csharp

static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    static void Main()
    {
        // Set the hosting Mode - Note: This will update the connection strings in the app.config
        HostingEnvironment.Initialise();

        // Remaining code for your application startup
    }
}

```

CORE
```csharp
   
public class Program
    {
        public static async Task Main(string[] args)
        { 
            var builder = WebApplication.CreateBuilder(args);
            
            // Set the hosting Mode - Note: This will update the connection strings in the app.config
            HostingEnvironment.Initialise(builder);

            // Remaining code for your application startup
    }
}

```