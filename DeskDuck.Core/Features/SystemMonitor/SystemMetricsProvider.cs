using DeskDuck.Core.Helper;
using Microsoft.Extensions.Logging;
using Windows.Devices.Power;

namespace DeskDuck.Core.Features.SystemMonitor;

public class SystemMetricsProvider(ILogger<SystemMetricsProvider> logger) : ISystemMetricsProvider
{
    private readonly ILogger<SystemMetricsProvider> _logger = logger;

    /// <inheritdoc/>
    public double? GetBatteryPercent()
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
                    return ((double)remaining / full) * 100.0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting battery");
        }
        return null;
    }

    /// <inheritdoc/>
    public async Task<double?> GetCpuUsageAsync(CancellationToken cancellationToken)
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
            _logger.LogDebug(ex, "Error getting CPU usage");
        }
        return null;
    }

    /// <inheritdoc/>
    public double? GetRamUsage()
    {
        try
        {
            if (Win32WindowHelper.GetMemoryLoad(out uint memoryLoad))
                return memoryLoad;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting RAM usage");
        }
        return null;
    }
}
