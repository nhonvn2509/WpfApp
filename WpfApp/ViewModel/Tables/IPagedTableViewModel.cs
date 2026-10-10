using System.ComponentModel;
namespace WpfApp.ViewModel.Tables;

public interface IPagedTableViewModel : INotifyPropertyChanged
{
    string Title { get; }
    bool IsActive { get; set; }
    bool IsLoaded { get; }
    bool IsLoading { get; }
    string? ErrorMessage { get; }
    string StatusText { get; }
    bool CanSelect { get; set; }
    int SelectedCount { get; }
    string SelectionText { get; }
    string PageText { get; }
    int PageIndex { get; }
    int PageSize { get; }

    long TotalCount { get; }

    bool IsTotalCountKnown { get; }

    int FirstRowNumber { get; }

    int LastRowNumber { get; }
    int PaginationMaxPageCount { get; }
    Task EnsureLoadedAsync();
    Task ApplyFilterAsync();
    Task ClearFilterAsync();
    void SelectAllRows();
    void ClearSelectedRows();
    Task RefreshAsync();
    Task FirstPageAsync();
    Task PreviousPageAsync();
    Task NextPageAsync();
    Task LastPageAsync();
}
