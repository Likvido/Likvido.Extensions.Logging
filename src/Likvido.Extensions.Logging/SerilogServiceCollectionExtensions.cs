using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace Likvido.Extensions.Logging
{
    public static class SerilogServiceCollectionExtensions
    {
        // Does nothing, but makes it easy to create an `ILogger` from a Serilog `Logger`
        // that will not dispose the underlying pipeline when disposed itself.
        class NullEnricher : ILogEventEnricher
        {
            public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
            }
        }
        // Used internally to pass information through the container. We need to do this because if `logger` is the
        // root logger, registering it as a singleton may lead to disposal along with the container by MEDI. This isn't
        // always desirable, i.e. we may be handed a logger and `dispose: false`, so wrapping it keeps us in control
        // of when the logger is disposed.
        class RegisteredLogger
        {
            public RegisteredLogger(Serilog.ILogger logger)
            {
                Logger = logger;
            }

            public Serilog.ILogger Logger { get; }
        }

        /// <summary>
        /// Sets Serilog as the logging provider.
        /// </summary>
        /// <param name="collection">The host builder to configure.</param>
        /// <param name="logger">The Serilog logger; if not supplied, the static <see cref="Serilog.Log"/> will be used.</param>
        /// <param name="dispose">When <c>true</c>, dispose <paramref name="logger"/> when the framework disposes the provider. If the
        /// logger is not specified but <paramref name="dispose"/> is <c>true</c>, the <see cref="Serilog.Log.CloseAndFlush()"/> method will be
        /// called on the static <see cref="Serilog.Log"/> class instead.</param>
        /// <param name="providers">A <see cref="LoggerProviderCollection"/> registered in the Serilog pipeline using the
        /// <c>WriteTo.Providers()</c> configuration method, enabling other <see cref="Microsoft.Extensions.Logging.ILoggerProvider"/>s to receive events. By
        /// default, only Serilog sinks will receive events.</param>
        /// <returns>The host builder.</returns>
        public static IServiceCollection UseSerilog(
            this IServiceCollection collection,
            Serilog.ILogger logger = null,
            bool dispose = false,
            LoggerProviderCollection providers = null)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (providers != null)
            {
                collection.AddSingleton<ILoggerFactory>(services =>
                {
                    var factory = new SerilogLoggerFactory(logger, dispose, providers);

                    foreach (var provider in services.GetServices<ILoggerProvider>())
                    {
                        factory.AddProvider(provider);
                    }

                    return factory;
                });
            }
            else
            {
                collection.AddSingleton<ILoggerFactory>(services => new SerilogLoggerFactory(logger, dispose));
            }

            return collection;
        }

        /// <summary>Sets Serilog as the logging provider.</summary>
        /// <remarks>
        /// A <see cref="IServiceCollection"/> is supplied so that configuration and hosting information can be used.
        /// The logger will be shut down when application services are disposed.
        /// </remarks>
        /// <param name="builder">The host builder to configure.</param>
        /// <param name="configureLogger">The delegate for configuring the <see cref="Serilog.LoggerConfiguration" /> that will be used to construct a <see cref="Serilog.Core.Logger" />.</param>
        /// <param name="preserveStaticLogger">Indicates whether to preserve the value of <see cref="Serilog.Log.Logger"/>.</param>
        /// <param name="writeToProviders">By default, Serilog does not write events to <see cref="ILoggerProvider"/>s registered through
        /// the Microsoft.Extensions.Logging API. Normally, equivalent Serilog sinks are used in place of providers. Specify
        /// <c>true</c> to write events to all providers.</param>
        /// <returns>The host builder.</returns>
        public static IServiceCollection UseSerilog(
            this IServiceCollection collection,
            Action<Serilog.LoggerConfiguration> configureLogger,
            bool preserveStaticLogger = false,
            bool writeToProviders = false)
        {
            if (collection is null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (configureLogger == null)
            {
                throw new ArgumentNullException(nameof(configureLogger));
            }

            return UseSerilog(
                collection,
                (services, loggerConfiguration) =>
                    configureLogger(loggerConfiguration),
                preserveStaticLogger: preserveStaticLogger,
                writeToProviders: writeToProviders);
        }

        /// <summary>Sets Serilog as the logging provider.</summary>
        /// <remarks>
        /// A <see cref="HostBuilderContext"/> is supplied so that configuration and hosting information can be used.
        /// The logger will be shut down when application services are disposed.
        /// </remarks>
        /// <param name="builder">The host builder to configure.</param>
        /// <param name="configureLogger">The delegate for configuring the <see cref="Serilog.LoggerConfiguration" /> that will be used to construct a <see cref="Serilog.Core.Logger" />.</param>
        /// <param name="preserveStaticLogger">Indicates whether to preserve the value of <see cref="Serilog.Log.Logger"/>.</param>
        /// <param name="writeToProviders">By default, Serilog does not write events to <see cref="ILoggerProvider"/>s registered through
        /// the Microsoft.Extensions.Logging API. Normally, equivalent Serilog sinks are used in place of providers. Specify
        /// <c>true</c> to write events to all providers.</param>
        /// <remarks>If the static <see cref="Log.Logger"/> is a bootstrap logger (see
        /// <c>LoggerConfigurationExtensions.CreateBootstrapLogger()</c>), and <paramref name="preserveStaticLogger"/> is
        /// not specified, the the bootstrap logger will be reconfigured through the supplied delegate, rather than being
        /// replaced entirely or ignored.</remarks>
        /// <returns>The host builder.</returns>
        public static IServiceCollection UseSerilog(
            this IServiceCollection collection,
            Action<IServiceProvider, LoggerConfiguration> configureLogger,
            bool preserveStaticLogger = false,
            bool writeToProviders = false)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (configureLogger == null)
            {
                throw new ArgumentNullException(nameof(configureLogger));
            }

            LoggerProviderCollection loggerProviders = null;
            if (writeToProviders)
            {
                loggerProviders = new LoggerProviderCollection();
            }

            collection.AddSingleton(services =>
            {
                Serilog.ILogger logger;
                var loggerConfiguration = new LoggerConfiguration();

                if (loggerProviders != null)
                {
                    loggerConfiguration.WriteTo.Providers(loggerProviders);
                }

                configureLogger(services, loggerConfiguration);
                logger = loggerConfiguration.CreateLogger();

                return new RegisteredLogger(logger);
            });

            collection.AddSingleton(services =>
            {
                // How can we register the logger, here, but not have MEDI dispose it?
                // Using the `NullEnricher` hack to prevent disposal.
                var logger = services.GetRequiredService<RegisteredLogger>().Logger;
                return logger.ForContext(new NullEnricher());
            });

            collection.AddSingleton<ILoggerFactory>(services =>
            {
                var logger = services.GetRequiredService<RegisteredLogger>().Logger;

                Serilog.ILogger registeredLogger = null;
                if (preserveStaticLogger)
                {
                    registeredLogger = logger;
                }
                else
                {
                    // Passing a `null` logger to `SerilogLoggerFactory` results in disposal via
                    // `Log.CloseAndFlush()`, which additionally replaces the static logger with a no-op.
                    Log.Logger = logger;
                }

                var factory = new SerilogLoggerFactory(registeredLogger, true, loggerProviders);

                if (writeToProviders)
                {
                    foreach (var provider in services.GetServices<ILoggerProvider>())
                    {
                        factory.AddProvider(provider);
                    }
                }

                return factory;
            });

            return collection;
        }
    }
}
