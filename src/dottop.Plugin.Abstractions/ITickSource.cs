namespace dottop.Plugin.Abstractions;

public interface ITickSource
{
    TimeSpan CurrentInterval { get; }
    IDisposable Subscribe(Action onTick);
}
