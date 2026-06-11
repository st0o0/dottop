using dtop.Core.Models;

namespace dtop.Services;

/// <summary>Actors publish snapshots here. Implemented by MetricStore.</summary>
public interface IMetricSink
{
    void Publish(CpuSnapshot snapshot);
    void Publish(MemorySnapshot snapshot);
    void Publish(GpuSnapshot snapshot);
    void Publish(List<DiskSnapshot> snapshots);
    void Publish(List<NetworkSnapshot> snapshots);
    void Publish(List<ProcessSnapshot> snapshots);
    void Publish(List<ConnectionSnapshot> snapshots);
    void Publish(List<ContainerSnapshot> snapshots);
}
