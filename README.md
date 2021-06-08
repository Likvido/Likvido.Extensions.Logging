# Likvido.Extensions.Logging [![GitHub Workflow Status](https://img.shields.io/github/workflow/status/likvido/Likvido.Extensions.Logging/Publish%20to%20nuget)](https://github.com/Likvido/Likvido.Extensions.Logging/actions?query=workflow%3A%22Publish+to+nuget%22) [![Nuget](https://img.shields.io/nuget/v/Likvido.Extensions.Logging)](https://www.nuget.org/packages/Likvido.Extensions.Logging/)
Adds a convenient way to replace logging implementation on .NET Core with Serilog for IServicecollection. Very similar to https://github.com/serilog/serilog-extensions-hosting but can be used for apps without hosting capabilities.
# Usage
Similar to https://github.com/serilog/serilog-extensions-hosting
```
private static void Configure(IConfiguration configuration, IServiceCollection services)
{
    services.AddSingleton<ContextFactory<ApplicationDbContext>>();
    services.UseSerilog((serviceProvider, loggerConfiguration) =>
    {
        loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext();
    });
}
```
