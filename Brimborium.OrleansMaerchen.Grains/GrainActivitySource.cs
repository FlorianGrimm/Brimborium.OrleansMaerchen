using System.Diagnostics;

namespace Brimborium.OrleansMaerchen.Grains;

public class GrainActivitySource {
    public static readonly ActivitySource ActivitySource = new ActivitySource("Grain");
}