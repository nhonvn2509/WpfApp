using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfApp.ViewModel;

public sealed partial class FileExtensionFilterRowViewModel : ObservableObject
{
    public FileExtensionFilterRowViewModel(string extension, long count)
    {
        Extension = extension;
        Count = count;
    }

    public string Extension { get; }

    public string Name => Extension.StartsWith('.') ? Extension : "." + Extension;
    public long Count { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isActive;
}
