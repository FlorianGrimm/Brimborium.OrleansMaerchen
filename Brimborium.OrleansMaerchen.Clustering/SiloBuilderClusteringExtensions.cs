namespace Orleans.Hosting;

public static class SiloBuilderClusteringExtensions {
    public const string ModeAdoNet = "AdoNet";
    public const string ModeLocalhost = "Localhost";
    public const string ModeInMemory = "InMemory";
    // TODO: add Microsoft.Orleans.Clustering.AzureStorage
    public const string ModeAzureStorage = "AzureStorage";

    private static SiloBuilderClusteringHandlers? _Handlers;
    public static SiloBuilderClusteringHandlers Handlers {
        get => _Handlers ??= SiloBuilderClusteringHandlers.Create();
        set => _Handlers = value;
    }

    public static ClusteringOptions UseOrleansByConfiguration(
        this ISiloBuilder siloBuilder,
        IConfiguration configurationRoot,
        string? sectionName = default,
        ClusteringOptions? fallbackOptions = default,
        SiloBuilderClusteringHandlers? handlers = default) {
        var clusteringOptions = GetClusteringOptions(configurationRoot, sectionName, fallbackOptions);
        return siloBuilder.UseOrleansByConfiguration(clusteringOptions, handlers);
    }

    public static ClusteringOptions UseOrleansByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions clusteringOptions,
        SiloBuilderClusteringHandlers? handlers = default) {
        handlers ??= Handlers;
        siloBuilder.ConfigureClusterOptions(clusteringOptions, handlers);
        siloBuilder.UseClusteringByConfiguration(clusteringOptions, handlers);
        siloBuilder.UseReminderServiceByConfiguration(clusteringOptions, handlers);
        siloBuilder.UseDefaultGrainStorageByConfiguration(clusteringOptions, handlers);

        // Microsoft.Orleans.Clustering.AzureStorage
        // "AdoNetClustering"

        siloBuilder.ConfigureEndpointsByConfiguration(clusteringOptions, handlers);
        return clusteringOptions;
    }

    public static ClusteringOptions GetClusteringOptions(IConfiguration configurationRoot, string? sectionName, ClusteringOptions? fallbackOptions) {
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

    public static bool ConfigureClusterOptions(
        this ISiloBuilder siloBuilder,
        ClusteringOptions clusteringOptions,
        SiloBuilderClusteringHandlers? handlers = default) {
        handlers ??= Handlers;
        siloBuilder.Configure<ClusterOptions>(options => {
            if (!string.IsNullOrEmpty(clusteringOptions.ClusterId)) {
                options.ClusterId = clusteringOptions.ClusterId;
            }
            if (!string.IsNullOrEmpty(clusteringOptions.ServiceId)) {
                options.ServiceId = clusteringOptions.ServiceId;
            }

            if (clusteringOptions.Fork.TryGetForked(out var forkIndex, out var forkCount)) {
                options.ServiceId = $"{options.ServiceId}{forkIndex}";
            }
        });
        return true;
    }

    public static bool UseClusteringByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions clusteringOptions,
        SiloBuilderClusteringHandlers? handlers = default) {
        handlers ??= Handlers;
        if (!clusteringOptions.TryGetConnectionStringClustering(out var effectiveConnectionString)) {
            throw new InvalidOperationException("No ConnectionString for Clustering - 'ConnectionStringClustering'");
            //return false;
        }

        var mode = effectiveConnectionString.GetMode(string.Empty);

        if (!string.IsNullOrEmpty(mode)
            && handlers.UseClustering.TryGetValue(mode, out var handler)) {
            return handler(siloBuilder, clusteringOptions);
        } else {
            //return false;
            var keys = string.Join(" | ", handlers.UseClustering.Keys);
            throw new InvalidOperationException($"ClusterMode is unexpected. Known {keys}");
        }
    }

    // https://learn.microsoft.com/en-us/dotnet/orleans/host/configuration-guide/configuring-ado-dot-net-providers
    public static bool UseAdoNetClusteringByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions clusteringOptions) {
        if (!clusteringOptions.TryGetConnectionStringClustering(out var effectiveConnectionString)) {
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
        return true;
    }

    public static bool UseLocalhostClusteringByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions clusteringOptions) {
        siloBuilder.UseLocalhostClustering();
        return true;
    }

    public static bool UseReminderServiceByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions clusteringOptions,
        SiloBuilderClusteringHandlers? handlers = default) {
        handlers ??= Handlers;
        if (!clusteringOptions.TryGetConnectionStringReminder(out var effectiveConnectionString)) {
            return false;
        }
        var mode = effectiveConnectionString.GetMode(ModeAdoNet);
        if (!string.IsNullOrEmpty(mode)
            && handlers.UseReminder.TryGetValue(mode, out var handler)) {
            return handler(siloBuilder, clusteringOptions);
        } else {
            return false;
        }
    }

    public static bool UseAdoNetReminderByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions clusteringOptions) {
        if (!clusteringOptions.TryGetConnectionStringReminder(out var effectiveConnectionString)) {
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

        return true;
    }

    public static bool UseInMemoryReminderReminderByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions clusteringOptions) {
        siloBuilder.UseInMemoryReminderService();
        return true;
    }

    public static bool UseDefaultGrainStorageByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions clusteringOptions,
        SiloBuilderClusteringHandlers? handlers = default) {
        handlers ??= Handlers;
        if (!clusteringOptions.TryGetConnectionStringGrainStorage("Default", out var effectiveConnectionString)) {
            return false;
        }
        var mode = effectiveConnectionString.GetMode(string.Empty);

        if (!string.IsNullOrEmpty(mode)
            && handlers.UseDefaultGrainStorage.TryGetValue(mode, out var handler)) {
            return handler(siloBuilder, clusteringOptions);
        } else {
            return false;
        }
    }

    public static bool AddNamedGrainStorageByConfiguration(
        this ISiloBuilder siloBuilder,
        string name,
        ClusteringOptions clusteringOptions,
        SiloBuilderClusteringHandlers? handlers = default) {
        handlers ??= Handlers;
        if (!clusteringOptions.TryGetConnectionStringGrainStorage(name, out var effectiveConnectionString)) {
            return false;
        }
        var mode = effectiveConnectionString.GetMode(string.Empty);
        if (!string.IsNullOrEmpty(mode)
           && handlers.UseNamedGrainStorage.TryGetValue(mode, out var handler)) {
            return handler(siloBuilder, name, clusteringOptions);
        } else {
            return false;
        }
    }

    public static bool AddDefaultAdoNetGrainStorageByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions clusteringOptions) {
        if (!clusteringOptions.TryGetConnectionStringGrainStorage("Default", out var effectiveConnectionString)) {
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
        return true;
    }

    public static bool AddNamedAdoNetGrainStorageByConfiguration(
        this ISiloBuilder siloBuilder,
        string name,
        ClusteringOptions clusteringOptions) {
        if (!clusteringOptions.TryGetConnectionStringGrainStorage(name, out var effectiveConnectionString)) {
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
        return true;
    }

    public static bool AddDefaultMemoryGrainStorageByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions clusteringOptions) {
        if (!clusteringOptions.TryGetConnectionStringGrainStorage("Default", out var effectiveConnectionString)) {
            effectiveConnectionString = new EffectiveConnectionString("Default", ModeInMemory, default, string.Empty, default);
        }
        var mode = effectiveConnectionString.GetMode(ModeInMemory);
        if (!string.Equals(mode, ModeInMemory)) {
            throw new InvalidOperationException("ConnectionString for GrainStorage is not for InMemory");
        }

        siloBuilder.AddMemoryGrainStorageAsDefault(options => {
            if (effectiveConnectionString.NumStorageGrains.HasValue) {
                options.NumStorageGrains = effectiveConnectionString.NumStorageGrains.Value;
            }
        });
        return true;
    }

    public static bool AddNamedMemoryGrainStorageByConfiguration(
        this ISiloBuilder siloBuilder,
        string name,
        ClusteringOptions clusteringOptions) {
        siloBuilder.AddMemoryGrainStorage(name);
        if (!clusteringOptions.TryGetConnectionStringGrainStorage(name, out var effectiveConnectionString)) {
            effectiveConnectionString = new EffectiveConnectionString(name, ModeInMemory, default, string.Empty, default);
        }
        var mode = effectiveConnectionString.GetMode(ModeInMemory);
        if (!string.Equals(mode, ModeInMemory)) {
            throw new InvalidOperationException("ConnectionString for GrainStorage is not for InMemory");
        }

        siloBuilder.AddMemoryGrainStorage(name, options => {
            if (effectiveConnectionString.NumStorageGrains.HasValue) {
                options.NumStorageGrains = effectiveConnectionString.NumStorageGrains.Value;
            }
        });
        return true;
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
    ClusteringOptions clusteringOptions,
    SiloBuilderClusteringHandlers? handlers = default) {
        handlers ??= Handlers;
        siloBuilder.Configure<EndpointOptions>(options => {

            bool forkEnabled = clusteringOptions.Fork.TryGetForked(out var forkIndex, out var forkCount);
            if (clusteringOptions.Endpoint.AdvertisedIPAddress is not null) {
                options.AdvertisedIPAddress = clusteringOptions.Endpoint.AdvertisedIPAddress;
            }
            if (clusteringOptions.Endpoint.GatewayPort.HasValue) {
                options.GatewayPort = clusteringOptions.Endpoint.GatewayPort.Value + forkIndex;
            } else {
                if (forkEnabled) {
                    options.GatewayPort = Orleans.Configuration.EndpointOptions.DEFAULT_GATEWAY_PORT + forkIndex;
                }
            }
            if (clusteringOptions.Endpoint.GatewayListeningEndpoint is not null) {
                options.GatewayListeningEndpoint = clusteringOptions.Endpoint.GatewayListeningEndpoint;
            } else if (options.AdvertisedIPAddress is null
                || options.AdvertisedIPAddress.Equals(IPAddress.Any)) {
                options.GatewayListeningEndpoint = new IPEndPoint(IPAddress.Any, options.GatewayPort);
            } else if (options.AdvertisedIPAddress is not null
                && options.AdvertisedIPAddress.Equals(IPAddress.IPv6Any)) {
                options.GatewayListeningEndpoint = new IPEndPoint(IPAddress.IPv6Any, options.GatewayPort);
            }

            if (clusteringOptions.Endpoint.SiloPort.HasValue) {
                options.SiloPort = clusteringOptions.Endpoint.SiloPort.Value + forkIndex;
            } else {
                if (forkEnabled) {
                    options.SiloPort = Orleans.Configuration.EndpointOptions.DEFAULT_SILO_PORT + forkIndex;
                }
            }
            if (clusteringOptions.Endpoint.SiloListeningEndpoint is not null) {
                options.SiloListeningEndpoint = clusteringOptions.Endpoint.SiloListeningEndpoint;
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
