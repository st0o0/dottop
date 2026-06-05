using Akka.Actor;
using dottop.Models;
using Microsoft.Win32;

namespace dottop.Actors;

public sealed class StartupActor : ReceiveActor
{
    public static Props Props() => Akka.Actor.Props.Create<StartupActor>();

    private const string RunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public StartupActor()
    {
        Receive<GetStartupEntries>(_ =>
        {
            try
            {
                var entries = new List<StartupEntry>();
                var disabled = GetDisabledEntries();

                using var key = Registry.CurrentUser.OpenSubKey(RunPath);
                if (key is not null)
                {
                    foreach (var name in key.GetValueNames())
                    {
                        var value = key.GetValue(name)?.ToString() ?? "";
                        var enabled = !disabled.Contains(name);
                        entries.Add(new StartupEntry(name, "", enabled, "", value));
                    }
                }

                Sender.Tell(entries.OrderBy(e => e.Name).ToList());
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        Receive<SetStartupEnabled>(msg =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(ApprovedPath, writable: true);
                if (key is null)
                {
                    Sender.Tell(new ActionFailure("StartupApproved Registry-Key nicht gefunden"));
                    return;
                }

                if (msg.Enabled)
                {
                    var enabled = new byte[] { 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                    key.SetValue(msg.Name, enabled, RegistryValueKind.Binary);
                }
                else
                {
                    var disabled = new byte[] { 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                    key.SetValue(msg.Name, disabled, RegistryValueKind.Binary);
                }

                Sender.Tell(new ActionSuccess($"{msg.Name} {(msg.Enabled ? "aktiviert" : "deaktiviert")}"));
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });
    }

    private static HashSet<string> GetDisabledEntries()
    {
        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ApprovedPath);
            if (key is null) return disabled;

            foreach (var name in key.GetValueNames())
            {
                if (key.GetValue(name) is byte[] data && data.Length >= 1 && (data[0] & 0x01) != 0)
                    disabled.Add(name);
            }
        }
        catch { }
        return disabled;
    }
}
