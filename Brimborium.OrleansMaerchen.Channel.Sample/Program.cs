namespace Brimborium.OrleansMaerchen.Channel.Sample;

public class Program {
    public static async Task<int> Main(string[] args) {
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings() {
                Args = args
            });
        builder.Configuration.AddUserSecrets<Program>();
#if DEBUG
        var clusteringConfigurationResult = await builder.PrepareClusteringConfiguration(
            typeof(Program).Assembly,
            args);
        if (clusteringConfigurationResult.ShouldExit(out var exitCode)) {
            System.Console.Out.WriteLine("main fork terminates.");
            return exitCode;
        } else if (clusteringConfigurationResult.TryForked(out var forkIndex, out var forkCount)) {
            System.Console.Out.WriteLine($"Forked starts {forkIndex} / {forkCount}.");
            // System.Diagnostics.Debugger.Launch();
        } else {
            System.Console.Out.WriteLine("Non Forked starts.");
        }
#endif
        builder.Services.AddOptions<ProgramOptions>().BindConfiguration("");
        builder.UseOrleans((ISiloBuilder siloBuilder) => {
            var clusteringOptions = siloBuilder.UseOrleansByConfiguration(
                builder.Configuration
                );
            siloBuilder.AddNamedGrainStorageByConfiguration("name", clusteringOptions);
        });
        var app = builder.Build();
        await app.StartAsync();
        await Task.Delay(20000);
        await app.StopAsync();
        System.Console.Out.WriteLine("-fini-");
        return 0;
    }

}

public class ProgramOptions {
}