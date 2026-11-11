using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using NovelTranslator.App.ViewModels;
using NovelTranslator.App.Views;
using NovelTranslator.Core.Settings;

namespace NovelTranslator.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var settings = AppSettings.Load();
        ApplyTheme(settings.ThemeMode);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyTheme(string? mode)
    {
        if (Current == null) return;

        switch (mode)
        {
            case "Dark":
                Current.RequestedThemeVariant = ThemeVariant.Dark;
                SetBrush("HeaderBgBrush", "#0F0F10");
                SetBrush("SidebarBgBrush", "#0F0F10");
                SetBrush("MainBgBrush", "#141415");
                SetBrush("CardBgBrush", "#19191B");
                SetBrush("EditorBgBrush", "#19191B");
                SetBrush("RaisedBgBrush", "#232325");
                SetBrush("BorderSubtleBrush", "#27272A");
                SetBrush("TextPrimaryBrush", "#EDEDED");
                SetBrush("TextSecondaryBrush", "#A1A1A6");
                SetBrush("TextMutedBrush", "#85858B");
                SetBrush("BadgeBgBrush", "#232325");
                break;

            case "Sepia":
                Current.RequestedThemeVariant = ThemeVariant.Light;
                SetBrush("HeaderBgBrush", "#EFE9DF");
                SetBrush("SidebarBgBrush", "#EFE9DF");
                SetBrush("MainBgBrush", "#F7F4EE");
                SetBrush("CardBgBrush", "#FDFBF7");
                SetBrush("EditorBgBrush", "#FDFBF7");
                SetBrush("RaisedBgBrush", "#EFE9DF");
                SetBrush("BorderSubtleBrush", "#E0D8CA");
                SetBrush("TextPrimaryBrush", "#1F1B16");
                SetBrush("TextSecondaryBrush", "#5E554A");
                SetBrush("TextMutedBrush", "#7B7266");
                SetBrush("BadgeBgBrush", "#EFE9DF");
                break;

            default: // Light Graphite
                Current.RequestedThemeVariant = ThemeVariant.Light;
                SetBrush("HeaderBgBrush", "#F4F4F5");
                SetBrush("SidebarBgBrush", "#F4F4F5");
                SetBrush("MainBgBrush", "#FAFAFA");
                SetBrush("CardBgBrush", "#FFFFFF");
                SetBrush("EditorBgBrush", "#FFFFFF");
                SetBrush("RaisedBgBrush", "#F4F4F5");
                SetBrush("BorderSubtleBrush", "#E4E4E7");
                SetBrush("TextPrimaryBrush", "#171717");
                SetBrush("TextSecondaryBrush", "#525252");
                SetBrush("TextMutedBrush", "#737373");
                SetBrush("BadgeBgBrush", "#F4F4F5");
                break;
        }
    }

    private static void SetBrush(string key, string hex)
    {
        if (Current != null)
        {
            Current.Resources[key] = SolidColorBrush.Parse(hex);
        }
    }
}