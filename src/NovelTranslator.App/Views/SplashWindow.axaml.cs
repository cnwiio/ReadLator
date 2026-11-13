using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NovelTranslator.App.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = ver != null ? $"v{ver.Major}.{ver.Minor}" : string.Empty;
    }

    public void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    public void ShowError(string message)
    {
        LoadingPanel.IsVisible = false;
        ErrorText.Text = message;
        ErrorPanel.IsVisible = true;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
