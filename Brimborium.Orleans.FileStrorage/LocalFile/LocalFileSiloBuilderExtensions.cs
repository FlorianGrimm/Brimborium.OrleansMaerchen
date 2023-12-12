#pragma warning disable IDE0058 // Expression value is never used

namespace Orleans.Hosting;

public static class LocalFileSiloBuilderExtensions {
    public static ISiloBuilder AddLocalFileGrainStorage(
        this ISiloBuilder builder,
        string providerName,
        Action<LocalFileGrainStorageOptions> options) =>
        builder.ConfigureServices(
            services => services.AddOrleansFormatFileGrainStorage(
                providerName, options));

    public static IServiceCollection AddOrleansFormatFileGrainStorage(
        this IServiceCollection services,
        string providerName,
        Action<LocalFileGrainStorageOptions> options) {
        services.AddOptions<LocalFileGrainStorageOptions>(providerName)
            .Configure(options)
            ;

        services.AddTransient<
            IPostConfigureOptions<LocalFileGrainStorageOptions>,
            LocalFileGrainStorageOptionsConfigurator<LocalFileGrainStorageOptions>>();

        services.AddTransient<
            IPostConfigureOptions<LocalFileGrainStorageOptions>,
            DefaultStorageProviderSerializerOptionsConfigurator<LocalFileGrainStorageOptions>>();
        services.AddTransient<
            IValidateOptions<LocalFileGrainStorageOptions>,
            LocalFileGrainStorageOptionsConfigurator<LocalFileGrainStorageOptions>>();
        services.AddGrainStorage<LocalFileGrainStorage>(providerName, LocalFileGrainStorageFactory.Create);

        //services.AddKeyedSingleton<OrleansFormatFileGrainStorage>(
        //    providerName,
        //    (serviceProvider, name) => OrleansFormatFileGrainStorageFactory.Create(serviceProvider, name));

        //services.AddKeyedSingleton<IGrainStorage>(
        //    providerName,
        //    (serviceProvider, name)=>serviceProvider.GetRequiredKeyedService<OrleansFormatFileGrainStorage>(name);

        //services.AddKeyedSingleton<ILifecycleParticipant<ISiloLifecycle>>(
        //    providerName, (serviceProvider, name) =>
        //    serviceProvider.GetRequiredKeyedService<OrleansFormatFileGrainStorage>(name)
        //);

        return services;
    }
}