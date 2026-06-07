using dottop.Core.Messages;

namespace dottop.Core.Platform;

public interface IProcessTreeProvider
{
    ProcessTreeResult BuildTree(int rootPid);
}
