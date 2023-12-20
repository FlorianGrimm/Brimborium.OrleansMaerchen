namespace Orleans.Extension.Clustering;

public record ClusteringConfigurationResult(
    int ExitCode,
    int ForkIndex,
    int ForkCount
    ) {
    public static ClusteringConfigurationResult Empty()
        => new(-1, 0, 0);

    public static ClusteringConfigurationResult Exit(int exitCode)
        => new(exitCode, 0, 0);

    public bool ShouldExit(out int exitCode) {
        if (0 <= this.ExitCode) {
            exitCode = this.ExitCode;
            return true;
        } else {
            exitCode = 0;
            return false;
        }
    }
    public static ClusteringConfigurationResult Forked(int forkIndex, int forkCount)
        => new(-1, forkIndex, forkCount);

    public bool TryForked(out int forkIndex, out int forkCount) {
        if (-1 == this.ExitCode && 0 < this.ForkCount) {
            forkIndex = this.ForkIndex;
            forkCount = this.ForkCount;
            return true;
        } else {
            forkIndex = 0;
            forkCount = 0;
            return false;
        }
    }
}
