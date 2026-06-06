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
    private long[]? _prevCoreIdle;
    private long[]? _prevCoreTotal;
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
        var cores = GetPerCoreCpuWindows(coreCount);

        return (totalPercent, cores);
    }

    private List<double> GetPerCoreCpuWindows(int coreCount)
    {
        try
        {
            var size = Marshal.SizeOf<SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION>() * coreCount;
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                var status = NtQuerySystemInformation(8, buffer, size, out _);
                if (status != 0) return Enumerable.Repeat(0.0, coreCount).ToList();

                _prevCoreIdle ??= new long[coreCount];
                _prevCoreTotal ??= new long[coreCount];

                var cores = new List<double>(coreCount);
                for (var i = 0; i < coreCount; i++)
                {
                    var ptr = buffer + i * Marshal.SizeOf<SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION>();
                    var info = Marshal.PtrToStructure<SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION>(ptr);
                    var coreIdle = info.IdleTime;
                    var coreTotal = info.KernelTime + info.UserTime;

                    var idleDelta = coreIdle - _prevCoreIdle[i];
                    var totalDelta = coreTotal - _prevCoreTotal[i];
                    _prevCoreIdle[i] = coreIdle;
                    _prevCoreTotal[i] = coreTotal;

                    var pct = totalDelta > 0 ? (1.0 - (double)idleDelta / totalDelta) * 100 : 0;
                    cores.Add(Math.Clamp(pct, 0, 100));
                }
                return cores;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        catch { return Enumerable.Repeat(0.0, coreCount).ToList(); }
    }

    private (double, List<double>) MeasureCpuLinux()
    {
        try
        {
            var lines = File.ReadAllLines("/proc/stat");
            var totalPct = ParseProcStatLine(lines[0], ref _prevIdle, ref _prevTotal);

            var coreCount = Environment.ProcessorCount;
            _prevCoreIdle ??= new long[coreCount];
            _prevCoreTotal ??= new long[coreCount];

            var cores = new List<double>(coreCount);
            for (var i = 0; i < coreCount; i++)
            {
                var lineIdx = i + 1;
                if (lineIdx < lines.Length && lines[lineIdx].StartsWith("cpu"))
                    cores.Add(ParseProcStatLine(lines[lineIdx], ref _prevCoreIdle[i], ref _prevCoreTotal[i]));
                else
                    cores.Add(totalPct);
            }

            return (totalPct, cores);
        }
        catch { return (0, []); }
    }

    private static double ParseProcStatLine(string line, ref long prevIdle, ref long prevTotal)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5) return 0;

        long user = long.Parse(parts[1]), nice = long.Parse(parts[2]);
        long system = long.Parse(parts[3]), idle = long.Parse(parts[4]);
        var total = user + nice + system + idle;
        for (var j = 5; j < Math.Min(parts.Length, 8); j++)
            if (long.TryParse(parts[j], out var v)) total += v;

        var idleDelta = idle - prevIdle;
        var totalDelta = total - prevTotal;
        prevIdle = idle;
        prevTotal = total;

        var pct = totalDelta > 0 ? (1.0 - (double)idleDelta / totalDelta) * 100 : 0;
        return Math.Clamp(pct, 0, 100);
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

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int infoClass, IntPtr buffer, int size, out int returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION
    {
        public long IdleTime;
        public long KernelTime;
        public long UserTime;
        public long DpcTime;
        public long InterruptTime;
        public int InterruptCount;
    }

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
