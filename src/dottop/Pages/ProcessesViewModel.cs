using Akka.Actor;
using Akka.Hosting;
using R3;
using dottop.Actors;
using Termina.Input;
using Termina.Reactive;

namespace dottop.Pages;

public class ProcessesViewModel : ReactiveViewModel
{
    public ProcessesViewModel(ActorSystem system, IRequiredActor<ProcessActionActor> processAction) { }

    public override void OnActivated()
    {
        Input.OfType<IInputEvent, KeyPressed>()
            .Subscribe(key =>
            {
                switch (key.KeyInfo.Key)
                {
                    case ConsoleKey.D2: Navigate("/performance"); break;
                    case ConsoleKey.D3: Navigate("/services"); break;
                    case ConsoleKey.D4: Navigate("/network"); break;
                    case ConsoleKey.D5: Navigate("/autostart"); break;
                    case ConsoleKey.Q or ConsoleKey.Escape: Shutdown(); break;
                }
            })
            .DisposeWith(Subscriptions);
    }
}
