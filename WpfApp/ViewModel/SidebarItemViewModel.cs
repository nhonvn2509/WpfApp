using CommunityToolkit.Mvvm.ComponentModel;
namespace WpfApp.ViewModel;

public partial class SidebarItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string value = string.Empty;
    [ObservableProperty]
    private int count;
    [ObservableProperty]
    private bool isSelected;
    public override string ToString() => Value;
}
