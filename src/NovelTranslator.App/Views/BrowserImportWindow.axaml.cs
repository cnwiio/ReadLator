using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NovelTranslator.Core.Export;

namespace NovelTranslator.App.Views;

public partial class BrowserImportWindow : Window
{
    private readonly string? _initialUrl;
    private readonly Func<WebChapterResult, Task> _onImported;
    private bool _isBusy;

    public BrowserImportWindow() : this(null, _ => Task.CompletedTask) { }

    public BrowserImportWindow(string? initialUrl, Func<WebChapterResult, Task> onImported)
    {
        _initialUrl = initialUrl;
        _onImported = onImported;
        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(initialUrl))
        {
            UrlTextBox.Text = initialUrl;
        }

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        WebView.CoreWebView2Ready += OnCoreWebView2Ready;
        try
        {
            await WebView.InitializeAsync(_initialUrl);
            if (string.IsNullOrWhiteSpace(_initialUrl))
            {
                StatusText.Text = "พร้อมใช้งาน — วาง URL ด้านบนแล้วกด Enter เพื่อเปิดหน้าเว็บ";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"ไม่สามารถเปิด WebView2 ได้ (กรุณาตรวจสอบ Microsoft Edge WebView2 Runtime): {ex.Message}";
        }
    }

    private void OnCoreWebView2Ready()
    {
        var core = WebView.CoreWebView2;
        if (core == null) return;

        core.SourceChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            UrlTextBox.Text = core.Source;
            UpdateNavButtons();
        });

        core.NavigationStarting += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            StatusText.Text = "กำลังโหลดหน้าเว็บ...";
            UpdateNavButtons();
        });

        core.NavigationCompleted += (_, args) => Dispatcher.UIThread.Post(() =>
        {
            UpdateNavButtons();
            StatusText.Text = args.IsSuccess
                ? $"โหลดเสร็จแล้ว: {core.DocumentTitle}"
                : $"โหลดหน้าเว็บมีปัญหา (รหัส {args.WebErrorStatus})";
        });
    }

    private void UpdateNavButtons()
    {
        var core = WebView.CoreWebView2;
        BackButton.IsEnabled = core?.CanGoBack == true;
        ForwardButton.IsEnabled = core?.CanGoForward == true;
    }

    private void OnBackClicked(object? sender, RoutedEventArgs e) => WebView.CoreWebView2?.GoBack();
    private void OnForwardClicked(object? sender, RoutedEventArgs e) => WebView.CoreWebView2?.GoForward();
    private void OnReloadClicked(object? sender, RoutedEventArgs e) => WebView.CoreWebView2?.Reload();

    private void OnUrlKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            WebView.Navigate(UrlTextBox.Text ?? string.Empty);
        }
    }

    private async void OnImportKeepOpenClicked(object? sender, RoutedEventArgs e)
        => await ImportCurrentPageAsync(closeAfterImport: false);

    private async void OnImportAndCloseClicked(object? sender, RoutedEventArgs e)
        => await ImportCurrentPageAsync(closeAfterImport: true);

    private async Task ImportCurrentPageAsync(bool closeAfterImport)
    {
        if (_isBusy) return;
        _isBusy = true;
        KeepOpenButton.IsEnabled = false;
        ImportCloseButton.IsEnabled = false;

        try
        {
            StatusText.Text = "กำลังดึงเนื้อหาจากหน้าเว็บ (Reader Mode)...";
            string currentUrl = WebView.CoreWebView2?.Source ?? UrlTextBox.Text ?? "https://localhost/";
            string html = await WebView.GetPageHtmlAsync();
            var result = WebChapterImportService.ExtractFromHtml(currentUrl, html);

            await _onImported(result);

            if (closeAfterImport)
            {
                Close();
                return;
            }

            StatusText.Text = $"นำเข้า \"{result.Title}\" แล้ว — กดตอนถัดไปบนหน้าเว็บเพื่อดึงต่อได้";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"ดึงเนื้อหาไม่สำเร็จ: {ex.Message}";
        }
        finally
        {
            _isBusy = false;
            KeepOpenButton.IsEnabled = true;
            ImportCloseButton.IsEnabled = true;
        }
    }
}
