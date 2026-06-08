using System.Diagnostics;
using dtop.Core.Models;

namespace dtop.Core.Platform;

public interface IProcessClassifier
{
    ProcessGroup Classify(Process process);
}
