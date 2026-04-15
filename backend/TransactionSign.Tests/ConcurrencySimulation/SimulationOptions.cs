using Microsoft.Extensions.Configuration;

namespace TransactionSign.Tests.ConcurrencySimulation;

public sealed class SimulationOptions
{
    public int MinConcurrentUsers { get; init; } = 20;
    public int MaxConcurrentUsers { get; init; } = 50;
    public int IterationsPerUser { get; init; } = 5;
    public int MinDelayMs { get; init; } = 10;
    public int MaxDelayMs { get; init; } = 100;

    public static SimulationOptions FromConfiguration(IConfiguration config)
    {
        var s = config.GetSection("Simulation");
        return new SimulationOptions
        {
            MinConcurrentUsers = Math.Clamp(int.TryParse(s["MinConcurrentUsers"], out var min) ? min : 20, 1, 100),
            MaxConcurrentUsers = Math.Clamp(int.TryParse(s["MaxConcurrentUsers"], out var max) ? max : 50, 1, 100),
            IterationsPerUser = Math.Max(1, int.TryParse(s["IterationsPerUser"], out var iter) ? iter : 5),
            MinDelayMs = Math.Max(0, int.TryParse(s["MinDelayMs"], out var minD) ? minD : 10),
            MaxDelayMs = Math.Max(0, int.TryParse(s["MaxDelayMs"], out var maxD) ? maxD : 100)
        };
    }
}
