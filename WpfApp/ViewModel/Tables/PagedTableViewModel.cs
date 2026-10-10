using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WpfApp.Models.Tables;
using WpfApp.Services.Data;
using WpfApp.Services.Tables;

namespace WpfApp.ViewModel.Tables;

public abstract partial class PagedTableViewModel<TQuery, TRow> : ObservableObject, IPagedTableViewModel
    where TRow : TableRow
{
    private readonly ITableLoadService<TQuery, TRow> _loadService;
    private readonly IDatabaseInsightsService? _insights;
    private readonly ITableRowCountProvider? _rowCounts;

    protected PagedTableViewModel(string title, ITableLoadService<TQuery, TRow> loadService)
        : this(title, loadService, TablePage.DefaultPageSize)
    {
    }

    protected PagedTableViewModel(
        string title,
        ITableLoadService<TQuery, TRow> loadService,
        int pageSize)
        : this(title, loadService, pageSize, null, null)
    {
    }

    protected PagedTableViewModel(
        string title,
        ITableLoadService<TQuery, TRow> loadService,
        int pageSize,
        IDatabaseInsightsService? insights,
        ITableRowCountProvider? rowCounts)
    {
        Title = title;
        _loadService = loadService;
        _insights = insights;
        _rowCounts = rowCounts;
        PageSize = TablePage.ClampPageSize(pageSize);
    }

    public string Title { get; }
    public ObservableCollection<TRow> Items { get; private set; } = [];
    protected abstract TQuery CreateQuery();
    protected virtual void OnPageApplied(PagedResult<TRow> result)
    {
    }

    protected virtual (string Table, string Column, string? Term)? ScanProbe => null;

    private static readonly TimeSpan SlowLoadThreshold = TimeSpan.FromSeconds(2);

    private const long LargeTableRows = 1_000_000;

    private async Task WarnIfTableScanAsync(TimeSpan elapsed, (string Table, string Column, string? Term) probe)
    {
        if (_insights is null || _rowCounts is null || elapsed < SlowLoadThreshold)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(probe.Term) || string.IsNullOrWhiteSpace(probe.Column))
        {
            return;
        }

        try
        {
            if (await _insights.IsColumnIndexedAsync(probe.Table, probe.Column).ConfigureAwait(true))
            {
                return;
            }

            var rows = await _rowCounts.GetRowCountAsync(probe.Table).ConfigureAwait(true);
            if (rows < LargeTableRows)
            {
                return;
            }

            StatusText =
                $"Tìm kiếm \"{probe.Term.Trim()}\" phải quét {rows:N0} dòng của {probe.Table} " +
                $"vì cột {probe.Column} không có index — lần này mất {elapsed.TotalSeconds:F1}s.";
        }
        catch (Exception)
        {
        }
    }

    protected Task WarnIfTableScanAsync(TimeSpan elapsed) =>
        ScanProbe is { } probe
            ? WarnIfTableScanAsync(elapsed, probe)
            : Task.CompletedTask;

    public abstract Task ApplyFilterAsync();
    public abstract Task ClearFilterAsync();
}
