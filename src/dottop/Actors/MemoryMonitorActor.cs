using System.Runtime.InteropServices;
using System.Threading.Channels;
using Akka.Actor;
using dottop.Models;

namespace dottop.Actors;

public sealed class MemoryMonitorActor : ReceiveActor
{
    private readonly TimeSpan _interval;
    private Channel<MemorySnapshot>? _channel;
    private ICancelable? _tickSchedule;
    private CancellationTokenSource? _streamCts;

    public static Props Props(TimeSpan interval) =>
        Akka.Actor.Props.Create(() => new MemoryMonitorActor(interval));

    public MemoryMonitorActor(TimeSpan interval)
    {
        _interval = interval;

        Receive<StartMonitoring>(_ =>
        {
            CleanupPreviousStream();

            _streamCts = new CancellationTokenSource();
            _channel = Channel.CreateBounded<MemorySnapshot>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.Zero, _interval, Self, new Tick(), Self);

            var stream = ChannelHelper.ReadFromChannelAsync(_channel.Reader, _streamCts.Token);
            Sender.Tell(new MonitoringStream<MemorySnapshot>(stream, _streamCts));
        });

        Receive<Tick>(_ =>
        {
            if (_channel is null) return;

            var (total, used) = GetMemoryInfo();
            _channel.Writer.TryWrite(new MemorySnapshot(total, used));
        });
    }

    private static (ulong Total, ulong Used) GetMemoryInfo()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var status = new MEMORYSTATUSEX { dwLength = 64 };
            if (GlobalMemoryStatusEx(ref status))
                return (status.ullTotalPhys, status.ullTotalPhys - status.ullAvailPhys);
        }
        else
        {
            try
            {
                ulong total = 0, available = 0;
                foreach (var line in File.ReadAllLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:"))
                        total = ParseKb(line) * 1024;
                    else if (line.StartsWith("MemAvailable:"))
                        available = ParseKb(line) * 1024;
                }
                if (total > 0) return (total, total - available);
            }
            catch { }
        }
        return (0, 0);
    }

    private static ulong ParseKb(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && ulong.TryParse(parts[1], out var val) ? val : 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
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

    protected override void PostStop()
    {
        CleanupPreviousStream();
        base.PostStop();
    }
}
