using R3;
using Termina.Input;
using Termina.Reactive;

namespace dtop.Plugin.Example;

public class ExampleViewModel : ReactiveViewModel
{
    public ReactiveProperty<int> Counter { get; } = new(0);

    public override void OnActivated()
    {
        Input.OfType<IInputEvent, KeyPressed>().Subscribe(HandleKey).DisposeWith(Subscriptions);
    }

    private void HandleKey(KeyPressed key)
    {
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.Enter:
                Counter.Value++;
                break;
            case ConsoleKey.D1: Navigate("/"); break;
            case ConsoleKey.D2: Navigate("/performance"); break;
            case ConsoleKey.D3: Navigate("/services"); break;
            case ConsoleKey.D4: Navigate("/network"); break;
            case ConsoleKey.D5: Navigate("/docker"); break;
            case ConsoleKey.Q: Shutdown(); break;
        }
    }

    public override void Dispose()
    {
        Counter.Dispose();
        base.Dispose();
    }
}
