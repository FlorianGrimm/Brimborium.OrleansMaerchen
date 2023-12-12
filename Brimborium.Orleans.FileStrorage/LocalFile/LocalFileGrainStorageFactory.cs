namespace Orleans.Extensions.FileStrorage.OrleansFormat;

public static class LocalFileGrainStorageFactory {
    public static LocalFileGrainStorage Create(
        IServiceProvider services, string name
        ) {
        var optionsMonitor =
            services.GetRequiredService<IOptionsMonitor<LocalFileGrainStorageOptions>>();
        
        return ActivatorUtilities.CreateInstance<LocalFileGrainStorage>(
            services,
            name,
            optionsMonitor.Get(name),
            services.GetProviderClusterOptions(name));
    }
}