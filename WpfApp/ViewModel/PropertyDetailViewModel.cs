using System.Text;
using WpfApp.Models;
namespace WpfApp.ViewModel;

public sealed class PropertyDetailViewModel
{
    public PropertyDetailViewModel(string title, string? subtitle, IReadOnlyList<PropertyDetailGroup> groups)
    {
        Title = title;
        Subtitle = subtitle;
        Groups = groups;
    }
    public string Title { get; }
    public string? Subtitle { get; }
    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);
    public IReadOnlyList<PropertyDetailGroup> Groups { get; }
    public string BuildText()
    {
        var builder = new StringBuilder();
        builder.AppendLine(Title);
        if (HasSubtitle)
        {
            builder.AppendLine(Subtitle);
        }
        foreach (var group in Groups)
        {
            builder.AppendLine();
            builder.AppendLine($"[{group.Name}]");
            foreach (var item in group.Items)
            {
                builder.AppendLine($"{item.Name}: {item.Value}");
            }
        }
        return builder.ToString();
    }
}
