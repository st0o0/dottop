using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace dottop.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsCpuMetrics : ICpuMetricsProvider
{
    private readonly PerformanceCounter _totalCounter;
    private readonly PerformanceCounter[] _coreCounters;
    private readonly string _cpuName;

    public WindowsCpuMetrics()
    {
        try
        {
            var coreCount = Environment.ProcessorCount;
            _totalCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
            var cores = new List<PerformanceCounter>();
            for (var i = 0; i < coreCount; i++)
            {
                try { cores.Add(new PerformanceCounter("Processor", "% Processor Time", i.ToString(), true)); }
                catch { break; }
            }
            _coreCounters = cores.ToArray();
            _cpuName = ReadCpuName();

            _totalCounter.NextValue();
            foreach (var c in _coreCounters)
            {
                try { c.NextValue(); } catch { }
            }
        }
        catch
        {
            _totalCounter ??= new PerformanceCounter();
            _coreCounters ??= [];
            _cpuName ??= "Unknown CPU";
        }
    }

    public (string Name, double TotalPercent, IReadOnlyList<double> CorePercents) GetSnapshot()
    {
        try
        {
            var total = Math.Clamp(_totalCounter.NextValue(), 0, 100);
            var cores = new double[_coreCounters.Length];
            for (var i = 0; i < _coreCounters.Length; i++)
            {
                try { cores[i] = Math.Clamp(_coreCounters[i].NextValue(), 0, 100); }
                catch { cores[i] = 0; }
            }
            return (_cpuName, total, cores);
        }
        catch { return (_cpuName, 0, new double[_coreCounters.Length]); }
    }

    public void Dispose()
    {
        _totalCounter.Dispose();
        foreach (var c in _coreCounters)
        {
            c.Dispose();
        }
    }

    private static string ReadCpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "Unknown CPU";
        }
        catch
        {
            return "Unknown CPU";
        }
    }
}
