using System.Diagnostics;
using dtop.Core.Models;
using dtop.Core.Platform;

namespace dtop.Mac;

public sealed class MacProcessClassifier : IProcessClassifier
{
    public ProcessGroup Classify(Process process) => ProcessGroup.Apps;
}
