namespace DeskDuck.Core.Features.SystemMonitor;

public interface ISystemMetricsProvider
{
    double? GetBatteryPercent();
    Task<double?> GetCpuUsageAsync(CancellationToken cancellationToken);
    double? GetRamUsage();
}
