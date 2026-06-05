using Akka.Actor;
using R3;
using Termina.Input;
using Termina.Reactive;

namespace dottop.Pages;

public class PerformanceViewModel : ReactiveViewModel
{
    public PerformanceViewModel(ActorSystem system) { }

    public override void OnActivated()
    {
        Input.OfType<IInputEvent, KeyPressed>()
            .Subscribe(key =>
            {
                switch (key.KeyInfo.Key)
                {
                    case ConsoleKey.D1: Navigate("/"); break;
                    case ConsoleKey.D3: Navigate("/services"); break;
                    case ConsoleKey.D4: Navigate("/network"); break;
                    case ConsoleKey.D5: Navigate("/autostart"); break;
                    case ConsoleKey.Q or ConsoleKey.Escape: Shutdown(); break;
                }
            })
            .DisposeWith(Subscriptions);
    }
}
