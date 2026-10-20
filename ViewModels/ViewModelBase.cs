using CommunityToolkit.Mvvm.ComponentModel;

namespace TrueTweaks.ViewModels
{
    public abstract class ViewModelBase : ObservableObject
    {
        public abstract string Title { get; }
    }
}
