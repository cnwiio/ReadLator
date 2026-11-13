using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using NovelTranslator.Core.Llm;
using NovelTranslator.Core.Settings;

namespace NovelTranslator.App.Views;

public partial class FirstRunWindow : Window
{
    private readonly AppSettings _settings;

    public bool SettingsUpdated { get; private set; }

    public FirstRunWindow() : this(new AppSettings())
    {
    }

    public FirstRunWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnOpenOllamaClicked(object? sender, RoutedEventArgs e) => OpenUrl("https://ollama.com/download");

    private void OnOpenLmStudioClicked(object? sender, RoutedEventArgs e) => OpenUrl("https://lmstudio.ai");

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // ข้ามหากระบบไม่มีเบราว์เซอร์เริ่มต้น
        }
    }

    private async void OnRecheckClicked(object? sender, RoutedEventArgs e)
    {
        RecheckButton.IsEnabled = false;
        ProbeStatusText.IsVisible = true;
        ProbeStatusText.Foreground = Brush.Parse("#6B7280");
        ProbeStatusText.Text = "กำลังตรวจสอบ Ollama และ LM Studio...";

        try
        {
            var (backend, baseUrl, firstModel) = await LlmClient.ProbeLocalBackendsAsync();
            if (backend.HasValue)
            {
                _settings.Backend = backend.Value;
                _settings.BaseUrl = baseUrl;
                if (!string.IsNullOrWhiteSpace(firstModel))
                {
                    _settings.ModelName = firstModel;
                }
                _settings.HasCompletedFirstRun = true;
                _settings.Save();
                SettingsUpdated = true;
                Close();
                return;
            }

            ProbeStatusText.Foreground = Brush.Parse("#EF4444");
            ProbeStatusText.Text = "ยังไม่พบ Ollama (localhost:11434) หรือ LM Studio (localhost:1234) กรุณาเปิดโปรแกรมก่อนแล้วลองอีกครั้ง";
        }
        finally
        {
            RecheckButton.IsEnabled = true;
        }
    }

    private void OnSaveCloudClicked(object? sender, RoutedEventArgs e)
    {
        string baseUrl = CloudBaseUrlBox.Text?.Trim() ?? string.Empty;
        string apiKey = CloudApiKeyBox.Text?.Trim() ?? string.Empty;
        string model = CloudModelBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            ProbeStatusText.IsVisible = true;
            ProbeStatusText.Foreground = Brush.Parse("#EF4444");
            ProbeStatusText.Text = "กรุณากรอก Base URL และ API Key สำหรับ Cloud AI";
            return;
        }

        _settings.Backend = BackendType.Custom;
        _settings.BaseUrl = baseUrl;
        _settings.ApiKey = apiKey;
        if (!string.IsNullOrWhiteSpace(model))
        {
            _settings.ModelName = model;
        }
        _settings.HasCompletedFirstRun = true;
        _settings.Save();
        SettingsUpdated = true;
        Close();
    }

    private void OnSkipClicked(object? sender, RoutedEventArgs e)
    {
        _settings.HasCompletedFirstRun = true;
        _settings.Save();
        Close();
    }
}