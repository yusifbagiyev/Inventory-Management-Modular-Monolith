using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace SharedServices.Logging
{
    public class SanitizingLogEventEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            // Sanitize the message
            var sanitizedMessage = LogSanitizer.SanitizeLogMessage(logEvent.MessageTemplate.Text);

            // Create new properties collection with sanitized values
            var sanitizedProperties = new Dictionary<string, LogEventPropertyValue>();

            foreach (var property in logEvent.Properties)
            {
                if (property.Value is ScalarValue scalarValue)
                {
                    var value = scalarValue.Value?.ToString() ?? "";
                    var sanitizedValue = LogSanitizer.SanitizeLogMessage(value);
                    sanitizedProperties[property.Key] = new ScalarValue(sanitizedValue);
                }
                else if (property.Value is StructureValue structureValue)
                {
                    sanitizedProperties[property.Key] = SanitizeStructure(structureValue);
                }
                else
                {
                    sanitizedProperties[property.Key] = property.Value;
                }
            }

            // Replace properties with sanitized versions
            foreach (var prop in sanitizedProperties)
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(prop.Key, prop.Value));
            }
        }

        private StructureValue SanitizeStructure(StructureValue structure)
        {
            var sanitizedProperties = new List<LogEventProperty>();

            foreach (var prop in structure.Properties)
            {
                if (prop.Value is ScalarValue scalarValue)
                {
                    var value = scalarValue.Value?.ToString() ?? "";
                    var sanitizedValue = LogSanitizer.SanitizeLogMessage(value);
                    sanitizedProperties.Add(new LogEventProperty(prop.Name, new ScalarValue(sanitizedValue)));
                }
                else
                {
                    sanitizedProperties.Add(prop);
                }
            }

            return new StructureValue(sanitizedProperties, structure.TypeTag);
        }
    }

    public static class LoggingExtensions
    {
        public static LoggerConfiguration WithSanitization(this LoggerEnrichmentConfiguration enrich)
        {
            return enrich.With<SanitizingLogEventEnricher>();
        }

        public static IHostBuilder ConfigureSanitizedLogging(this IHostBuilder builder, IConfiguration configuration)
        {
            return builder.UseSerilog((context, services, loggerConfiguration) =>
            {
                loggerConfiguration
                    .ReadFrom.Configuration(configuration)
                    .Enrich.FromLogContext()
                    .Enrich.WithSanitization()
                    .Enrich.WithProperty("ApplicationName", context.HostingEnvironment.ApplicationName)
                    .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                    .WriteTo.Seq(
                        serverUrl: configuration.GetConnectionString("Seq") ?? "http://localhost:5342",
                        restrictedToMinimumLevel: LogEventLevel.Information);
            });
        }
    }
}