using System.Globalization;
using System.IO;
using System.Text.Json;
using BruTile;
using BruTile.MbTiles;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using SQLite;
using WpfApp.Services;
using WpfApp.Models.Tables;
using VexTile.TileSource.Mvt;

namespace WpfApp.Views.Controls;

public sealed partial class LocationMapPane
{
    private LocationMapWindow? _expandedWindow;

    private static readonly HashSet<string> LatitudeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "lat",
        "latitude",
        "gpslat",
        "gpslatitude",
        "latitudedegrees",
        "latitudedeg",
        "gpslatitudedeg",
        "latdeg"
    };

    private static readonly HashSet<string> LongitudeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "lon",
        "lng",
        "long",
        "longitude",
        "gpslon",
        "gpslng",
        "gpslongitude",
        "longitudedegrees",
        "longitudedeg",
        "gpslongitudedeg",
        "londeg",
        "longdeg",
        "lngdeg"
    };

    public LocationMapPane()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => UpdateMap();
        Loaded += (_, _) => UpdateMap();
    }

    private void UpdateMap()
    {
        if (!IsLoaded || MapControl.Map is not { } map)
        {
            return;
        }

        var locations = DataContext is SessionMetadataSection section
            ? ReadLocations(section.Rows)
            : [];
        foreach (var oldTileLayer in map.Layers.OfType<TileLayer>().ToArray())
        {
            oldTileLayer.AbortFetch();
            oldTileLayer.Dispose();
            if (oldTileLayer.TileSource is IDisposable disposableSource)
            {
                disposableSource.Dispose();
            }
        }

        map.Layers.Clear();
        var (tileLayer, mapError, mapBounds) = CreateOfflineTileLayer();
        if (tileLayer is not null)
        {
            map.Layers.Add(tileLayer);
        }
        List<LocationPoint> visibleLocations;
        if (mapBounds is null)
        {
            visibleLocations = locations;
        }
        else
        {
            try
            {
                visibleLocations = FindLocationsOnMap(locations, mapBounds);
            }
            catch (SQLiteException exception)
            {
                visibleLocations = locations;
                mapError = $"Không thể kiểm tra vùng phủ bản đồ offline: {exception.Message}";
            }
        }

        var outsideLocationCount = locations.Count - visibleLocations.Count;
        var features = visibleLocations.Select(location =>
        {
            var point = SphericalMercator.FromLonLat(location.Longitude, location.Latitude).ToMPoint();
            var feature = new PointFeature(point);
            feature["latitude"] = location.Latitude;
            feature["longitude"] = location.Longitude;
            return feature;
        }).ToArray();
        var locationLayer = new MemoryLayer
        {
            Name = "Vị trí",
            IsMapInfoLayer = true,
            Features = features,
            Style = new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                SymbolScale = 1.4,
                Fill = new Brush(new Mapsui.Styles.Color(220, 53, 69)),
                Outline = new Pen(new Mapsui.Styles.Color(255, 255, 255), 2)
            }
        };
        map.Layers.Add(locationLayer);
        if (features.Length > 0)
        {
            map.Home = navigator => SetInitialMapView(navigator, features);
            map.Home(map.Navigator);
        }

        MapControl.Visibility = tileLayer is not null || locations.Count > 0
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
        var allLocationsOutsideMap = locations.Count > 0 && visibleLocations.Count == 0 && mapBounds is not null;
        MapStatusText.Text = mapError
            ?? (outsideLocationCount > 0 && !allLocationsOutsideMap
                ? $"Đã ẩn {outsideLocationCount} vị trí nằm ngoài phạm vi bản đồ offline đã tải."
                : string.Empty);
        MapStatusText.Visibility = string.IsNullOrWhiteSpace(MapStatusText.Text)
            ? System.Windows.Visibility.Collapsed
            : System.Windows.Visibility.Visible;
        EmptyLocationText.Text = allLocationsOutsideMap
            ? "Vị trí nằm ngoài phạm vi bản đồ offline đã tải."
            : "Không tìm thấy cặp tọa độ vĩ độ/kinh độ trong dữ liệu vị trí.";
        EmptyLocationText.Visibility = (allLocationsOutsideMap || locations.Count == 0) && mapError is null
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
    }

    private void ExpandMap_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_expandedWindow is { IsVisible: true })
        {
            _expandedWindow.DataContext = DataContext;
            _expandedWindow.Activate();
            return;
        }

        _expandedWindow = new LocationMapWindow
        {
            Owner = System.Windows.Window.GetWindow(this),
            DataContext = DataContext
        };
        _expandedWindow.Closed += (_, _) => _expandedWindow = null;
        _expandedWindow.Show();
    }

    private static (TileLayer? Layer, string? Error, MapBounds? Bounds) CreateOfflineTileLayer()
    {
        const string mapFileName = "vietnam-south-china-sea-shortbread-1.0.mbtiles";
        var mapPath = FindSourceTreeMap(mapFileName);
        if (mapPath is null)
        {
            return (null, $"Không tìm thấy tệp bản đồ offline trong source: WpfApp\\Assets\\Maps\\{mapFileName}", null);
        }

        try
        {
            var connectionString = new SQLiteConnectionString(mapPath, storeDateTimeAsTicks: false);
            using var connection = new SQLiteConnection(mapPath, SQLiteOpenFlags.ReadOnly, false);
            var format = connection.ExecuteScalar<string>(
                "SELECT \"value\" FROM metadata WHERE \"name\"=?;",
                "format");
            var boundsValue = connection.ExecuteScalar<string>(
                "SELECT \"value\" FROM metadata WHERE \"name\"=?;",
                "bounds");
            if (!TryParseMapBounds(boundsValue, out var bounds) || bounds is null)
            {
                return (null, "Tệp bản đồ offline thiếu thông tin phạm vi hợp lệ.", null);
            }

            var maxZoomValue = connection.ExecuteScalar<string>(
                "SELECT \"value\" FROM metadata WHERE \"name\"=?;",
                "maxzoom");
            if (!int.TryParse(maxZoomValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxZoom) ||
                maxZoom is < 0 or > 30)
            {
                return (null, "Tệp bản đồ offline thiếu thông tin mức phóng đại hợp lệ.", null);
            }

            bounds = bounds with { MapPath = mapPath, MaxZoom = maxZoom };

            ITileSource source;
            if (string.Equals(format, "pbf", StringComparison.OrdinalIgnoreCase))
            {
                var stylePath = Path.Combine(AppContext.BaseDirectory, "Assets", "MapStyles", "shortbread-vi.json");
                if (!File.Exists(stylePath))
                {
                    return (null, $"Không tìm thấy kiểu bản đồ tiếng Việt: {stylePath}", null);
                }

                source = new ShortbreadMbTilesTileSource(connectionString, File.ReadAllText(stylePath));
            }
            else
            {
                source = new MbTilesTileSource(connectionString);
            }

            return (new TileLayer(source), null, bounds);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or ArgumentException
                                          or InvalidOperationException
                                          or KeyNotFoundException
                                          or JsonException
                                          or SQLiteException)
        {
            return (null, $"Không thể mở bản đồ MBTiles: {exception.Message}", null);
        }
    }

    private static List<LocationPoint> FindLocationsOnMap(
        IReadOnlyList<LocationPoint> locations,
        MapBounds bounds)
    {
        using var connection = new SQLiteConnection(bounds.MapPath, SQLiteOpenFlags.ReadOnly, false);
        return locations.Where(location => bounds.ContainsTile(connection, location)).ToList();
    }

    private static bool TryParseMapBounds(string? value, out MapBounds? bounds)
    {
        bounds = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split(',');
        if (parts.Length != 4 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var west) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var south) ||
            !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var east) ||
            !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var north) ||
            west is < -180 or > 180 ||
            east is < -180 or > 180 ||
            south is < -90 or > 90 ||
            north is < -90 or > 90 ||
            west > east ||
            south > north)
        {
            return false;
        }

        bounds = new MapBounds(west, south, east, north, string.Empty, 0);
        return true;
    }

    private static string? FindSourceTreeMap(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var mapPath = Path.Combine(directory.FullName, "Assets", "Maps", fileName);
            if (File.Exists(mapPath))
            {
                return mapPath;
            }
        }

        return null;
    }

    private static void SetInitialMapView(Navigator navigator, IReadOnlyList<PointFeature> features)
    {
        if (features.Count == 0)
        {
            return;
        }

        var points = features.Select(feature => feature.Point).ToArray();
        var minX = points.Min(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxX = points.Max(point => point.X);
        var maxY = points.Max(point => point.Y);

        var padding = Math.Max(Math.Max(maxX - minX, maxY - minY) * 0.1, 250);
        navigator.ZoomToBox(new MRect(minX, minY, maxX, maxY).Grow(padding, padding));
    }

    private static List<LocationPoint> ReadLocations(IReadOnlyList<Dictionary<string, string>> rows)
    {
        var locations = new List<LocationPoint>();
        var unpairedLatitudes = new List<double>();
        var unpairedLongitudes = new List<double>();

        foreach (var row in rows)
        {
            var latitude = FindCoordinate(row, LatitudeKeys, true);
            var longitude = FindCoordinate(row, LongitudeKeys, false);
            if (latitude is { } lat && longitude is { } lon)
            {
                locations.Add(new LocationPoint(lat, lon));
            }
            else
            {
                if (latitude is { } unpairedLatitude)
                {
                    unpairedLatitudes.Add(unpairedLatitude);
                }

                if (longitude is { } unpairedLongitude)
                {
                    unpairedLongitudes.Add(unpairedLongitude);
                }
            }
        }

        if (unpairedLatitudes.Count == unpairedLongitudes.Count)
        {
            locations.AddRange(unpairedLatitudes.Zip(
                unpairedLongitudes,
                (latitude, longitude) => new LocationPoint(latitude, longitude)));
        }

        return locations.Distinct().ToList();
    }

    private static double? FindCoordinate(
        IReadOnlyDictionary<string, string> row,
        IReadOnlySet<string> candidateKeys,
        bool isLatitude)
    {
        foreach (var pair in row)
        {
            var normalizedKey = string.Concat(pair.Key.Where(char.IsLetterOrDigit));
            if (!candidateKeys.Contains(normalizedKey) &&
                !candidateKeys.Any(key => normalizedKey.EndsWith(key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (TryParseCoordinate(pair.Value, isLatitude, out var coordinate))
            {
                return coordinate;
            }
        }

        return null;
    }

    private static bool TryParseCoordinate(string value, bool isLatitude, out double coordinate)
    {
        coordinate = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        var direction = char.ToUpperInvariant(text[^1]);
        var hasDirection = direction is 'N' or 'S' or 'E' or 'W';
        if (hasDirection)
        {
            text = text[..^1].Trim();
        }

        text = text.TrimEnd('°').Trim();
        var parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out coordinate) ||
                     double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out coordinate);
        if (!parsed)
        {
            return false;
        }

        if (hasDirection && direction is 'S' or 'W')
        {
            coordinate = -Math.Abs(coordinate);
        }

        return double.IsFinite(coordinate) &&
               (isLatitude
                   ? coordinate is >= -90 and <= 90
                   : coordinate is >= -180 and <= 180);
    }

    private sealed record LocationPoint(double Latitude, double Longitude);

    private sealed record MapBounds(
        double West,
        double South,
        double East,
        double North,
        string MapPath,
        int MaxZoom)
    {
        public bool ContainsTile(SQLiteConnection connection, LocationPoint point)
        {
            if (point.Longitude < West ||
                point.Longitude > East ||
                point.Latitude < South ||
                point.Latitude > North)
            {
                return false;
            }

            var tileCount = 1 << MaxZoom;
            var latitude = Math.Clamp(point.Latitude, -85.05112878, 85.05112878);
            var x = (int)Math.Clamp(
                Math.Floor((point.Longitude + 180) / 360 * tileCount),
                0,
                tileCount - 1);
            var y = (int)Math.Clamp(
                Math.Floor(
                    (1 - Math.Asinh(Math.Tan(latitude * Math.PI / 180)) / Math.PI) /
                    2 * tileCount),
                0,
                tileCount - 1);
            var tmsRow = tileCount - 1 - y;

            return connection.ExecuteScalar<int>(
                "SELECT EXISTS(SELECT 1 FROM tiles WHERE zoom_level=? AND tile_column=? AND tile_row=?);",
                MaxZoom,
                x,
                tmsRow) != 0;
        }
    }
}
