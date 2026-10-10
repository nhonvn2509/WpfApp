using BruTile;
using BruTile.MbTiles;
using SQLite;
using VexTile.Common.Enums;
using VexTile.Data.Sources;
using VexTile.Renderer.Mvt.AliFlux;
using VexTile.Renderer.Mvt.AliFlux.Sources;
using TileInfo = BruTile.TileInfo;

namespace WpfApp.Views.Controls;

internal sealed class ShortbreadMbTilesTileSource : ITileSource, IDisposable
{
    private readonly MbTilesTileSource _metadataSource;
    private readonly SqliteDataSource _dataSource;
    private readonly VectorStyle _style;

    public ShortbreadMbTilesTileSource(SQLiteConnectionString connectionString, string style)
    {
        _metadataSource = new MbTilesTileSource(connectionString);
        _dataSource = new SqliteDataSource(connectionString);
        _style = new VectorStyle(VectorStyleKind.Custom, customStyle: style);
        _style.SetSourceProvider("shortbread", new VectorTilesSource(_dataSource));
    }

    public ITileSchema Schema => _metadataSource.Schema;
    public string Name => _metadataSource.Name;
    public Attribution Attribution
    {
        get => _metadataSource.Attribution;
        set => _metadataSource.Attribution = value;
    }

    public MbTilesType Type => _metadataSource.Type;
    public string Version => _metadataSource.Version;
    public string Description => _metadataSource.Description;
    public string Json => _metadataSource.Json;
    public string Compression => _metadataSource.Compression;

    public async Task<byte[]?> GetTileAsync(TileInfo tileInfo)
    {
        var index = tileInfo.Index;
        return await TileRendererFactory.RenderAsync(
            _style,
            new SkiaCanvas(),
            index.Col,
            index.Row,
            index.Level,
            256,
            256,
            1);
    }

    public void Dispose() => _dataSource.Dispose();
}
