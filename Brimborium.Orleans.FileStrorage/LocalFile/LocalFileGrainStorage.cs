namespace Orleans.Extensions.FileStrorage.OrleansFormat;

[global::Orleans.Providers.StorageProvider(ProviderName = "OrleansFormatFile")]
public sealed class LocalFileGrainStorage : IGrainStorage, ILifecycleParticipant<ISiloLifecycle> {
    private readonly string _StorageName;
    private readonly LocalFileGrainStorageOptions _Options;
    private readonly ClusterOptions _ClusterOptions;
    private string _RootDirectory;

    public LocalFileGrainStorage(
        string storageName,
        LocalFileGrainStorageOptions options,
        IOptions<ClusterOptions> clusterOptions) {
        this._StorageName = storageName;
        this._Options = options;
        this._ClusterOptions = clusterOptions.Value;
        this._RootDirectory = string.Empty;
    }

    // <clearstateasync>
    public Task ClearStateAsync<T>(
        string stateName,
        GrainId grainId,
        IGrainState<T> grainState) {
        var fullName = this.GetFileName(stateName, grainId);
        var fileInfo = new FileInfo(fullName);
        if (fileInfo.Exists) {
            if (fileInfo.LastWriteTimeUtc.ToString() != grainState.ETag) {
                throw new InconsistentStateException($"""
                    Version conflict (ClearState): ServiceId={this._ClusterOptions.ServiceId}
                    ProviderName={this._StorageName} GrainType={typeof(T)}
                    GrainReference={grainId}.
                    """);
            }

            grainState.ETag = null;
            // grainState.State = (T)Activator.CreateInstance(typeof(T))!;
            // grainState.State = default(T)!;
            grainState.RecordExists = false;
            fileInfo.Delete();
        }

        return Task.CompletedTask;
    }
    // </clearstateasync>
    // <readstateasync>
    public async Task ReadStateAsync<T>(
        string stateName,
        GrainId grainId,
        IGrainState<T> grainState) {
        var fullName = this.GetFileName(stateName, grainId);
        var fileInfo = new FileInfo(fullName);
        if (fileInfo is { Exists: false }) {
            // grainState.State = (T)Activator.CreateInstance(typeof(T))!;
            // grainState.State = default(T)!;
            grainState.RecordExists = false;
            return;
        }
        using var stream = fileInfo.Open(FileMode.Open);
        var binaryData = await BinaryData.FromStreamAsync(stream);
        grainState.State = this._Options.GrainStorageSerializer.Deserialize<T>(binaryData);
        grainState.ETag = fileInfo.LastWriteTimeUtc.ToString();
        grainState.RecordExists = true;
    }
    // </readstateasync>
    // <writestateasync>
    public async Task WriteStateAsync<T>(
        string stateName,
        GrainId grainId,
        IGrainState<T> grainState) {
        var fullName = this.GetFileName(stateName, grainId);
        var fileInfo = new FileInfo(fullName);
        if (fileInfo.Exists && fileInfo.LastWriteTimeUtc.ToString() != grainState.ETag) {
            throw new InconsistentStateException($"""
                Version conflict (WriteState): ServiceId={this._ClusterOptions.ServiceId}
                ProviderName={this._StorageName} GrainType={typeof(T)}
                GrainReference={grainId}.
                """);
        }
        var storedData = this._Options.GrainStorageSerializer.Serialize(grainState.State);
        using var stream = fileInfo.OpenWrite();
        await stream.WriteAsync(storedData.ToArray());
        await stream.FlushAsync();
        await stream.DisposeAsync();

        fileInfo.Refresh();
        grainState.ETag = fileInfo.LastWriteTimeUtc.ToString();
        grainState.RecordExists = true;
    }
    // </writestateasync>
    // <participate>
    public void Participate(ISiloLifecycle lifecycle) =>
        lifecycle.Subscribe(
            observerName: OptionFormattingUtilities.Name<LocalFileGrainStorage>(this._StorageName),
            stage: ServiceLifecycleStage.ApplicationServices,
            onStart: (ct) => {
                if (string.IsNullOrEmpty(this._Options.RootDirectory)) {
                    throw new OptionsValidationException("",
                        typeof(LocalFileGrainStorageOptions),
                        ["RootDirectory is empty."]
                        );
                }
                this._RootDirectory = System.IO.Path.Combine(this._Options.RootDirectory, this._StorageName);

                _ = Directory.CreateDirectory(this._RootDirectory);

                return Task.CompletedTask;
            });
    // </participate>
    // <getkeystring>
    private string GetFileName(string grainType, GrainId grainId) =>
        System.IO.Path.Combine(
            this._RootDirectory,
            $"{grainType}.{grainId.Key}.json");
    //$"{this._ClusterOptions.ServiceId}.{grainId.Key}.{grainType}.json";
    // </getkeystring>
}