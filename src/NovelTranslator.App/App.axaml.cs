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
                SetBrush("HeaderBgBrush", "#090D16");
                SetBrush("SidebarBgBrush", "#111827");
                SetBrush("MainBgBrush", "#0F172A");
                SetBrush("CardBgBrush", "#1E293B");
                SetBrush("EditorBgBrush", "#1E293B");
                SetBrush("BorderSubtleBrush", "#334155");
                SetBrush("TextPrimaryBrush", "#F1F5F9");
                SetBrush("TextMutedBrush", "#94A3B8");
                SetBrush("BadgeBgBrush", "#334155");
                break;

            case "Sepia":
                Current.RequestedThemeVariant = ThemeVariant.Light;
                SetBrush("HeaderBgBrush", "#3E3026");
                SetBrush("SidebarBgBrush", "#EFE5D1");
                SetBrush("MainBgBrush", "#F6EEDF");
                SetBrush("CardBgBrush", "#FBF6EC");
                SetBrush("EditorBgBrush", "#FDF9F1");
                SetBrush("BorderSubtleBrush", "#D8C9B0");
                SetBrush("TextPrimaryBrush", "#3B2F26");
                SetBrush("TextMutedBrush", "#786452");
                SetBrush("BadgeBgBrush", "#E6D8C0");
                break;

            default: // Light
                Current.RequestedThemeVariant = ThemeVariant.Light;
                SetBrush("HeaderBgBrush", "#1E293B");
                SetBrush("SidebarBgBrush", "#F1F5F9");
                SetBrush("MainBgBrush", "#F8FAFC");
                SetBrush("CardBgBrush", "#FFFFFF");
                SetBrush("EditorBgBrush", "#FFFFFF");
                SetBrush("BorderSubtleBrush", "#CBD5E1");
                SetBrush("TextPrimaryBrush", "#0F172A");
                SetBrush("TextMutedBrush", "#64748B");
                SetBrush("BadgeBgBrush", "#E2E8F0");
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