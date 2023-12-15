using System;
using System.Diagnostics;
using System.Net;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Orleans.Configuration;
using Orleans.Configuration.Internal;
using Orleans.Runtime;
using Orleans.Runtime.MembershipService;

using Orleans.Extension.Clustering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Orleans.Hosting;
public static class SiloBuilderClusteringExtensions {
    public const string ModeAdoNet = "AdoNet";
    public const string ModeLocalhost = "Localhost";
    public const string ModeInMemory = "InMemory";
    
    /*
    public static async Task<ClusterForkFeedback> HandleDevelopmentFork(
        Assembly assembly,
        string[] args) {
        */


    public static ISiloBuilder UseOrleansByConfiguration(
        this ISiloBuilder siloBuilder,
        IConfiguration configurationRoot,
        string? sectionName = default,
        ClusteringOptions? fallbackOptions = default) {
        var config = GetClusteringOptions(configurationRoot, sectionName, fallbackOptions);
        siloBuilder.ConfigureClusterOptions(config);
        siloBuilder.UseClusteringByConfiguration(config);
        siloBuilder.UseReminderServiceByConfiguration(config);
        siloBuilder.UseDefaultGrainStorageByConfiguration(config);

        // Microsoft.Orleans.Clustering.AzureStorage
        // "AdoNetClustering"

        siloBuilder.ConfigureEndpointsByConfiguration(config);

        return siloBuilder;
    }

    private static ClusteringOptions GetClusteringOptions(IConfiguration configurationRoot, string? sectionName, ClusteringOptions? fallbackOptions) {
        var config = ClusteringOptions.Empty();
        if (string.Equals(sectionName, string.Empty, StringComparison.Ordinal)) {
            configurationRoot.Bind(config);
        } else {
            var configuration = configurationRoot.GetSection(sectionName ?? ClusteringOptions.DefaultSectionName);
            if (configuration.Exists()) {
                configuration.Bind(config);
            } else if (fallbackOptions is not null) {
                config = fallbackOptions;
            }
        }

        return config;
    }

    // https://learn.microsoft.com/en-us/dotnet/orleans/host/configuration-guide/typical-configurations#unreliable-deployment-on-a-cluster-of-dedicated-servers

    public static ISiloBuilder ConfigureClusterOptions(
        this ISiloBuilder siloBuilder,
        ClusteringOptions config) {
        siloBuilder.Configure<ClusterOptions>(options => {
            if (!string.IsNullOrEmpty(config.ClusterId)) {
                options.ClusterId = config.ClusterId;
            }
            if (!string.IsNullOrEmpty(config.ServiceId)) {
                options.ServiceId = config.ServiceId;
            }

            if (config.Fork.TryGetForked(out var forkIndex, out var forkCount)) {
                options.ServiceId = $"{options.ServiceId}{forkIndex}";
            }
        });
        return siloBuilder;
    }

