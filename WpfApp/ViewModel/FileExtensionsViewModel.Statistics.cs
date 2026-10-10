using WpfApp.Models.Files;
using WpfApp.Models.Tables;
using WpfApp.Services.Tables;

namespace WpfApp.ViewModel;

public sealed partial class FileExtensionsViewModel
{
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private CancellationTokenSource? _refreshCancellation;

    public async Task RefreshForChainsAsync(
        IReadOnlyList<int> chainIds,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        _refreshCancellation?.Cancel();
        using var refreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _refreshCancellation = refreshCancellation;
        var gateEntered = false;
        try
        {
            await _refreshGate.WaitAsync(refreshCancellation.Token);
            gateEntered = true;
            IsRefreshing = true;
            var previousSelectedExtensions = SelectedExtensions;
            var statistics = await LoadStatisticsAsync(
                chainIds,
                fromDate,
                toDate,
                refreshCancellation.Token);
            ApplyExtensionCounts(statistics.Counts, previousSelectedExtensions);
            StatusText = statistics.Counts.Count == 0
                ? "Không có đuôi file nào trong danh mục."
                : $"Danh mục có {statistics.Counts.Count:N0} đuôi file; số bên cạnh là session có ít nhất một tệp thuộc đuôi đó.";
        }
        catch (OperationCanceledException) when (refreshCancellation.IsCancellationRequested)
        {
            if (ReferenceEquals(_refreshCancellation, refreshCancellation))
            {
                StatusText = "Đã hủy khai thác trước.";
            }
        }
        catch (Exception exception)
        {
            StatusText = $"Không tính được đuôi file: {exception.Message}";
            throw;
        }
        finally
        {
            if (ReferenceEquals(_refreshCancellation, refreshCancellation))
            {
                IsRefreshing = false;
                _refreshCancellation = null;
            }

            if (gateEntered)
            {
                _refreshGate.Release();
            }
        }
    }

    public void ApplyStatistics(
        FileExtensionStatistics statistics,
        IReadOnlyCollection<string>? selectedExtensions = null)
    {
        ApplyExtensionCounts(statistics.Counts, selectedExtensions ?? SelectedExtensions);
        StatusText = statistics.Counts.Count == 0
            ? "Danh mục không có đuôi file nào."
            : $"Danh mục có {statistics.Counts.Count:N0} đuôi file; số bên cạnh là session có ít nhất một tệp thuộc đuôi đó.";
    }

    public async Task<FileExtensionStatistics> LoadStatisticsAsync(
        IReadOnlyList<int> chainIds,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        var statisticsQuery = new FilesQuery(
            TablePage.First(TablePage.MaxPageSize),
            Search: null,
            FromDate: fromDate,
            ToDate: toDate,
            ChainIds: chainIds,
            Extensions: []);
        return chainIds.Count == 0
            ? FileExtensionStatistics.Empty
            : await _loadService.LoadExtensionStatisticsAsync(statisticsQuery, cancellationToken);
    }
}
