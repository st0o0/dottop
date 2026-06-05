using Akka.Actor;
using dottop.Models;
using Microsoft.Win32;

namespace dottop.Actors;

public sealed class StartupActor : ReceiveActor
{
    public static Props Props() => Akka.Actor.Props.Create<StartupActor>();

    private static readonly string[] RegistryPaths =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
    ];

    public StartupActor()
    {
        Receive<GetStartupEntries>(_ =>
        {
            try
            {
                var entries = new List<StartupEntry>();
                foreach (var path in RegistryPaths)
                {
                    using var key = Registry.CurrentUser.OpenSubKey(path);
                    if (key is null) continue;
                    foreach (var name in key.GetValueNames())
                    {
                        var value = key.GetValue(name)?.ToString() ?? "";
                        entries.Add(new StartupEntry(name, "", true, "Unbekannt", value));
                    }
                }
                Sender.Tell(entries);
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        Receive<SetStartupEnabled>(msg =>
        {
            try
            {
                Sender.Tell(new ActionSuccess($"{msg.Name} {(msg.Enabled ? "aktiviert" : "deaktiviert")}"));
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });
    }
}
