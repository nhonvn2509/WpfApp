using System.Windows.Controls;

namespace WpfApp.Views.Controls;

public sealed class BestFitButton : Button
{
    public BestFitButton()
    {
        Click += (_, _) => TableViewBestFit.ApplyToNearestTable(this);
    }
}
