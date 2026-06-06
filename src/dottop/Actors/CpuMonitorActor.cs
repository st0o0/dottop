using System.Runtime.InteropServices;
using System.Threading.Channels;
using Akka.Actor;
using dottop.Models;
using Microsoft.Win32;

namespace dottop.Actors;

public sealed class CpuMonitorActor : ReceiveActor
{
    private Channel<CpuSnapshot>? _channel;
    private ICancelable? _tickSchedule;
    private CancellationTokenSource? _streamCts;
    private long _prevIdle;
    private long _prevTotal;
    private string? _cpuName;

    public static Props Props(TimeSpan interval) =>
        Akka.Actor.Props.Create(() => new CpuMonitorActor(interval));

    public CpuMonitorActor(TimeSpan interval)
    {
        var interval1 = interval;

        Receive<StartMonitoring>(_ =>
        {
            CleanupPreviousStream();

            _streamCts = new CancellationTokenSource();
            _channel = Channel.CreateBounded<CpuSnapshot>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.Zero, interval1, Self, new Tick(), Self);

            var stream = ChannelHelper.ReadFromChannelAsync(_channel.Reader, _streamCts.Token);
            Sender.Tell(new MonitoringStream<CpuSnapshot>(stream, _streamCts));
        });

        Receive<Tick>(_ =>
        {
            if (_channel is null) return;

            _cpuName ??= ReadCpuName();

            var (totalPercent, cores) = MeasureCpu();
            _channel.Writer.TryWrite(new CpuSnapshot(_cpuName, totalPercent, cores));
        });
    }

    private void CleanupPreviousStream()
    {
        _tickSchedule?.Cancel();
        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _channel?.Writer.TryComplete();
        _tickSchedule = null;
        _streamCts = null;
        _channel = null;
    }

    private (double Total, List<double> Cores) MeasureCpu()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return MeasureCpuWindows();
            return MeasureCpuLinux();
        }
        catch { return (0, []); }
    }

    private (double, List<double>) MeasureCpuWindows()
    {
        GetSystemTimes(out var idleTime, out var kernelTime, out var userTime);
        var idle = idleTime.ToLong();
        var total = kernelTime.ToLong() + userTime.ToLong();

        var idleDelta = idle - _prevIdle;
        var totalDelta = total - _prevTotal;
        _prevIdle = idle;
        _prevTotal = total;

        var totalPercent = totalDelta > 0 ? (1.0 - (double)idleDelta / totalDelta) * 100 : 0;
        totalPercent = Math.Clamp(totalPercent, 0, 100);

        var coreCount = Environment.ProcessorCount;
        var cores = Enumerable.Repeat(totalPercent, coreCount).ToList();

        return (totalPercent, cores);
    }

    private (double, List<double>) MeasureCpuLinux()
    {
        try
        {
            var line = File.ReadAllLines("/proc/stat")[0];
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
                return (0, []);

            long user = long.Parse(parts[1]), nice = long.Parse(parts[2]);
            long system = long.Parse(parts[3]), idleVal = long.Parse(parts[4]);
            var total = user + nice + system + idleVal;
            if (parts.Length > 5) total += long.Parse(parts[5]);
            if (parts.Length > 6) total += long.Parse(parts[6]);
            if (parts.Length > 7) total += long.Parse(parts[7]);

            var idleDelta = idleVal - _prevIdle;
            var totalDelta = total - _prevTotal;
            _prevIdle = idleVal;
            _prevTotal = total;

            var pct = totalDelta > 0 ? (1.0 - (double)idleDelta / totalDelta) * 100 : 0;
            pct = Math.Clamp(pct, 0, 100);

            var coreCount = Environment.ProcessorCount;
            return (pct, Enumerable.Repeat(pct, coreCount).ToList());
        }
        catch { return (0, []); }
    }

    private static string ReadCpuName()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "CPU";
            }
            var cpuinfo = File.ReadAllLines("/proc/cpuinfo");
            var nameLine = cpuinfo.FirstOrDefault(l => l.StartsWith("model name"));
            return nameLine?.Split(':').LastOrDefault()?.Trim() ?? "CPU";
        }
        catch { return "CPU"; }
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint Low;
        public uint High;
        public long ToLong() => ((long)High << 32) | Low;
    }

    protected override void PostStop()
    {
        CleanupPreviousStream();
        base.PostStop();
    }
}
