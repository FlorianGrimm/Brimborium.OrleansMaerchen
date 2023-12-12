using Microsoft.Extensions.Hosting;

using Orleans.Serialization;

namespace Orleans.Extensions.FileStrorage.OrleansFormat;

/// <summary>
/// Provides default configuration for <see cref="IStorageProviderSerializerOptions.GrainStorageSerializer"/>.
/// </summary>
/// <typeparam name="TOptions">The options type.</typeparam>
public class LocalFileGrainStorageOptionsConfigurator<TOptions>
    : IPostConfigureOptions<TOptions>
    , IValidateOptions<TOptions>
    where TOptions : LocalFileGrainStorageOptions {
    private readonly IServiceProvider _ServiceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultStorageProviderSerializerOptionsConfigurator{TOptions}"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public LocalFileGrainStorageOptionsConfigurator(IServiceProvider serviceProvider) {
        this._ServiceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public void PostConfigure(string? name, TOptions options) {
        if (options.GrainStorageSerializer is null) {

            if (string.Equals(options.SerializerType, "default", StringComparison.OrdinalIgnoreCase)) {
                var grainStorageSerializer
                    = _ServiceProvider.GetKeyedService<IGrainStorageSerializer>(name);
                if (grainStorageSerializer is not null) {
                    options.GrainStorageSerializer = grainStorageSerializer;
                }
            } else if (string.Equals(options.SerializerType, "json", StringComparison.OrdinalIgnoreCase)) {
                var grainStorageSerializer
                    = this._ServiceProvider.GetRequiredService<JsonGrainStorageSerializer>();
                if (grainStorageSerializer is null) {
                    var orleansJsonSerializer = this._ServiceProvider.GetService<OrleansJsonSerializer>();
                    if (orleansJsonSerializer is null) {
                        IOptions<OrleansJsonSerializerOptions> orleansJsonSerializerOptions = this._ServiceProvider.GetRequiredService<IOptions<OrleansJsonSerializerOptions>>();
                        orleansJsonSerializer = new OrleansJsonSerializer(orleansJsonSerializerOptions);
                    }
                    grainStorageSerializer = new JsonGrainStorageSerializer(orleansJsonSerializer);
                }
                if (grainStorageSerializer is not null) {
                    options.GrainStorageSerializer = grainStorageSerializer;
                }
            } else if (string.Equals(options.SerializerType, "orleans", StringComparison.OrdinalIgnoreCase)) {
                var grainStorageSerializer
                    = this._ServiceProvider.GetService<OrleansGrainStorageSerializer>();

                if (grainStorageSerializer is not null) {
                    options.GrainStorageSerializer = grainStorageSerializer;
                }
            }

        }
        if (options.GrainStorageSerializer is null) {
            // First, try to get a IGrainStorageSerializer that was registered with 
            // the same name as the storage provider
            // If none is found, fallback to system wide default
            options.GrainStorageSerializer
                = _ServiceProvider.GetKeyedService<IGrainStorageSerializer>(name)
                ?? _ServiceProvider.GetRequiredService<IGrainStorageSerializer>();
        }
        if (string.IsNullOrEmpty(options.RootDirectory)) {
            var hostEnvironment = this._ServiceProvider.GetService<Microsoft.Extensions.Hosting.IHostEnvironment>();
            if (hostEnvironment is not null) { 
                options.RootDirectory = Path.Combine(hostEnvironment.ContentRootPath, "Data");
            }
        }        
    }

    public ValidateOptionsResult Validate(string? name, TOptions options) {
        if (string.IsNullOrEmpty(options.RootDirectory)) {
            return ValidateOptionsResult.Fail($"{nameof(options.RootDirectory)} is empty");
        } else { 
            return ValidateOptionsResult.Success;
        }
    }
}