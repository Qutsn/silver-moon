using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GlassPlayground;

public sealed partial class GlassPreviewWindow : Window
{
    public GlassPreviewWindow()
    {
        AvaloniaXamlLoader.Load(this);
        MaterialTunerWindow? tuner = null;
        Opened += (_, _) => { tuner = new MaterialTunerWindow(this); tuner.Show(); };
        Closed += (_, _) => tuner?.Close();
        this.FindControl<Button>("CloseButton")!.Click += (_, _) => Close();
        this.FindControl<Grid>("DragArea")!.PointerPressed += (_, e) =>
        {
            if (e.Source is Button) return;
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
    }
}
