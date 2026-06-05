public record DiskInfo(string Name, ulong Total, ulong Free)
{
    public ulong Used => Total - Free;
    public double UsedPercent => Total > 0 ? (double)Used / Total * 100 : 0;
}