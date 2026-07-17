using DeskDuck.Helper;
using DeskDuck.Models;
using DeskDuck.Publisher;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Power;

namespace DeskDuck.Services
{
    /// <summary>
    /// Hosted background service that periodically checks system health metrics
    /// (battery level, CPU usage, RAM usage) and publishes warning notifications
    /// to RabbitMQ when a configured threshold is exceeded.
    /// Each warning is only published once per threshold breach; it resets after
    /// the metric recovers so the user is not spammed with repeated alerts.
    /// </summary>
    public partial class SystemMonitorPublisherService(
        IOptions<SystemMonitorOptions> options,
        RabbitMqPublisher publisher) : BackgroundService
    {
        private readonly IOptions<SystemMonitorOptions> _options = options;
        private readonly RabbitMqPublisher _publisher = publisher;

        private bool _batteryWarningTriggered = false;
        private bool _cpuWarningTriggered = false;
        private bool _ramWarningTriggered = false;



        /// <summary>
        /// Main service loop. Reads the latest configuration on every iteration so that
        /// changes to appsettings.json are picked up without restarting the application.
        /// Sleeps for the configured interval between checks.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                SystemMonitorOptions config = _options.Value;
                if (config.Enabled)
                {
                    try
                    {
                        await CheckSystemMetricsAsync(config, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SystemMonitor] Error during check: {ex.Message}");
                    }
                }

                int intervalSeconds = Math.Max(1, config.CheckIntervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
        }

        /// <summary>
        /// Evaluates battery, CPU, and RAM metrics against their configured thresholds and
        /// publishes a warning notification the first time each threshold is breached.
        /// Clears the triggered flag once the metric returns to a safe level so subsequent
        /// breaches will generate a new warning.
        /// </summary>
        private async Task CheckSystemMetricsAsync(SystemMonitorOptions config, CancellationToken cancellationToken)
        {
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

        /// <summary>
        /// Returns the current battery charge as a percentage using the WinRT
        /// <see cref="Battery.AggregateBattery"/> API. Returns <c>null</c> if no battery
        /// is present or the capacity values are unavailable.
        /// </summary>
        private static double? GetBatteryPercent()
        {
            try
            {
                Battery aggregateBattery = Battery.AggregateBattery;
                BatteryReport report = aggregateBattery.GetReport();
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
                Debug.WriteLine($"[SystemMonitor] Error getting battery: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Calculates CPU usage by sampling GetSystemTimes twice with a 250 ms interval
        /// and computing the ratio of active time to total elapsed time.
        /// Returns <c>null</c> if the P/Invoke call fails.
        /// </summary>
        private static async Task<double?> GetCpuUsageAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (Win32WindowHelper.GetSystemTimesInfo(out var idleTime1, out var kernelTime1, out var userTime1))
                {
                    await Task.Delay(250, cancellationToken);
                    if (Win32WindowHelper.GetSystemTimesInfo(out var idleTime2, out var kernelTime2, out var userTime2))
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
                Debug.WriteLine($"[SystemMonitor] Error getting CPU usage: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Returns the current system-wide RAM usage percentage via the Win32
        /// <c>GlobalMemoryStatusEx</c> API. Returns <c>null</c> if the call fails.
        /// </summary>
        private static double? GetRamUsage()
        {
            try
            {
                if (Win32WindowHelper.GetMemoryLoad(out uint memoryLoad))
                {
                    return memoryLoad;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemMonitor] Error getting RAM usage: {ex.Message}");
            }
            return null;
        }
    }
}
