using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace dottop.Core.Messages;

public sealed record ActionSuccess(string Message);

public sealed record ActionFailure(string Error);

public sealed record MonitoringStream<T>(
    IAsyncEnumerable<T> Data,
    CancellationTokenSource Cancellation);

public static class ChannelHelper
{
    public static async IAsyncEnumerable<T> ReadFromChannelAsync<T>(
        ChannelReader<T> reader,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in reader.ReadAllAsync(ct).ConfigureAwait(false))
            yield return item;
    }
}