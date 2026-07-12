using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DeskDuck
{
    public class SystemMonitorPublisherService : BackgroundService
    {
        private readonly IOptions<SystemMonitorOptions> _options;
        private readonly RabbitMqPublisher _publisher;

        private bool _batteryWarningTriggered = false;
        private bool _cpuWarningTriggered = false;
        private bool _ramWarningTriggered = false;

        #region Win32 P/Invokes
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
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
            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
            public ulong ToUInt64() => ((ulong)dwHighDateTime << 32) | dwLowDateTime;
        }
        #endregion

        public SystemMonitorPublisherService(
            IOptions<SystemMonitorOptions> options,
            RabbitMqPublisher publisher)
        {
            _options = options;
            _publisher = publisher;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var config = _options.Value;
                if (config.Enabled)
                {
                    try
                    {
                        await CheckSystemMetricsAsync(config, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SystemMonitor] Error during check: {ex.Message}");
                    }
                }

                // Wait for the configured interval
                int intervalSeconds = Math.Max(1, config.CheckIntervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
        }

        private async Task CheckSystemMetricsAsync(SystemMonitorOptions config, CancellationToken cancellationToken)
        {
            // 1. Battery Check
            if (config.BatteryWarningEnabled)
            {
                double? batteryPercent = GetBatteryPercent();
                if (batteryPercent.HasValue)
                {
                    if (batteryPercent.Value < config.BatteryWarningThresholdPercent)
                    {
                        if (!_batteryWarningTriggered)
                        {
                            _batteryWarningTriggered = true;
                            await _publisher.PublishAsync(
                                source: "SystemMonitor",
                                severity: "Warning",
                                text: $"Akkustand ist niedrig: {batteryPercent.Value:F1}% (Schwellenwert: {config.BatteryWarningThresholdPercent}%)",
                                cancellationToken: cancellationToken
                            );
                        }
                    }
                    else
                    {
                        _batteryWarningTriggered = false;
                    }
                }
            }

            // 2. CPU Check
            if (config.CpuWarningEnabled)
            {
                double? cpuUsage = await GetCpuUsageAsync(cancellationToken);
                if (cpuUsage.HasValue)
                {
                    if (cpuUsage.Value > config.CpuWarningThresholdPercent)
                    {
                        if (!_cpuWarningTriggered)
                        {
                            _cpuWarningTriggered = true;
                            await _publisher.PublishAsync(
                                source: "SystemMonitor",
                                severity: "Warning",
                                text: $"Hohe CPU-Auslastung: {cpuUsage.Value:F1}% (Schwellenwert: {config.CpuWarningThresholdPercent}%)",
                                cancellationToken: cancellationToken
                            );
                        }
                    }
                    else
                    {
                        _cpuWarningTriggered = false;
                    }
                }
            }

            // 3. RAM Check
            if (config.RamWarningEnabled)
            {
                double? ramUsage = GetRamUsage();
                if (ramUsage.HasValue)
                {
                    if (ramUsage.Value > config.RamWarningThresholdPercent)
                    {
                        if (!_ramWarningTriggered)
                        {
                            _ramWarningTriggered = true;
                            await _publisher.PublishAsync(
                                source: "SystemMonitor",
                                severity: "Warning",
                                text: $"Hohe RAM-Auslastung: {ramUsage.Value:F1}% (Schwellenwert: {config.RamWarningThresholdPercent}%)",
                                cancellationToken: cancellationToken
                            );
                        }
                    }
                    else
                    {
                        _ramWarningTriggered = false;
                    }
                }
            }
        }

        private double? GetBatteryPercent()
        {
            try
            {
                var aggregateBattery = Windows.Devices.Power.Battery.AggregateBattery;
                var report = aggregateBattery.GetReport();
                if (report.RemainingCapacityInMilliwattHours.HasValue && report.FullChargeCapacityInMilliwattHours.HasValue)
                {
                    int full = report.FullChargeCapacityInMilliwattHours.Value;
                    int remaining = report.RemainingCapacityInMilliwattHours.Value;
                    if (full > 0)
                    {
                        return ((double)remaining / full) * 100.0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SystemMonitor] Error getting battery: {ex.Message}");
            }
            return null;
        }

        private async Task<double?> GetCpuUsageAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (GetSystemTimes(out var idleTime1, out var kernelTime1, out var userTime1))
                {
                    await Task.Delay(250, cancellationToken);
                    if (GetSystemTimes(out var idleTime2, out var kernelTime2, out var userTime2))
                    {
                        var idleDifference = idleTime2.ToUInt64() - idleTime1.ToUInt64();
                        var kernelDifference = kernelTime2.ToUInt64() - kernelTime1.ToUInt64();
                        var userDifference = userTime2.ToUInt64() - userTime1.ToUInt64();

                        var totalDifference = kernelDifference + userDifference;
                        if (totalDifference > 0)
                        {
                            var systemTime = totalDifference - idleDifference;
                            return (100.0 * systemTime) / totalDifference;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SystemMonitor] Error getting CPU usage: {ex.Message}");
            }
            return null;
        }

        private double? GetRamUsage()
        {
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    return memStatus.dwMemoryLoad;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SystemMonitor] Error getting RAM usage: {ex.Message}");
            }
            return null;
        }
    }
}
