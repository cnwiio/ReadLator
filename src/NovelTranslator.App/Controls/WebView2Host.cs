using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Web.WebView2.Core;

namespace NovelTranslator.App.Controls;

public sealed class WebView2Host : NativeControlHost
{
    private IPlatformHandle? _hostHandle;
    private CoreWebView2Controller? _controller;
    private string? _tempUserDataDir;
    private bool _isInitializing;

    public CoreWebView2? CoreWebView2 => _controller?.CoreWebView2;
    public event Action? CoreWebView2Ready;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _hostHandle = base.CreateNativeControlCore(parent);
        return _hostHandle;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        if (_controller != null)
        {
            _controller.Close();
            _controller = null;
        }

        CleanupTempUserData();
        _hostHandle = null;
        base.DestroyNativeControlCore(control);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        UpdateBounds(arranged);
        return arranged;
    }

    public async Task InitializeAsync(string? initialUrl = null)
    {
        if (_controller != null || _isInitializing || _hostHandle == null) return;
        _isInitializing = true;

        try
        {
            _tempUserDataDir = Path.Combine(
                Path.GetTempPath(),
                "NovelTranslator_WebView2_" + Guid.NewGuid().ToString("N"));

            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: _tempUserDataDir);

            var options = env.CreateCoreWebView2ControllerOptions();
            options.IsInPrivateModeEnabled = true;

            _controller = await env.CreateCoreWebView2ControllerAsync(_hostHandle.Handle, options);
            UpdateBounds(Bounds.Size);
            _controller.IsVisible = true;

            CoreWebView2Ready?.Invoke();

            if (!string.IsNullOrWhiteSpace(initialUrl))
            {
                Navigate(initialUrl);
            }
        }
        finally
        {
            _isInitializing = false;
        }
    }

    public void Navigate(string url)
    {
        if (_controller?.CoreWebView2 == null || string.IsNullOrWhiteSpace(url)) return;

        string target = url.Trim();
        if (!target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            target = "https://" + target;
        }

        if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            _controller.CoreWebView2.Navigate(uri.ToString());
        }
    }

    public async Task<string> GetPageHtmlAsync()
    {
        if (_controller?.CoreWebView2 == null)
        {
            throw new InvalidOperationException("เบราว์เซอร์ยังไม่พร้อมใช้งาน");
        }

        string json = await _controller.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML");
        return JsonSerializer.Deserialize<string>(json) ?? string.Empty;
    }

    private void UpdateBounds(Size dipSize)
    {
        if (_controller == null) return;
        double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        int w = Math.Max(1, (int)Math.Round(dipSize.Width * scale));
        int h = Math.Max(1, (int)Math.Round(dipSize.Height * scale));
        _controller.Bounds = new System.Drawing.Rectangle(0, 0, w, h);
    }

    private void CleanupTempUserData()
    {
        if (string.IsNullOrEmpty(_tempUserDataDir)) return;
        string dir = _tempUserDataDir;
        _tempUserDataDir = null;

        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
                    return;
                }
                catch
                {
                    await Task.Delay(400);
                }
            }
        });
    }
}
