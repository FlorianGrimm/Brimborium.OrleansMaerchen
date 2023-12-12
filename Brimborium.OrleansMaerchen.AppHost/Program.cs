internal class Program {
    private static void Main(string[] args) {
        var builder = DistributedApplication.CreateBuilder(args);
        builder.AddProject<Projects.Brimborium_OrleansMaerchen_WebApp>("WebApp");
        builder.Build().Run();
    }
}