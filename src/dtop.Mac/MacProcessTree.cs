using dtop.Core.Messages;
using dtop.Core.Platform;

namespace dtop.Mac;

public sealed class MacProcessTree : IProcessTreeProvider
{
    public ProcessTreeResult BuildTree(int rootPid)
        => new(rootPid, "unknown", []);
}
