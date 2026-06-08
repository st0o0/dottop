using dtop.Core.Messages;

namespace dtop.Core.Platform;

public interface IProcessTreeProvider
{
    ProcessTreeResult BuildTree(int rootPid);
}
