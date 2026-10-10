using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfApp.Models.Tables;
using WpfApp.Services.Tables;
namespace WpfApp.ViewModel.Tables;

public sealed partial class ContactsTableViewModel : PagedTableViewModel<ContactsQuery, ContactRow>
{
    public ContactsTableViewModel(IContactsLoadService loadService)
        : base("Liên hệ", loadService)
    {
    }
    [ObservableProperty]
    private string _searchText = string.Empty;
    [ObservableProperty]
    private long? _sessionId;
    protected override ContactsQuery CreateQuery() =>
        new(new TablePage(PageIndex, PageSize), SearchText, SessionId);
    [RelayCommand]
    public override Task ApplyFilterAsync() => ResetAsync();
    [RelayCommand]
    public override async Task ClearFilterAsync()
    {
        SearchText = string.Empty;
        SessionId = null;
        await ResetAsync();
    }
}
