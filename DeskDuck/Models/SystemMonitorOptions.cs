namespace DeskDuck.Models
{
    /// <summary>
    /// Configuration options for the system health monitor publisher service.
    /// Maps to the "Publishers:SystemMonitor" section of appsettings.json and
    /// is injected via <c>IOptions&lt;SystemMonitorOptions&gt;</c>.
    /// </summary>
    public class SystemMonitorOptions
    {
        /// <summary>Whether the system monitor is active. Set to <c>false</c> to disable all checks.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>How often (in seconds) the service samples system metrics.</summary>
        public int CheckIntervalSeconds { get; set; } = 60;

        /// <summary>Whether low-battery warnings are enabled.</summary>
        public bool BatteryWarningEnabled { get; set; } = true;

        /// <summary>Battery percentage below which a warning notification is triggered.</summary>
        public int BatteryWarningThresholdPercent { get; set; } = 20;

        /// <summary>Whether high-CPU warnings are enabled.</summary>
        public bool CpuWarningEnabled { get; set; } = true;

        /// <summary>CPU usage percentage above which a warning notification is triggered.</summary>
        public int CpuWarningThresholdPercent { get; set; } = 85;

        /// <summary>Whether high-RAM warnings are enabled.</summary>
        public bool RamWarningEnabled { get; set; } = true;

        /// <summary>RAM usage percentage above which a warning notification is triggered.</summary>
        public int RamWarningThresholdPercent { get; set; } = 85;
    }
}
