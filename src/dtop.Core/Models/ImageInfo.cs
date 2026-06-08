namespace dtop.Core.Models;

public record ImageInfo(
    string Id, string Repository, string Tag,
    long SizeBytes, DateTimeOffset Created,
    string OsArch, int ContainerCount);
