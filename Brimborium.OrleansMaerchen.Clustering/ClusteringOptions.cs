using System.Net;

namespace Orleans.Extension.Clustering;

public class ClusteringOptions {
    public const string DefaultSectionName = "Orleans";

    public const string ClusterModeAdoNetClustering = "AdoNet";

    public const string ClusterModeLocalhostClustering = "Localhost";

    public ClusterForkOptions Fork { get; set; } = new();

    public ClusterEndpointOptions Endpoint { get; set; } = new();

    /// <summary>
    /// Gets or sets the cluster identity
    /// </summary>
    public string? ClusterId { get; set; }

    /// <summary>
    /// Gets or sets a unique identifier for this service, which should survive deployment and redeployment, where as <see cref="ClusterId"/> might not.
    /// </summary>
    public string? ServiceId { get; set; }

    public bool TryGetNamedConnectionString(
        string name,
        [MaybeNullWhen(false)] out EffectiveConnectionString connectionString
        ) {
        if (this.NamedConnectionString.TryGetValue(name, out var ccs)
            && ccs is not null
            && ccs.TryGetConnectionString(name, out connectionString)) {
            return true;
        }
        connectionString = default;
        return false;
    }
    public bool TryGetConnectionStringClustering(
        [MaybeNullWhen(false)] out EffectiveConnectionString connectionString
        ) {
        if (this.TryGetNamedConnectionString("Clustering", out connectionString)) { return true; }
        if (this.TryGetNamedConnectionString("Default", out connectionString)) { return true; }
        connectionString = default;
        return false;
    }

    public bool TryGetConnectionStringReminder(
        [MaybeNullWhen(false)] out EffectiveConnectionString connectionString) {
        if (this.TryGetNamedConnectionString("Reminder", out connectionString)) { return true; }
        connectionString = default;
        return false;
    }
    public Dictionary<string, CommonConnectionString> NamedConnectionString { get; set; } = new Dictionary<string, CommonConnectionString>(StringComparer.OrdinalIgnoreCase);

    public bool TryGetConnectionStringGrainStorage(
        string nameGraingStorage,
        [MaybeNullWhen(false)] out EffectiveConnectionString connectionString) {
        if (this.TryGetNamedConnectionString(nameGraingStorage, out connectionString)) { return true; }
        if (this.TryGetNamedConnectionString("GrainStorage", out connectionString)) { return true; }
        if (this.TryGetNamedConnectionString("Default", out connectionString)) { return true; }
        connectionString = default;
        return false;
    }

    public bool TryGetConnectionStringDefaultGrainStorage(
        [MaybeNullWhen(false)] out EffectiveConnectionString connectionString) {
        if (this.TryGetNamedConnectionString("GrainStorage", out connectionString)) { return true; }
        if (this.TryGetNamedConnectionString("Default", out connectionString)) { return true; }
        connectionString = default;
        return false;
    }

    public static ClusteringOptions Empty()
        => new();
}


public sealed class ClusterForkOptions {
    public bool Enable { get; set; }
    public int ForkIndex { get; set; } = -1;
    public int ForkCount { get; set; }

    public bool TryGetForked(out int forkIndex, out int forkCount) {
        if (this.Enable && this.ForkIndex != 0) {
            if (0 < this.ForkIndex && this.ForkIndex <= this.ForkCount
                && 0 < this.ForkCount
                ) {
                forkIndex = this.ForkIndex - 1;
                forkCount = this.ForkCount;
                return true;
            }
        }
        forkIndex = 0;
        forkCount = 0;
        return false;
    }
}

public class ClusterEndpointOptions {
    /// <summary>
    /// The IP address used for clustering.
    /// </summary>
    public IPAddress? AdvertisedIPAddress { get; set; }

    /// <summary>
    /// Gets or sets the port this silo uses for silo-to-silo communication.  = 11111
    /// </summary>
    public int? SiloPort { get; set; }

