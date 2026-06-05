namespace dottop.Models;

public record DiskSnapshot(
    string Name,
    ulong TotalBytes,
    ulong FreeBytes,
    ulong ReadBytesPerSec,
    ulong WriteBytesPerSec)
{
    public ulong UsedBytes => TotalBytes - FreeBytes;
    public double UsedPercent => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;
}
