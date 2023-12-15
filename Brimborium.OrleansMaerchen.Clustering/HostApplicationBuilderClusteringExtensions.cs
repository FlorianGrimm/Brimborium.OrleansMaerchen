namespace Microsoft.Extensions.Hosting;

public static class HostApplicationBuilderClusteringExtensions {
    public const int ForkCountMax = 10;

    public static async Task<ClusterForkFeedback> HandleDevelopmentFork(
       this IHostApplicationBuilder builder,
       Assembly assembly,
       string? sectionName = default
       ) {
        var appConfig = ClusteringOptions.Empty();
        sectionName ??= ClusteringOptions.DefaultSectionName;
        IConfiguration configuration
          = (string.Equals(sectionName, string.Empty, StringComparison.Ordinal))
          ? builder.Configuration
          : builder.Configuration.GetSection(sectionName);
        configuration.Bind(appConfig);
        if (appConfig.Fork.Enable) {
            var forkCount = appConfig.Fork.ForkCount;
            if (forkCount < 1) {
                System.Console.Error.WriteLine($"forkCount {forkCount} < 1 - terminate -");
                return ClusterForkFeedback.Exit(1);
            } else if (forkCount > ForkCountMax) {
                System.Console.Error.WriteLine($"forkCount {forkCount} > {ForkCountMax} - terminate -");
                return ClusterForkFeedback.Exit(1);
            }
            string prefixConfiguration
                = (string.IsNullOrEmpty(sectionName))
                ? "--Fork:"
                : $"--{sectionName}:Fork:";

            var forkIndex = appConfig.Fork.ForkIndex;
            if (forkIndex < 0) {
                return ClusterForkFeedback.Exit(1);
            } else if (forkIndex == 0) {
                var locationDll = assembly.Location;
                if (!string.Equals(".dll", System.IO.Path.GetExtension(locationDll), StringComparison.OrdinalIgnoreCase)) {
                    System.Console.Error.WriteLine($"assembly is not an dll - terminate -");
                    return ClusterForkFeedback.Exit(1);
                }
                var locationExe = System.IO.Path.ChangeExtension(locationDll, ".exe");
                List<Process> processList = new List<Process>();
                System.Console.Out.WriteLine($"Forking {forkCount} times. Press enter to continue.");
                System.Console.ReadLine();
                for (forkIndex = 1; forkIndex <= forkCount; forkIndex++) {
                    System.Diagnostics.ProcessStartInfo processStartInfo = new(
                        locationExe
                        ) {
                        ArgumentList = {
                            $"{prefixConfiguration}{nameof(ClusterForkOptions.Enable)}=true",
                            $"{prefixConfiguration}{nameof(ClusterForkOptions.ForkIndex)}={forkIndex}",
                            $"{prefixConfiguration}{nameof(ClusterForkOptions.ForkCount)}={forkCount}"
                        },
                        WindowStyle = ProcessWindowStyle.Normal,
                        CreateNoWindow = false,
                        UseShellExecute = true
                    };
                    var p = System.Diagnostics.Process.Start(processStartInfo);
                    if (p is not null) {
                        processList.Add(p);
                    }
                    await Task.Delay(1000);
                }
                System.Console.CancelKeyPress += (sender, e) => {
                    foreach (var p in processList) {
                        p.Kill();
                    }
                };
                foreach (var p in processList) {
                    if (p.HasExited) {
                        System.Console.Out.WriteLine($"{p.Id} has exited.");
                    } else {
                        System.Console.Out.WriteLine($"{p.Id} started.");
                    }
                }
                foreach (var p in processList) {
                    if (p.HasExited) {
                        System.Console.Out.WriteLine($"{p.Id} has exited.");
                    } else {
                        await p.WaitForExitAsync();
                        System.Console.Out.WriteLine($"{p.Id} exited.");
                    }
                }
                System.Console.WriteLine("-fini-");
                System.Environment.Exit(0);
                return ClusterForkFeedback.Exit(0);
            } else if (forkIndex <= ForkCountMax) {
                return ClusterForkFeedback.Forked(forkIndex, forkCount);
            } else {
                return ClusterForkFeedback.Exit(1);
            }
        }
        //
        return ClusterForkFeedback.Exit(-1);
    }
}
