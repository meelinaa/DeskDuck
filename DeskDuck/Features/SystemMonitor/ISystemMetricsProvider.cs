using System.Threading;
using System.Threading.Tasks;

namespace DeskDuck.Features.SystemMonitor
{
    public interface ISystemMetricsProvider
    {
        double? GetBatteryPercent();
        Task<double?> GetCpuUsageAsync(CancellationToken cancellationToken);
        double? GetRamUsage();
    }
}
