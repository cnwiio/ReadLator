using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using NovelTranslator.App.ViewModels;
using NovelTranslator.App.Views;
using NovelTranslator.Core.Data;
using NovelTranslator.Core.Llm;
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
            var splash = new SplashWindow();
            desktop.MainWindow = splash;
            splash.Show();

            _ = InitializeMainWindowAsync(desktop, splash, settings);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task InitializeMainWindowAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        SplashWindow splash,
        AppSettings settings)
    {
        // Let Avalonia paint the splash window before running heavy initialization
        await Task.Delay(20);

        try
        {
            splash.SetStatus("กำลังเชื่อมต่อฐานข้อมูล...");
            var database = await Task.Run(() =>
            {
                var db = new NovelDatabase();
                db.Initialize();
                return db;
            });

            splash.SetStatus("กำลังเตรียมหน้าต่างหลัก...");
            await Task.Yield();
            var viewModel = new MainViewModel(settings, database, deferInitialLoad: true);
            var mainWindow = new MainWindow { DataContext = viewModel };

            splash.SetStatus("กำลังโหลดรายการนิยาย...");
            await viewModel.LoadInitialDataAsync();

            desktop.MainWindow = mainWindow;
            mainWindow.Show();
            splash.Close();

            if (!settings.HasCompletedFirstRun && string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                var (backend, baseUrl, firstModel) = await LlmClient.ProbeLocalBackendsAsync();
                if (backend.HasValue)
                {
                    settings.Backend = backend.Value;
                    settings.BaseUrl = baseUrl;
                    if (string.IsNullOrWhiteSpace(settings.ModelName) && !string.IsNullOrWhiteSpace(firstModel))
                    {
                        settings.ModelName = firstModel;
                    }
                    settings.HasCompletedFirstRun = true;
                    settings.Save();
                    viewModel.ReloadSettings();
                }
                else
                {
                    var firstRun = new FirstRunWindow(settings);
                    await firstRun.ShowDialog(mainWindow);
                    if (firstRun.SettingsUpdated)
                    {
                        viewModel.ReloadSettings();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            splash.ShowError($"ไม่สามารถเริ่มโปรแกรมได้:\n{ex.Message}");
        }
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