    public static ISiloBuilder UseClusteringByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions config) {
        if (!config.TryGetConnectionStringClustering(out var effectiveConnectionString)) {
            throw new InvalidOperationException("No ConnectionString for Clustering - 'ConnectionStringClustering'");
        }
        var mode = effectiveConnectionString.GetMode(ModeAdoNet);
        if (string.Equals(mode, ModeAdoNet)) {
            return siloBuilder.UseAdoNetClusteringByConfiguration(config);
        }

        if (string.Equals(mode, ModeLocalhost)) {
            return siloBuilder.UseLocalhostClustering();
        }

        throw new InvalidOperationException("ClusterMode is unexpected. Known Localhost|AdoNet");
        //return siloBuilder;
    }

    // https://learn.microsoft.com/en-us/dotnet/orleans/host/configuration-guide/configuring-ado-dot-net-providers
    public static ISiloBuilder UseAdoNetClusteringByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions config) {
        if (!config.TryGetConnectionStringClustering(out var effectiveConnectionString)) {
            throw new InvalidOperationException("No ConnectionString for Clustering - 'ConnectionString:Clustering'");
        }
        var mode = effectiveConnectionString.GetMode(ModeAdoNet);
        if (!string.Equals(mode, ModeAdoNet)) {
            throw new InvalidOperationException("ConnectionString for Clustering is not for AdoNet");
        }
        var invariant = effectiveConnectionString.GetInvariant("Microsoft.Data.SqlClient");
        siloBuilder.UseAdoNetClustering(options => {
            options.Invariant = invariant;
            options.ConnectionString = effectiveConnectionString.ConnectionString;
        });
        return siloBuilder;
    }

    public static ISiloBuilder UseReminderServiceByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions config
        ) {
        if (!config.TryGetConnectionStringReminder(out var effectiveConnectionString)) {
            return siloBuilder;
        }
        var mode = effectiveConnectionString.GetMode(ModeAdoNet);
        if (!string.Equals(mode, ModeAdoNet)) {
            return siloBuilder.UseAdoNetReminderByConfiguration(config);
        }
        return siloBuilder;
    }

    public static ISiloBuilder UseAdoNetReminderByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions config) {
        if (!config.TryGetConnectionStringReminder(out var effectiveConnectionString)) {
            throw new InvalidOperationException("No ConnectionString for Reminder");
        }
        var mode = effectiveConnectionString.GetMode(ModeAdoNet);
        if (!string.Equals(mode, ModeAdoNet)) {
            throw new InvalidOperationException("ConnectionString for Reminder is not for AdoNet");
        }
        var invariant = effectiveConnectionString.GetInvariant("Microsoft.Data.SqlClient");
        siloBuilder.UseAdoNetReminderService(options => {
            options.Invariant = invariant;
            options.ConnectionString = effectiveConnectionString.ConnectionString;
        });
        //siloBuilder.UseInMemoryReminderService
        return siloBuilder;
    }

    public static ISiloBuilder UseDefaultGrainStorageByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions config) {
        if (!config.TryGetConnectionStringGrainStorage("Default", out var effectiveConnectionString)) {
            return siloBuilder;
        }
        var mode = effectiveConnectionString.GetMode(ModeAdoNet);
        if (string.Equals(mode, ModeAdoNet)) {
            return siloBuilder.AddDefaultAdoNetGrainStorageByConfiguration(config);
        }
        return siloBuilder;
    }

    public static ISiloBuilder AddNamedGrainStorageByConfiguration(
        this ISiloBuilder siloBuilder,
        string name,
        ClusteringOptions config) {
        if (!config.TryGetConnectionStringGrainStorage(name, out var effectiveConnectionString)) {
            return siloBuilder;
        }
        var mode = effectiveConnectionString.GetMode(ModeAdoNet);
        if (string.Equals(mode, ModeAdoNet)) {
            return siloBuilder.AddNamedAdoNetGrainStorageByConfiguration(name, config);
        }
        return siloBuilder;
    }
    public static ISiloBuilder AddDefaultAdoNetGrainStorageByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions config) {
        if (!config.TryGetConnectionStringGrainStorage("Default", out var effectiveConnectionString)) {
            throw new InvalidOperationException("No ConnectionString for GrainStorage");
        }
        var mode = effectiveConnectionString.GetMode(ModeAdoNet);
        if (!string.Equals(mode, ModeAdoNet)) {
            throw new InvalidOperationException("ConnectionString for GrainStorage is not for AdoNet");
        }
        var invariant = effectiveConnectionString.GetInvariant("Microsoft.Data.SqlClient");
        siloBuilder.AddAdoNetGrainStorageAsDefault(options => {
            options.Invariant = invariant;
            options.ConnectionString = effectiveConnectionString.ConnectionString;
        });
        return siloBuilder;
    }

    public static ISiloBuilder AddNamedAdoNetGrainStorageByConfiguration(
        this ISiloBuilder siloBuilder,
        string name,
        ClusteringOptions config) {
        if (!config.TryGetConnectionStringGrainStorage(name, out var effectiveConnectionString)) {
            throw new InvalidOperationException("No ConnectionString for GrainStorage");
        }
        var mode = effectiveConnectionString.GetMode(ModeAdoNet);
        if (!string.Equals(mode, ModeAdoNet)) {
            throw new InvalidOperationException($"ConnectionString {name}-{effectiveConnectionString.Name} for GrainStorage is not for AdoNet");
        }
        var invariant = effectiveConnectionString.GetInvariant("Microsoft.Data.SqlClient");
        siloBuilder.AddAdoNetGrainStorage(name, options => {
            options.Invariant = invariant;
            options.ConnectionString = effectiveConnectionString.ConnectionString;
        });
        return siloBuilder;
    }

