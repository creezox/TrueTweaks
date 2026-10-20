using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TrueTweaks.Models;

namespace TrueTweaks.Services
{
    public interface ITweaksManagerService
    {
        ObservableCollection<TweakModel> Tweaks { get; }
        int AppliedCount { get; }
        int TotalCount { get; }
        int AppliedSubTweaksCount { get; }
        int TotalSubTweaksCount { get; }

        Task InitializeTweaksAsync();
        Task<bool> ApplyTweakAsync(TweakModel tweak);
        Task<bool> RevertTweakAsync(TweakModel tweak);
        Task<int> ApplySelectedAsync();
        Task<int> RevertAllAsync();
    }
}
