namespace dtop.Plugin;

public interface ITickSource
{
    TimeSpan CurrentInterval { get; }
    IDisposable Subscribe(Action onTick);
}
