namespace Orleans.Extensions.FileStrorage.OrleansFormat;

public class LocalFileGrainStorageOptions : IStorageProviderSerializerOptions {
    public required string RootDirectory { get; set; }

    public required string SerializerType { get; set; } = "default";

    public required IGrainStorageSerializer GrainStorageSerializer { get; set; }
}
