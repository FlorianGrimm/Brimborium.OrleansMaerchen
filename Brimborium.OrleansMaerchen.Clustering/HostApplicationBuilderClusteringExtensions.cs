namespace Microsoft.Extensions.Hosting;

public static class HostApplicationBuilderClusteringExtensions {
    public const int ForkCountMax = 10;

    /// <summary>
    /// fork
    /// ==
    /// --Orleans:Fork:Enable=true --Orleans:Fork:ForkIndex=0
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="assembly"></param>
    /// <param name="sectionName"></param>
    /// <returns></returns>
    public static async Task<ClusteringConfigurationResult> PrepareClusteringConfiguration(
        this IHostApplicationBuilder builder,
        Assembly assembly,
        string[]? args = default,
        string? sectionName = default
        ) {
        args ??= Array.Empty<string>();
#if false
        if (args is not null) {
            System.Console.Out.WriteLine($"Args:#{string.Join("#", args)}#");
        }
#endif
        var clusteringOptions = ClusteringOptions.Empty();
        sectionName ??= ClusteringOptions.DefaultSectionName;
        {
            IConfiguration configuration
              = (string.Equals(sectionName, string.Empty, StringComparison.Ordinal))
              ? builder.Configuration
              : builder.Configuration.GetSection(sectionName);
            configuration.Bind(clusteringOptions);
        }
        
        {
            if (0 < args.Length
                && string.Equals("fork", args[0], StringComparison.OrdinalIgnoreCase)) {
                System.Console.Out.WriteLine("fork found");
                clusteringOptions.Fork.Enable = true;
                clusteringOptions.Fork.ForkIndex = 0;
                //args = args.Where(arg => !string.Equals("fork", arg, StringComparison.OrdinalIgnoreCase)).ToArray();
                args = args[1..];
            }

            if (2 < args.Length
                && string.Equals("forked", args[0], StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[1], out var forkIndex)
                && int.TryParse(args[2], out var forkCount)
                ) {
                System.Console.Out.WriteLine($"forked {forkIndex} found");
                clusteringOptions.Fork.Enable = true;
                clusteringOptions.Fork.ForkIndex = forkIndex;
                clusteringOptions.Fork.ForkCount = forkCount;
                args = args[3..];
            }
        }

        if (clusteringOptions.Fork.Enable) {
            var forkCount = clusteringOptions.Fork.ForkCount;
            if (forkCount < 1) {
                System.Console.Error.WriteLine($"forkCount {forkCount} < 1 - terminate -");
                return ClusteringConfigurationResult.Exit(1);
            } else if (forkCount > ForkCountMax) {
                System.Console.Error.WriteLine($"forkCount {forkCount} > {ForkCountMax} - terminate -");
                return ClusteringConfigurationResult.Exit(1);
            }
            /*
            string prefixConfiguration
                = (string.IsNullOrEmpty(sectionName))
                ? "--Fork:"
                : $"--{sectionName}:Fork:";
            */
            var forkIndex = clusteringOptions.Fork.ForkIndex;
            if (forkIndex < 0) {
                return ClusteringConfigurationResult.Exit(1);
            } else if (forkIndex == 0) {
                var locationDll = assembly.Location;
                if (!string.Equals(".dll", System.IO.Path.GetExtension(locationDll), StringComparison.OrdinalIgnoreCase)) {
                    System.Console.Error.WriteLine($"assembly is not an dll - terminate -");
                    return ClusteringConfigurationResult.Exit(1);
                }
                var locationExe = System.IO.Path.ChangeExtension(locationDll, ".exe");
                List<Process> processList = new ();
                /*
                System.Console.Out.WriteLine($"Forking {forkCount} times. Press enter to continue.");
                System.Console.ReadLine();
                */
                for (forkIndex = 1; forkIndex <= forkCount; forkIndex++) {
                    System.Diagnostics.ProcessStartInfo processStartInfo = new(
                        locationExe
                        ) {
                        ArgumentList = {
                            /*
                            $"{prefixConfiguration}{nameof(ClusterForkOptions.Enable)}=true",
                            $"{prefixConfiguration}{nameof(ClusterForkOptions.ForkIndex)}={forkIndex}",
                            $"{prefixConfiguration}{nameof(ClusterForkOptions.ForkCount)}={forkCount}",
                            */
                            "forked",
                            $"{forkIndex}",
                            $"{forkCount}",
                        },
                        WindowStyle = ProcessWindowStyle.Normal,
                        CreateNoWindow = false,
                        UseShellExecute = true
                    };
                    // passthrough args
                    foreach (var arg in args) {
                        processStartInfo.ArgumentList.Add(arg);
                    }
                    var p = System.Diagnostics.Process.Start(processStartInfo);
                    if (p is not null) {
                        processList.Add(p);
                        System.Console.Out.WriteLine($"PID: {p.Id} started.");
                    }
                    await Task.Delay(200);
                }
                System.Console.CancelKeyPress += (sender, e) => {
                    foreach (var p in processList) {
                        p.Kill();
                    }
                };
                foreach (var p in processList) {
                    if (p.HasExited) {
                        System.Console.Out.WriteLine($"PID: {p.Id} has exited.");
                    } else {
                        System.Console.Out.WriteLine($"PID: {p.Id} is running.");
                    }
                }
                System.Console.Out.WriteLine("Running...");
                foreach (var p in processList) {
                    if (p.HasExited) {
                        System.Console.Out.WriteLine($"PID: {p.Id} has exited.");
                    } else {
                        await p.WaitForExitAsync();
                        System.Console.Out.WriteLine($"PID: {p.Id} exited.");
                    }
                }
                System.Console.WriteLine("-fini-");
                System.Environment.Exit(0);
                return ClusteringConfigurationResult.Exit(0);
            } else if (forkIndex <= ForkCountMax) {
                return ClusteringConfigurationResult.Forked(forkIndex, forkCount);
            } else {
                return ClusteringConfigurationResult.Exit(1);
            }
        }
        //
        return ClusteringConfigurationResult.Exit(-1);
    }
}
