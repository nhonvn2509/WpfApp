using WpfApp.Models;
using WpfApp.Models.Tables;

namespace WpfApp.Services.Files;

public sealed class FileDetailService : IFileDetailService
{
    private readonly IFileDetailRepository _repository;
    private readonly IFilePathResolver _pathResolver;
    private readonly IFileDetailReportBuilder _reportBuilder;

    public FileDetailService(
        IFileDetailRepository repository,
        IFilePathResolver pathResolver,
        IFileDetailReportBuilder reportBuilder)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(pathResolver);
        ArgumentNullException.ThrowIfNull(reportBuilder);
        _repository = repository;
        _pathResolver = pathResolver;
        _reportBuilder = reportBuilder;
    }

    public async Task<PropertyDetailReport> GetAsync(
        FileRow row,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        var record = await _repository.GetAsync(row, cancellationToken).ConfigureAwait(false);
        var fullPath = _pathResolver.GetFullPath(row);
        return _reportBuilder.Build(row, record, fullPath);
    }
}
