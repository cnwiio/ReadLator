using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NovelTranslator.App.Views;

public partial class AutoGlossaryDialog : Window
{
    public AutoGlossaryDialog()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
