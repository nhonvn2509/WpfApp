using System.IO;
using System.Text.Json;

namespace WpfApp.Services;

public sealed class JsonAppSettings : IAppSettings
{
    private const string ConnectionStringsSection = "ConnectionStrings";
    private const string ConnectionStringName = "DeltaBat";
    private const string ConnectionStringVariable = "DELTABAT_CONNECTION_STRING";
    private readonly Lazy<JsonElement?> _root;
    private readonly string _filePath;

    public JsonAppSettings(string? filePath = null)
    {
        _filePath = filePath is null
            ? Path.Combine(AppContext.BaseDirectory, "appsettings.json")
            : Path.GetFullPath(filePath);
        _root = new Lazy<JsonElement?>(LoadRoot);
    }

    public string SettingsFileName => Path.GetFileName(_filePath);

    public string ConnectionString
    {
        get
        {
            var fromEnvironment = Environment.GetEnvironmentVariable(ConnectionStringVariable);
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                return fromEnvironment;
            }

            var fromFile = GetString(ConnectionStringsSection, ConnectionStringName);
            if (!string.IsNullOrWhiteSpace(fromFile))
            {
                return fromFile;
            }

            throw new InvalidOperationException(
                $"Không tìm thấy '{ConnectionStringsSection}:{ConnectionStringName}' trong '{_filePath}'. " +
                $"Hãy bổ sung cấu hình hoặc đặt biến môi trường {ConnectionStringVariable}.");
        }
    }

    public string ConnectionStringSource =>
        HasEnvironmentConnectionString()
            ? $"Biến môi trường {ConnectionStringVariable}"
            : $"Tệp kèm theo ({Path.GetFileName(_filePath)})";

    private bool HasEnvironmentConnectionString() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringVariable));

    public string? GetString(params string[] path)
    {
        if (_root.Value is not { } root || path.Length == 0)
        {
            return null;
        }

        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(segment, out var next))
            {
                return null;
            }

            current = next;
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    public IReadOnlyDictionary<string, string>? GetSection(params string[] path)
    {
        if (_root.Value is not { } root || path.Length == 0)
        {
            return null;
        }

        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(segment, out var next))
            {
                return null;
            }

            current = next;
        }

        if (current.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var section = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in current.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                section[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return section;
    }

    private JsonElement? LoadRoot()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        using var document = JsonDocument.Parse(
            File.ReadAllText(_filePath),
            new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        return document.RootElement.Clone();
    }
}
