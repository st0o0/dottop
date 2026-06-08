namespace dtop.Core.Models;

public record VolumeInfo(
    string Name, string Driver, string Mountpoint,
    DateTimeOffset Created, long SizeBytes, int MountCount,
    IReadOnlyDictionary<string, string> Labels);
