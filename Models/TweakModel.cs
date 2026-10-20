using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TrueTweaks.Models
{
    public enum TweakCategory
    {
        WindowsSystem,
        GamingGPU,
        MemoryRAM,
        NetworkPing,
        HardwareMSI,
        InputDevices,
        PrivacyTelemetry,
        WindowsServices,
        VisualsUI
    }

    public enum RiskLevel
    {
        Safe,
        Advanced,
        Experimental
    }

    /// <summary>
    /// Represents a selectable preset inside a mutually exclusive Master Card (SegmentedPillSelector).
    /// </summary>
    public partial class TweakPresetOption : ObservableObject
    {
        public int Index { get; set; }
        public string Label { get; set; } = string.Empty;
        public string TechnicalBadge { get; set; } = string.Empty;
        public string LatencyBadge { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TechnicalDetails { get; set; } = string.Empty;
        public TweakModel? UnderlyingTweak { get; set; }
        public TweakModel? ParentMasterCard { get; set; }
    }

    public partial class TweakModel : ObservableObject
    {
        // =========================================================================
        // UNIFIED BADGE SYSTEM BRUSHES (v2.0.0 Obsidian Neutral + Surgical Neon)
        // =========================================================================

        // 1. Registry Key Badge: Bg #161922, Border #262B38, Text #A0AAB8
        public static readonly SolidColorBrush UnifiedRegBg = CreateFrozenBrush(0xFF, 0x16, 0x19, 0x22);
        public static readonly SolidColorBrush UnifiedRegBorder = CreateFrozenBrush(0xFF, 0x26, 0x2B, 0x38);
        public static readonly SolidColorBrush UnifiedRegFg = CreateFrozenBrush(0xFF, 0xA0, 0xAA, 0xB8);

        // 2. Impact Badge: Bg rgba(0,255,102,0.08) (#1400FF66), Border rgba(0,255,102,0.25) (#4000FF66), Text #00FF66
        public static readonly SolidColorBrush UnifiedImpactBg = CreateFrozenBrush(0x14, 0x00, 0xFF, 0x66);
        public static readonly SolidColorBrush UnifiedImpactBorder = CreateFrozenBrush(0x40, 0x00, 0xFF, 0x66);
        public static readonly SolidColorBrush UnifiedImpactFg = CreateFrozenBrush(0xFF, 0x00, 0xFF, 0x66);

        // 3. Safety Badge: Bg #181C26, Border #262B38, Text #E2E8F0 + 6px Semantic Dot (#00FF66 / #FFB800 / #FF3366)
        public static readonly SolidColorBrush UnifiedSafetyBg = CreateFrozenBrush(0xFF, 0x18, 0x1C, 0x26);
        public static readonly SolidColorBrush UnifiedSafetyBorder = CreateFrozenBrush(0xFF, 0x26, 0x2B, 0x38);
        public static readonly SolidColorBrush UnifiedSafetyTextFg = CreateFrozenBrush(0xFF, 0xE2, 0xE8, 0xF0);

        private static readonly SolidColorBrush SafeDotBrush = CreateFrozenBrush(0xFF, 0x00, 0xFF, 0x66);
        private static readonly SolidColorBrush AdvDotBrush = CreateFrozenBrush(0xFF, 0xFF, 0xB8, 0x00);
        private static readonly SolidColorBrush ExpDotBrush = CreateFrozenBrush(0xFF, 0xFF, 0x33, 0x66);

        private static SolidColorBrush CreateFrozenBrush(byte a, byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }

        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TooltipInfo))]
        private string _technicalBadge = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        public TweakCategory Category { get; set; } = TweakCategory.WindowsSystem;
        public RiskLevel Risk { get; set; } = RiskLevel.Safe;
        public string Impact { get; set; } = "High Impact";

        [ObservableProperty]
        private string _latencyBadge = "Low Latency";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TooltipInfo))]
        private string _technicalDetails = string.Empty;

        public string? GroupId { get; set; } = null;
        public int SubTweaksCount { get; set; } = 1;

        // Mutually Exclusive Master Card Support (SegmentedPillSelector)
        public bool IsExclusiveGroup { get; set; }
        public ObservableCollection<TweakPresetOption> PresetOptions { get; } = new();

        [ObservableProperty]
        private int _selectedPresetIndex;

        [ObservableProperty]
        private bool _isApplied;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isBusy;

        public Func<Task<bool>>? ActionApply { get; set; }
        public Func<Task<bool>>? ActionRevert { get; set; }

        public string SubTweaksBadge => SubTweaksCount > 1
            ? $"{SubTweaksCount:N0} Rules"
            : "1 Rule";

        // Unified Badge Brushes exposed for direct XAML binding
        public SolidColorBrush RegBadgeBackground => UnifiedRegBg;
        public SolidColorBrush RegBadgeBorder => UnifiedRegBorder;
        public SolidColorBrush RegBadgeForeground => UnifiedRegFg;

        public SolidColorBrush ImpactBadgeBackground => UnifiedImpactBg;
        public SolidColorBrush ImpactBadgeBorder => UnifiedImpactBorder;
        public SolidColorBrush ImpactBadgeForeground => UnifiedImpactFg;

        public SolidColorBrush RiskBadgeBackground => UnifiedSafetyBg;
        public SolidColorBrush RiskBadgeBorder => UnifiedSafetyBorder;
        public SolidColorBrush RiskBadgeForeground => UnifiedSafetyTextFg;

        public SolidColorBrush RiskDotBrush => Risk switch
        {
            RiskLevel.Safe => SafeDotBrush,
            RiskLevel.Advanced => AdvDotBrush,
            RiskLevel.Experimental => ExpDotBrush,
            _ => SafeDotBrush
        };

        public string CategoryDisplay => Category switch
        {
            TweakCategory.WindowsSystem => "Kernel & CPU",
            TweakCategory.GamingGPU => "FPS & Gaming",
            TweakCategory.MemoryRAM => "Memory & RAM",
            TweakCategory.NetworkPing => "Network & Ping",
            TweakCategory.HardwareMSI => "Hardware & MSI",
            TweakCategory.InputDevices => "Mouse & USB",
            TweakCategory.PrivacyTelemetry => "Privacy & ETW",
            TweakCategory.WindowsServices => "Services & Tasks",
            TweakCategory.VisualsUI => "UI & Visuals",
            _ => "System"
        };

        public string RiskDisplay => Risk switch
        {
            RiskLevel.Safe => "Safe",
            RiskLevel.Advanced => "Moderate",
            RiskLevel.Experimental => "Expert",
            _ => "Safe"
        };

        public string TooltipInfo => !string.IsNullOrWhiteSpace(TechnicalDetails)
            ? $"Target Registry / Kernel Component:\n{TechnicalDetails}\n\nEngine Operations: {SubTweaksCount:N0} low-level rules\n100% reversible via TrueTweaks."
            : $"Native Windows Optimization: {Title}\nTechnical Key: {TechnicalBadge}\nEngine Operations: {SubTweaksCount:N0} low-level rules\n100% reversible via TrueTweaks.";
    }
}
