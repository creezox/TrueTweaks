using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TrueTweaks.Models
{
    public partial class CleanerItemModel : ObservableObject
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private long _estimatedBytes;

        [ObservableProperty]
        private bool _isCleaning;

        [ObservableProperty]
        private bool _isCleaned;

        public Func<Task<long>>? ScanAction { get; set; }
        public Func<Task<long>>? CleanAction { get; set; }

        public string FormattedSize
        {
            get
            {
                if (EstimatedBytes <= 0) return "0 Mo";
                if (EstimatedBytes < 1024 * 1024)
                    return $"{EstimatedBytes / 1024.0:F1} Ko";
                if (EstimatedBytes < 1024L * 1024L * 1024L)
                    return $"{EstimatedBytes / (1024.0 * 1024.0):F1} Mo";
                return $"{EstimatedBytes / (1024.0 * 1024.0 * 1024.0):F2} Go";
            }
        }

        partial void OnEstimatedBytesChanged(long value)
        {
            OnPropertyChanged(nameof(FormattedSize));
        }
    }
}
