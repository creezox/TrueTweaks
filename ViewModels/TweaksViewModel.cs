using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrueTweaks.Models;
using TrueTweaks.Services;

namespace TrueTweaks.ViewModels
{
    public partial class TweaksViewModel : ViewModelBase
    {
        public override string Title => "Optimization Center";

        public ITweaksManagerService Manager { get; }
        private readonly ILoggerService _logger;
        private readonly IToastService _toastService;

        // Consolidated catalog where mutually exclusive GroupIds are merged into Master Cards
        public ObservableCollection<TweakModel> ConsolidatedTweaks { get; } = new();

        private readonly ICollectionView _filteredTweaksView;
        public ICollectionView FilteredTweaks => _filteredTweaksView;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSearchEmpty))]
        private string _searchText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CategoryTitle))]
        [NotifyPropertyChangedFor(nameof(CategoryDescription))]
        private string _selectedCategory = "ALL";

        /// <summary>Header title of the active sidebar optimization section.</summary>
        public string CategoryTitle => SelectedCategory switch
        {
            "WINDOWS" => "Kernel & CPU",
            "GAMING" => "FPS & Gaming",
            "MEMORY" => "Memory & RAM",
            "NETWORK" => "Network & Ping",
            "HARDWARE" => "Hardware & MSI",
            "INPUT" => "Mouse & USB",
            "PRIVACY" => "Privacy & Telemetry",
            "SERVICES" => "Services & Tasks",
            "VISUALS" => "UI & Visuals",
            _ => "All Optimizations"
        };

        /// <summary>Human-readable benefit line of the active sidebar optimization section.</summary>
        public string CategoryDescription => SelectedCategory switch
        {
            "WINDOWS" => "Thread scheduling, Win32PrioritySeparation, BCD timers, core parking and C-states.",
            "GAMING" => "GameDVR removal, fullscreen exclusive, HAGS, MMCSS game profile and GPU priority.",
            "MEMORY" => "Kernel paging, SysMain, memory compression, standby list and pagefile behaviour.",
            "NETWORK" => "Nagle's algorithm, TCP ACK frequency, NIC offloads, QoS and network throttling.",
            "HARDWARE" => "PCIe MSI interrupts, device priority, ASPM link states and storage latency.",
            "INPUT" => "Raw 1:1 mouse input, keyboard repeat delay, USB selective suspend and HID polling.",
            "PRIVACY" => "Telemetry, ETW AutoLogger sessions, Copilot, Recall and advertising ID.",
            "SERVICES" => "Background services and scheduled tasks safely debloated, anti-cheat compatible.",
            "VISUALS" => "DWM animations, menu delay, transparency and Explorer shell responsiveness.",
            _ => "Every optimization module across all categories."
        };

        [ObservableProperty]
        private bool _isApplying;

        public bool IsSearchEmpty => string.IsNullOrEmpty(SearchText);

        public int AppliedCount => Manager.AppliedCount;
        public int TotalCount => Manager.TotalCount;
        public int AppliedSubTweaksCount => Manager.AppliedSubTweaksCount;
        public int TotalSubTweaksCount => Manager.TotalSubTweaksCount;

        public TweaksViewModel(ITweaksManagerService manager, ILoggerService logger, IToastService toastService)
        {
            Manager = manager;
            _logger = logger;
            _toastService = toastService;

            _filteredTweaksView = CollectionViewSource.GetDefaultView(ConsolidatedTweaks);
            _filteredTweaksView.Filter = FilterTweakItem;

            BuildConsolidatedCatalog();

            Manager.Tweaks.CollectionChanged += (_, _) =>
            {
                BuildConsolidatedCatalog();
                RefreshCounts();
                _filteredTweaksView.Refresh();
            };
        }

        /// <summary>
        /// Automatically groups mutually exclusive tweaks sharing the same Registry target (GroupId)
        /// into a single Master Card equipped with a SegmentedPillSelector.
        /// </summary>
        private void BuildConsolidatedCatalog()
        {
            ConsolidatedTweaks.Clear();
            if (Manager.Tweaks.Count == 0) return;

            var handledGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var tweak in Manager.Tweaks)
            {
                if (string.IsNullOrEmpty(tweak.GroupId))
                {
                    ConsolidatedTweaks.Add(tweak);
                    continue;
                }

                if (!handledGroups.Add(tweak.GroupId))
                {
                    continue;
                }

                var groupMembers = Manager.Tweaks
                    .Where(t => string.Equals(t.GroupId, tweak.GroupId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (string.Equals(tweak.GroupId, "win32_priority", StringComparison.OrdinalIgnoreCase))
                {
                    ConsolidatedTweaks.Add(CreateWin32PriorityMasterCard(groupMembers));
                }
                else if (string.Equals(tweak.GroupId, "sys_responsiveness", StringComparison.OrdinalIgnoreCase))
                {
                    ConsolidatedTweaks.Add(CreateSystemResponsivenessMasterCard(groupMembers));
                }
                else
                {
                    ConsolidatedTweaks.Add(CreateGenericMasterCard(tweak.GroupId, groupMembers));
                }
            }
        }

        private TweakModel CreateWin32PriorityMasterCard(List<TweakModel> members)
        {
            var master = new TweakModel
            {
                Id = "master_win32_priority",
                Title = "CPU Quantum Scheduling (Win32PrioritySeparation)",
                GroupId = "win32_priority",
                IsExclusiveGroup = true,
                Category = TweakCategory.WindowsSystem,
                Risk = RiskLevel.Safe,
                SubTweaksCount = members.FirstOrDefault()?.SubTweaksCount ?? 1
            };

            // Option 0: Off / Default (Windows Default 0x02)
            master.PresetOptions.Add(new TweakPresetOption
            {
                Index = 0,
                Label = "Off / Default",
                TechnicalBadge = "Win32PrioritySeparation = 0x02",
                LatencyBadge = "Default Quantum",
                Description = "Standard Windows thread quantum scheduling (0x02). Select an optimization profile below to prioritize game threads.",
                TechnicalDetails = @"HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl\Win32PrioritySeparation = 0x02 (2)",
                UnderlyingTweak = null,
                ParentMasterCard = master
            });

            string[] labels = { "0x26 • Max FPS", "0x28 • Frame Pacing", "0x14 • Balanced" };
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                string lbl = i < labels.Length ? labels[i] : m.LatencyBadge;
                master.PresetOptions.Add(new TweakPresetOption
                {
                    Index = i + 1,
                    Label = lbl,
                    TechnicalBadge = m.TechnicalBadge,
                    LatencyBadge = m.LatencyBadge,
                    Description = m.Description,
                    TechnicalDetails = m.TechnicalDetails,
                    UnderlyingTweak = m,
                    ParentMasterCard = master
                });
            }

            SyncMasterCardState(master);
            return master;
        }

        private TweakModel CreateSystemResponsivenessMasterCard(List<TweakModel> members)
        {
            var master = new TweakModel
            {
                Id = "master_sys_responsiveness",
                Title = "Multimedia & Gaming CPU Allocation (SystemResponsiveness)",
                GroupId = "sys_responsiveness",
                IsExclusiveGroup = true,
                Category = TweakCategory.WindowsSystem,
                Risk = RiskLevel.Safe,
                SubTweaksCount = members.FirstOrDefault()?.SubTweaksCount ?? 1
            };

            // Option 0: Default 20%
            master.PresetOptions.Add(new TweakPresetOption
            {
                Index = 0,
                Label = "Default 20%",
                TechnicalBadge = "SystemResponsiveness = 0x00000014",
                LatencyBadge = "20% Reserved",
                Description = "Windows default MMCSS allocation reserving 20% of CPU cycles for background tasks.",
                TechnicalDetails = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\SystemResponsiveness = 20",
                UnderlyingTweak = null,
                ParentMasterCard = master
            });

            string[] labels = { "0x00 • 100% Gaming", "0x0A • 90% Game / 10% Stream" };
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                string lbl = i < labels.Length ? labels[i] : m.LatencyBadge;
                master.PresetOptions.Add(new TweakPresetOption
                {
                    Index = i + 1,
                    Label = lbl,
                    TechnicalBadge = m.TechnicalBadge,
                    LatencyBadge = m.LatencyBadge,
                    Description = m.Description,
                    TechnicalDetails = m.TechnicalDetails,
                    UnderlyingTweak = m,
                    ParentMasterCard = master
                });
            }

            SyncMasterCardState(master);
            return master;
        }

        private TweakModel CreateGenericMasterCard(string groupId, List<TweakModel> members)
        {
            var first = members[0];
            var master = new TweakModel
            {
                Id = $"master_{groupId}",
                Title = first.Title,
                GroupId = groupId,
                IsExclusiveGroup = true,
                Category = first.Category,
                Risk = first.Risk,
                SubTweaksCount = first.SubTweaksCount
            };

            master.PresetOptions.Add(new TweakPresetOption
            {
                Index = 0,
                Label = "Off / Default",
                TechnicalBadge = first.TechnicalBadge,
                LatencyBadge = "Default",
                Description = first.Description,
                TechnicalDetails = first.TechnicalDetails,
                UnderlyingTweak = null,
                ParentMasterCard = master
            });

            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                master.PresetOptions.Add(new TweakPresetOption
                {
                    Index = i + 1,
                    Label = m.LatencyBadge,
                    TechnicalBadge = m.TechnicalBadge,
                    LatencyBadge = m.LatencyBadge,
                    Description = m.Description,
                    TechnicalDetails = m.TechnicalDetails,
                    UnderlyingTweak = m,
                    ParentMasterCard = master
                });
            }

            SyncMasterCardState(master);
            return master;
        }

        private static void SyncMasterCardState(TweakModel master)
        {
            if (!master.IsExclusiveGroup || master.PresetOptions.Count == 0) return;

            int activeIdx = 0;
            for (int i = 1; i < master.PresetOptions.Count; i++)
            {
                if (master.PresetOptions[i].UnderlyingTweak?.IsApplied == true)
                {
                    activeIdx = i;
                    break;
                }
            }

            // Default display metadata uses Preset 1 if Off so the user sees the primary registry key & benefit
            var displayOption = activeIdx > 0 ? master.PresetOptions[activeIdx] : (master.PresetOptions.Count > 1 ? master.PresetOptions[1] : master.PresetOptions[0]);
            master.SelectedPresetIndex = activeIdx;
            master.IsApplied = activeIdx > 0;
            master.TechnicalBadge = activeIdx > 0 ? displayOption.TechnicalBadge : master.PresetOptions[1].TechnicalBadge;
            master.LatencyBadge = displayOption.LatencyBadge;
            master.Description = displayOption.Description;
            master.TechnicalDetails = displayOption.TechnicalDetails;
        }

        private void SyncAllMasterCards()
        {
            foreach (var item in ConsolidatedTweaks)
            {
                if (item.IsExclusiveGroup)
                {
                    SyncMasterCardState(item);
                }
            }
        }

        private void RefreshCounts()
        {
            SyncAllMasterCards();
            OnPropertyChanged(nameof(AppliedCount));
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(AppliedSubTweaksCount));
            OnPropertyChanged(nameof(TotalSubTweaksCount));
        }

        partial void OnSearchTextChanged(string value)
        {
            _filteredTweaksView.Refresh();
        }

        partial void OnSelectedCategoryChanged(string value)
        {
            _filteredTweaksView.Refresh();
        }

        private bool FilterTweakItem(object item)
        {
            if (item is not TweakModel tweak) return false;

            bool categoryMatch = SelectedCategory switch
            {
                "WINDOWS" => tweak.Category == TweakCategory.WindowsSystem,
                "GAMING" => tweak.Category == TweakCategory.GamingGPU,
                "MEMORY" => tweak.Category == TweakCategory.MemoryRAM,
                "NETWORK" => tweak.Category == TweakCategory.NetworkPing,
                "HARDWARE" => tweak.Category == TweakCategory.HardwareMSI,
                "INPUT" => tweak.Category == TweakCategory.InputDevices,
                "PRIVACY" => tweak.Category == TweakCategory.PrivacyTelemetry,
                "SERVICES" => tweak.Category == TweakCategory.WindowsServices,
                "VISUALS" => tweak.Category == TweakCategory.VisualsUI,
                _ => true
            };

            if (!categoryMatch) return false;

            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            string query = SearchText.Trim();
            return tweak.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   tweak.TechnicalBadge.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   tweak.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   tweak.LatencyBadge.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   tweak.TechnicalDetails.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        [RelayCommand]
        private void SetCategory(string category)
        {
            SelectedCategory = category;
        }

        [RelayCommand]
        private async Task SelectPresetAsync(TweakPresetOption? option)
        {
            if (option?.ParentMasterCard == null) return;

            var master = option.ParentMasterCard;
            if (master.IsBusy) return;

            master.IsBusy = true;
            try
            {
                if (option.Index == 0 || option.UnderlyingTweak == null)
                {
                    // Revert any active member in this exclusive group back to Windows Default
                    foreach (var opt in master.PresetOptions)
                    {
                        if (opt.UnderlyingTweak is { IsApplied: true } activeTweak)
                        {
                            await Manager.RevertTweakAsync(activeTweak);
                        }
                    }

                    SyncMasterCardState(master);
                    _toastService.ShowInfo(
                        "Default Restored",
                        $"{master.Title} set to [{option.Label}].");
                }
                else
                {
                    var targetTweak = option.UnderlyingTweak;
                    bool success = await Manager.ApplyTweakAsync(targetTweak);
                    if (success)
                    {
                        SyncMasterCardState(master);
                        _toastService.ShowSuccess(
                            "Preset Activated",
                            $"{master.Title} -> [{option.Label}] applied.");
                    }
                    else
                    {
                        SyncMasterCardState(master);
                        _toastService.ShowError("Preset Error", $"Failed to apply {option.Label}.");
                    }
                }

                RefreshCounts();
            }
            finally
            {
                master.IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ToggleTweakAsync(TweakModel tweak)
        {
            if (tweak.IsBusy) return;

            bool targetState = tweak.IsApplied;

            if (targetState)
            {
                bool success = await Manager.ApplyTweakAsync(tweak);
                if (success)
                {
                    _toastService.ShowSuccess(
                        "Optimization Enabled",
                        $"{tweak.Title} ({tweak.SubTweaksCount:N0} rules) applied.");
                }
                else
                {
                    tweak.IsApplied = false;
                    _toastService.ShowError("Application Failed", $"Could not apply {tweak.Title}.");
                }
            }
            else
            {
                bool success = await Manager.RevertTweakAsync(tweak);
                if (success)
                {
                    _toastService.ShowInfo(
                        "Optimization Reverted",
                        $"{tweak.Title} restored to Windows default.");
                }
                else
                {
                    tweak.IsApplied = true;
                    _toastService.ShowError("Revert Failed", $"Could not restore defaults for {tweak.Title}.");
                }
            }

            RefreshCounts();
        }

        [RelayCommand]
        private async Task ApplySelectedAsync()
        {
            if (IsApplying) return;

            IsApplying = true;
            try
            {
                int count = await Manager.ApplySelectedAsync();
                RefreshCounts();
                _toastService.ShowSuccess(
                    "Selection Applied",
                    $"{count} modules ({AppliedSubTweaksCount:N0} engine rules) active!");
            }
            finally
            {
                IsApplying = false;
            }
        }

        [RelayCommand]
        private async Task UpdateAllTweaksAsync()
        {
            if (IsApplying) return;

            IsApplying = true;
            try
            {
                var seenGroups = new HashSet<string>();
                foreach (var tweak in Manager.Tweaks)
                {
                    if (tweak.Risk == RiskLevel.Safe)
                    {
                        if (tweak.GroupId == null || seenGroups.Add(tweak.GroupId))
                        {
                            tweak.IsSelected = true;
                        }
                        else
                        {
                            tweak.IsSelected = false;
                        }
                    }
                }

                await Manager.ApplySelectedAsync();
                RefreshCounts();
                _toastService.ShowSuccess(
                    "Recommended Profile Applied",
                    $"{AppliedCount} modules ({AppliedSubTweaksCount:N0} low-level tweaks) active!");
            }
            finally
            {
                IsApplying = false;
            }
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var tweak in Manager.Tweaks)
            {
                tweak.IsSelected = true;
            }
            _logger.LogInfo("All optimizations selected.");
            _toastService.ShowInfo("Selection", "All tweaks selected.");
        }

        [RelayCommand]
        private void DeselectAll()
        {
            foreach (var tweak in Manager.Tweaks)
            {
                tweak.IsSelected = false;
            }
            _logger.LogInfo("All optimizations deselected.");
            _toastService.ShowInfo("Selection", "All tweaks deselected.");
        }

        [RelayCommand]
        private async Task RevertAllAsync()
        {
            if (IsApplying) return;

            IsApplying = true;
            try
            {
                await Manager.RevertAllAsync();
                RefreshCounts();
                _toastService.ShowWarning("Defaults Restored", "All optimizations restored to Windows default values.");
            }
            finally
            {
                IsApplying = false;
            }
        }
    }
}
