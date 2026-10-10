using CommunityToolkit.Mvvm.ComponentModel;
namespace WpfApp.Models.Tables;

public abstract partial class TableRow : ObservableObject
{
    public abstract string SelectionKey { get; }
    [ObservableProperty]
    private bool _isSelected;
}
