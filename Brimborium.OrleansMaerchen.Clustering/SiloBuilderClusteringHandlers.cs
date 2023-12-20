namespace Orleans.Hosting;

/// <summary>
/// Maps mode to handler.
/// So you can add a new mode by adding a handler.
/// </summary>
public sealed class SiloBuilderClusteringHandlers {

    public static SiloBuilderClusteringHandlers Create() {
        SiloBuilderClusteringHandlers result = new();

        result.UseClustering.Add(
            SiloBuilderClusteringExtensions.ModeAdoNet,
            SiloBuilderClusteringExtensions.UseAdoNetClusteringByConfiguration);

        result.UseClustering.Add(
            SiloBuilderClusteringExtensions.ModeLocalhost,
            SiloBuilderClusteringExtensions.UseLocalhostClusteringByConfiguration);

        result.UseReminder.Add(
            SiloBuilderClusteringExtensions.ModeAdoNet,
            SiloBuilderClusteringExtensions.UseAdoNetReminderByConfiguration);

        result.UseReminder.Add(
            SiloBuilderClusteringExtensions.ModeInMemory,
            SiloBuilderClusteringExtensions.UseInMemoryReminderReminderByConfiguration);

        result.UseDefaultGrainStorage.Add(
            SiloBuilderClusteringExtensions.ModeAdoNet,
            SiloBuilderClusteringExtensions.AddDefaultAdoNetGrainStorageByConfiguration);

        result.UseDefaultGrainStorage.Add(
            SiloBuilderClusteringExtensions.ModeInMemory,
            SiloBuilderClusteringExtensions.AddDefaultMemoryGrainStorageByConfiguration);

        result.UseNamedGrainStorage.Add(
            SiloBuilderClusteringExtensions.ModeAdoNet,
            SiloBuilderClusteringExtensions.AddNamedAdoNetGrainStorageByConfiguration);

        result.UseNamedGrainStorage.Add(
            SiloBuilderClusteringExtensions.ModeInMemory,
            SiloBuilderClusteringExtensions.AddNamedMemoryGrainStorageByConfiguration);
        return result;
    }

    public Dictionary<
        /* Mode */ string,
        Func<ISiloBuilder, ClusteringOptions, bool>
        > UseClustering = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<
        /* Mode */ string,
        Func<ISiloBuilder, ClusteringOptions, bool>
        > UseReminder = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<
        /* Mode */ string,
        Func<ISiloBuilder, ClusteringOptions, bool>
        > UseDefaultGrainStorage = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<
        /* Mode */ string,
        Func<ISiloBuilder, /* Name */ string, ClusteringOptions, bool>
        > UseNamedGrainStorage = new(StringComparer.OrdinalIgnoreCase);

    /*
     UseAdoNetReminderByConfiguration(
        this ISiloBuilder siloBuilder,
        ClusteringOptions clusteringOptions
     */
}