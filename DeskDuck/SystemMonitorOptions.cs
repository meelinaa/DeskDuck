namespace DeskDuck
{
    public class SystemMonitorOptions
    {
        public bool Enabled { get; set; } = true;
        public int CheckIntervalSeconds { get; set; } = 60;
        public bool BatteryWarningEnabled { get; set; } = true;
        public int BatteryWarningThresholdPercent { get; set; } = 20;
        public bool CpuWarningEnabled { get; set; } = true;
        public int CpuWarningThresholdPercent { get; set; } = 85;
        public bool RamWarningEnabled { get; set; } = true;
        public int RamWarningThresholdPercent { get; set; } = 85;
    }
}