    /// <summary>
    /// Gets or sets the endpoint used to listen for silo to silo communication. 
    /// If not set will default to <see cref="AdvertisedIPAddress"/> + <see cref="SiloPort"/>
    /// </summary>
    public IPEndPoint? SiloListeningEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the port this silo uses for silo-to-client (gateway) communication. Specify 0 to disable gateway functionality.  = 30000
    /// </summary>
    public int? GatewayPort { get; set; }

    /// <summary>
    /// Gets or sets the endpoint used to listen for client to silo communication. 
    /// If not set will default to <see cref="AdvertisedIPAddress"/> + <see cref="GatewayPort"/>
    /// </summary>
    public IPEndPoint? GatewayListeningEndpoint { get; set; }
}

public class CommonConnectionString {
    public string? Mode { get; set; }

    // AdoNet
    public string? Invariant { get; set; }
    public string? ConnectionString { get; set; }
    public string? Server { get; set; }
    public string? Database { get; set; }

    // InMemory
    public int? NumStorageGrains { get; set; }

    public bool TryGetConnectionString(string name, [MaybeNullWhen(false)] out EffectiveConnectionString connectionString) {
        if (string.Equals(this.Mode, SiloBuilderClusteringExtensions.ModeAdoNet, StringComparison.OrdinalIgnoreCase)) {
            return this.TryGetConnectionStringAdoNet(name, out connectionString);
        } else if (string.Equals(this.Mode, SiloBuilderClusteringExtensions.ModeInMemory, StringComparison.OrdinalIgnoreCase)) {
            return this.TryGetConnectionStringInMemory(name, out connectionString);
        } else {
            return this.TryGetConnectionStringDefault(name, out connectionString);
        }
    }

    public bool TryGetConnectionStringAdoNet(string name, [MaybeNullWhen(false)] out EffectiveConnectionString connectionString) {
        if (!string.IsNullOrEmpty(this.ConnectionString)) {
            connectionString = new EffectiveConnectionString(
                    name,
                    this.Mode,
                    this.Invariant,
                    this.ConnectionString,
                    this.NumStorageGrains);
            return true;
        } else if (!string.IsNullOrEmpty(this.Server) && !string.IsNullOrEmpty(this.Database)) {
            connectionString = new EffectiveConnectionString(
                    name,
                    this.Mode,
                    this.Invariant,
                    $"Data Source={this.Server};Initial Catalog={this.Database};Integrated Security=True;Pooling=False;Max Pool Size=200;MultipleActiveResultSets=True;TrustServerCertificate=True",
                    // Asynchronous Processing
                    this.NumStorageGrains
                );
            return true;
        } else {
            connectionString = default;
            return false;
        }
    }

    public bool TryGetConnectionStringInMemory(string name, [MaybeNullWhen(false)] out EffectiveConnectionString connectionString) {
        connectionString = new EffectiveConnectionString(
                    name,
                    this.Mode,
                    this.Invariant,
                    this.ConnectionString ?? string.Empty,
                    this.NumStorageGrains
                );
        return true;
    }

    public bool TryGetConnectionStringDefault(string name, [MaybeNullWhen(false)] out EffectiveConnectionString connectionString) {
        connectionString = new EffectiveConnectionString(
                    name,
                    this.Mode,
                    this.Invariant,
                    this.ConnectionString ?? string.Empty,
                    this.NumStorageGrains
                );
        return true;
    }

}

public record class EffectiveConnectionString(
    string? Name,
    string? Mode,
    string? Invariant,
    string ConnectionString,
    int? NumStorageGrains
    ) {
    public string? GetMode(string? defaultValue) {
        if (!string.IsNullOrEmpty(this.Mode)) {
            return this.Mode;
        } else {
            return defaultValue;
        }
    }

    public string? GetInvariant(string? defaultValue) {
        if (!string.IsNullOrEmpty(this.Invariant)) {
            return this.Invariant;
        } else {
            return defaultValue;
        }
    }
}
//