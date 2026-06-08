namespace dtop.Core.Models;

public record NetworkSnapshot(
    string Name,
    ulong RxBytesPerSec,
    ulong TxBytesPerSec);