#if false
var siloHostBuilder = new SiloHostBuilder();

var invariant = "System.Data.SqlClient";
var connectionString = "Data Source=(localdb)\MSSQLLocalDB;" +
    "Initial Catalog=Orleans;Integrated Security=True;" +
    "Pooling=False;Max Pool Size=200;" +
    "Asynchronous Processing=True;MultipleActiveResultSets=True";

// Use ADO.NET for clustering
siloHostBuilder.UseAdoNetClustering(options =>
{
    options.Invariant = invariant;
    options.ConnectionString = connectionString;
});
// Use ADO.NET for reminder service
siloHostBuilder.UseAdoNetReminderService(options =>
{
    options.Invariant = invariant;
    options.ConnectionString = connectionString;
});
// Use ADO.NET for persistence
siloHostBuilder.AddAdoNetGrainStorage("GrainStorageForTest", options =>
{
    options.Invariant = invariant;
    options.ConnectionString = connectionString;
});
#endif

    public static ISiloBuilder ConfigureEndpointsByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions config) {
        siloBuilder.Configure<EndpointOptions>(options => {

            bool forkEnabled = config.Fork.TryGetForked(out var forkIndex, out var forkCount);
            if (config.Endpoint.AdvertisedIPAddress is not null) {
                options.AdvertisedIPAddress = config.Endpoint.AdvertisedIPAddress;
            }
            if (config.Endpoint.GatewayPort.HasValue) {
                options.GatewayPort = config.Endpoint.GatewayPort.Value + forkIndex;
            } else {
                if (forkEnabled) {
                    options.GatewayPort = Orleans.Configuration.EndpointOptions.DEFAULT_GATEWAY_PORT + forkIndex;
                }
            }
            if (config.Endpoint.GatewayListeningEndpoint is not null) {
                options.GatewayListeningEndpoint = config.Endpoint.GatewayListeningEndpoint;
            } else if (options.AdvertisedIPAddress is null
                || options.AdvertisedIPAddress.Equals(IPAddress.Any)) {
                options.GatewayListeningEndpoint = new IPEndPoint(IPAddress.Any, options.GatewayPort);
            } else if (options.AdvertisedIPAddress is not null
                && options.AdvertisedIPAddress.Equals(IPAddress.IPv6Any)) {
                options.GatewayListeningEndpoint = new IPEndPoint(IPAddress.IPv6Any, options.GatewayPort);
            }

            if (config.Endpoint.SiloPort.HasValue) {
                options.SiloPort = config.Endpoint.SiloPort.Value+forkIndex;
            } else {
                if (forkEnabled) { 
                    options.SiloPort = Orleans.Configuration.EndpointOptions.DEFAULT_SILO_PORT + forkIndex;
                }
            }
            if (config.Endpoint.SiloListeningEndpoint is not null) {
                options.SiloListeningEndpoint = config.Endpoint.SiloListeningEndpoint;
            } else if (options.AdvertisedIPAddress is null
                || options.AdvertisedIPAddress.Equals(IPAddress.Any)) {
                options.SiloListeningEndpoint = new IPEndPoint(IPAddress.Any, options.SiloPort);
            } else if (options.AdvertisedIPAddress is not null
                && options.AdvertisedIPAddress.Equals(IPAddress.IPv6Any)) {
                options.SiloListeningEndpoint = new IPEndPoint(IPAddress.IPv6Any, options.SiloPort);
            }
            System.Console.Out.WriteLine($"Silo: {options.SiloListeningEndpoint}, Gateway: {options.GatewayListeningEndpoint}");
        });
        return siloBuilder;
    }


#if false
const string connectionString = "YOUR_CONNECTION_STRING_HERE";
var silo = new HostBuilder()
    .UseOrleans(builder =>
    {
        .Configure<ClusterOptions>(options =>
        {
            options.ClusterId = "Cluster42";
            options.ServiceId = "MyAwesomeService";
        })
        .UseAdoNetClustering(options =>
        {
          options.ConnectionString = connectionString;
          options.Invariant = "System.Data.SqlClient";
        })
        .ConfigureEndpoints(siloPort: 11111, gatewayPort: 30000)
        .ConfigureLogging(builder => builder.SetMinimumLevel(LogLevel.Warning).AddConsole())
    })
    .Build();
#endif

}
