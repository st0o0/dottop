namespace dottop.Platform;

public interface ICpuMetricsProvider : IDisposable
{
    (string Name, double TotalPercent, IReadOnlyList<double> CorePercents) GetSnapshot();
}
