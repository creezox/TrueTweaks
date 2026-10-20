using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace TrueTweaks.Services
{
    public interface ISystemMonitorService : INotifyPropertyChanged, IDisposable
    {
        // Real-time Metrics
        double CpuUsage { get; }
        double RamUsedGb { get; }
        double RamTotalGb { get; }
        double RamUsagePercentage { get; }
        double DiskFreeGb { get; }
        double DiskTotalGb { get; }
        double DiskUsagePercentage { get; }
        string DiskDriveName { get; }

        // Segmented Storage Capacity Metrics (v2.0.0)
        double DiskSystemUsedGb { get; }
        double DiskAppsUsedGb { get; }
        GridLength DiskSystemColumnWidth { get; }
        GridLength DiskAppsColumnWidth { get; }
        GridLength DiskFreeColumnWidth { get; }
        string NvmeHealthStatus { get; }
        string NvmeTemperatureText { get; }

        // GPU Metrics
        double GpuUsage { get; }
        string GpuName { get; }
        string GpuClockText { get; }
        string GpuVramText { get; }
        ObservableCollection<double> GpuHistory { get; }

        int AppliedTweaksCount { get; set; }
        int TotalTweaksCount { get; set; }
        int AppliedSubTweaksCount { get; set; }
        int TotalSubTweaksCount { get; set; }
        int HealthScore { get; set; }
        string HealthStatusText { get; set; }

        // Hardware Specifications
        string CpuModelName { get; }
        string CpuClockSpeed { get; }
        string RamSpeedAndType { get; }
        string DiskTypeAndModel { get; }
        string LatencyProfileStatus { get; }

        // Live Scrolling History (60-Point Circular Buffer Feed)
        ObservableCollection<double> CpuHistory { get; }
        ObservableCollection<double> RamHistory { get; }
        ObservableCollection<double> DiskHistory { get; }
        ObservableCollection<double> TweaksHistory { get; }

        void Start();
        void Stop();
        void RefreshMetrics();
    }
}
