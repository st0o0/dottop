using Akka.Actor;
using R3;
using Termina.Input;
using Termina.Reactive;

namespace dottop.Pages;

public class ServicesViewModel : ReactiveViewModel
{
    public ServicesViewModel(ActorSystem system) { }

    public override void OnActivated()
    {
        Input.OfType<IInputEvent, KeyPressed>()
            .Subscribe(key =>
            {
                switch (key.KeyInfo.Key)
                {
                    case ConsoleKey.D1: Navigate("/"); break;
                    case ConsoleKey.D2: Navigate("/performance"); break;
                    case ConsoleKey.D4: Navigate("/network"); break;
                    case ConsoleKey.D5: Navigate("/autostart"); break;
                    case ConsoleKey.Q or ConsoleKey.Escape: Shutdown(); break;
                }
            })
            .DisposeWith(Subscriptions);
    }
}
