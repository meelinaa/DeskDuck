using DeskDuck.Core.Features.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeskDuck.Core.Features.SystemMonitor;

/// <summary>
/// Hosted background service that periodically checks system health metrics
/// (battery level, CPU usage, RAM usage) and publishes warning notifications
/// to RabbitMQ when a configured threshold is exceeded.
/// Each warning is only published once per threshold breach; it resets after
/// the metric recovers so the user is not spammed with repeated alerts.
/// </summary>
public partial class SystemMonitorPublisherService : BackgroundService
{
    private readonly IOptionsMonitor<SystemMonitorOptions> _optionsMonitor;
    private readonly IRabbitMqPublisher _publisher;
    private readonly ILogger<SystemMonitorPublisherService> _logger;
    private readonly ISystemMetricsProvider _metricsProvider;
    private CancellationTokenSource? _delayCts;

    public SystemMonitorPublisherService(
        IOptionsMonitor<SystemMonitorOptions> optionsMonitor,
        IRabbitMqPublisher publisher,
        ILogger<SystemMonitorPublisherService> logger,
        ISystemMetricsProvider metricsProvider)
    {
        _optionsMonitor = optionsMonitor;
        _publisher = publisher;
        _logger = logger;
        _metricsProvider = metricsProvider;

        _optionsMonitor.OnChange(config =>
        {
            // Cancel the delay to immediately pick up new configuration
            _delayCts?.Cancel();
        });
    }

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
            SystemMonitorOptions config = _optionsMonitor.CurrentValue;
            if (config.Enabled)
            {
                try
                {
                    await CheckSystemMetricsAsync(config, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during check");
                }
            }

            int intervalSeconds = Math.Max(1, config.CheckIntervalSeconds);

            try
            {
                _delayCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), _delayCts.Token);
            }
            catch (TaskCanceledException)
            {
                // If cancellation was triggered by options change, stoppingToken won't be cancelled.
            }
            finally
            {
                _delayCts?.Dispose();
                _delayCts = null;
            }
        }
    }

    /// <summary>
    /// Evaluates battery, CPU, and RAM metrics against their configured thresholds and
    /// publishes a warning notification the first time each threshold is breached.
    /// Clears the triggered flag once the metric returns to a safe level so subsequent
    /// breaches will generate a new warning.
    /// </summary>
    internal async Task CheckSystemMetricsAsync(SystemMonitorOptions config, CancellationToken cancellationToken)
    {
        if (config.BatteryWarningEnabled)
        {
            double? batteryPercent = _metricsProvider.GetBatteryPercent();
            if (batteryPercent.HasValue)
            {
                int clampedBatteryThreshold = Math.Clamp(config.BatteryWarningThresholdPercent, 0, 100);
                if (batteryPercent.Value < clampedBatteryThreshold)
                {
                    if (!_batteryWarningTriggered)
                    {
                        _batteryWarningTriggered = true;
                        await _publisher.PublishAsync(
                            source: "SystemMonitor",
                            severity: "Warning",
                            text: $"Akkustand ist niedrig: {batteryPercent.Value:F1}% (Schwellenwert: {clampedBatteryThreshold}%)",
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
            double? cpuUsage = await _metricsProvider.GetCpuUsageAsync(cancellationToken);
            if (cpuUsage.HasValue)
            {
                int clampedCpuThreshold = Math.Clamp(config.CpuWarningThresholdPercent, 0, 100);
                if (cpuUsage.Value > clampedCpuThreshold)
                {
                    if (!_cpuWarningTriggered)
                    {
                        _cpuWarningTriggered = true;
                        await _publisher.PublishAsync(
                            source: "SystemMonitor",
                            severity: "Warning",
                            text: $"Hohe CPU-Auslastung: {cpuUsage.Value:F1}% (Schwellenwert: {clampedCpuThreshold}%)",
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
            double? ramUsage = _metricsProvider.GetRamUsage();
            if (ramUsage.HasValue)
            {
                int clampedRamThreshold = Math.Clamp(config.RamWarningThresholdPercent, 0, 100);
                if (ramUsage.Value > clampedRamThreshold)
                {
                    if (!_ramWarningTriggered)
                    {
                        _ramWarningTriggered = true;
                        await _publisher.PublishAsync(
                            source: "SystemMonitor",
                            severity: "Warning",
                            text: $"Hohe RAM-Auslastung: {ramUsage.Value:F1}% (Schwellenwert: {clampedRamThreshold}%)",
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
}
