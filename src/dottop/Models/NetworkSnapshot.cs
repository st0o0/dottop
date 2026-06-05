namespace dottop.Models;

public record NetworkSnapshot(
    string Name,
    ulong RxBytesPerSec,
    ulong TxBytesPerSec);
