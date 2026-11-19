using System;
using System.Collections.Generic;
using System.Threading;
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
    private CancellationTokenSource? _importCts;
    private TaskCompletionSource<bool>? _pendingNavTcs;

    public BrowserImportWindow() : this((WebMultiImportOptions?)null, _ => Task.CompletedTask) { }

    public BrowserImportWindow(string? initialUrl, Func<WebChapterResult, Task> onImported)
        : this(string.IsNullOrWhiteSpace(initialUrl) ? null : new WebMultiImportOptions(initialUrl), onImported)
    {
    }

    public BrowserImportWindow(WebMultiImportOptions? initialOptions, Func<WebChapterResult, Task> onImported)
    {
        _onImported = onImported;
        InitializeComponent();

        if (initialOptions != null)
        {
            string url = initialOptions.Url?.Trim() ?? string.Empty;
            if (WebChapterImportService.IsTemplateUrl(url))
            {
                int firstNum = initialOptions.Mode == WebImportRangeMode.ByRange
                    ? Math.Max(1, initialOptions.StartChapter)
                    : 1;
                url = WebChapterImportService.TryReplaceUrlChapterNumber(url, firstNum) ?? url;
            }

            _initialUrl = url;
            if (!string.IsNullOrWhiteSpace(url))
            {
                UrlTextBox.Text = url;
            }

            if (initialOptions.Mode == WebImportRangeMode.ByRange)
            {
                ImportModeCombo.SelectedIndex = 1;
                RangeStartBox.Text = Math.Max(1, initialOptions.StartChapter).ToString();
                RangeEndBox.Text = Math.Max(1, initialOptions.EndChapter).ToString();
            }
            else
            {
                ImportModeCombo.SelectedIndex = 0;
                ChapterCountBox.Text = Math.Clamp(initialOptions.Count, 1, 200).ToString();
            }
            UpdateImportModePanels();
        }

        Loaded += OnLoaded;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _importCts?.Cancel();
        var pending = Interlocked.Exchange(ref _pendingNavTcs, null);
        pending?.TrySetCanceled();
        base.OnClosing(e);
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

        core.NavigationStarting += (_, args) =>
        {
            if (args.Uri != "about:blank" &&
                (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                 WebChapterImportService.IsBlockedUri(uri)))
            {
                args.Cancel = true;
                Dispatcher.UIThread.Post(() =>
                {
                    StatusText.Text = "บล็อกการนำทางไปยังที่อยู่ภายในหรือโปรโตคอลที่ไม่ปลอดภัย";
                    UpdateNavButtons();
                });
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (!_isBusy)
                {
                    StatusText.Text = "กำลังโหลดหน้าเว็บ...";
                }
                UpdateNavButtons();
            });
        };

        core.NavigationCompleted += (_, args) =>
        {
            if (args.IsSuccess)
            {
                _ = WebView.UnlockCopyProtectionAsync();
            }

            var tcs = Interlocked.Exchange(ref _pendingNavTcs, null);
            tcs?.TrySetResult(args.IsSuccess);

            Dispatcher.UIThread.Post(() =>
            {
                UpdateNavButtons();
                if (!_isBusy)
                {
                    StatusText.Text = args.IsSuccess
                        ? $"โหลดเสร็จแล้ว: {core.DocumentTitle}"
                        : $"โหลดหน้าเว็บมีปัญหา (รหัส {args.WebErrorStatus})";
                }
            });
        };
    }

    private void UpdateNavButtons()
    {
        var core = WebView.CoreWebView2;
        BackButton.IsEnabled = core?.CanGoBack == true;
        ForwardButton.IsEnabled = core?.CanGoForward == true;
    }

    private void OnImportModeChanged(object? sender, SelectionChangedEventArgs e)
        => UpdateImportModePanels();

    private void UpdateImportModePanels()
    {
        if (CountPanel == null || RangePanel == null || ImportModeCombo == null) return;
        bool isRange = ImportModeCombo.SelectedIndex == 1;
        CountPanel.IsVisible = !isRange;
        RangePanel.IsVisible = isRange;
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

    private void OnStopImportClicked(object? sender, RoutedEventArgs e)
    {
        _importCts?.Cancel();
        StatusText.Text = "กำลังหยุดการดึงเนื้อหา...";
    }

    private bool TryBuildImportOptions(out WebMultiImportOptions? options, out string error)
    {
        options = null;
        error = string.Empty;
        string url = WebView.CoreWebView2?.Source ?? UrlTextBox.Text ?? string.Empty;

        if (ImportModeCombo.SelectedIndex == 1)
        {
            if (!int.TryParse(RangeStartBox.Text?.Trim(), out int start) ||
                !int.TryParse(RangeEndBox.Text?.Trim(), out int end) ||
                start < 1 || end < start)
            {
                error = "กรุณาระบุช่วงเลขตอนให้ถูกต้อง (เริ่มต้น >= 1 และไม่เกินเลขสิ้นสุด)";
                return false;
            }
            if (end - start + 1 > 200)
            {
                error = "ดึงต่อเนื่องได้สูงสุดครั้งละ 200 ตอน";
                return false;
            }
            options = new WebMultiImportOptions(url, WebImportRangeMode.ByRange, end - start + 1, start, end);
            return true;
        }

        if (!int.TryParse(ChapterCountBox.Text?.Trim(), out int count) || count < 1 || count > 200)
        {
            error = "กรุณาระบุจำนวนตอนระหว่าง 1 ถึง 200";
            return false;
        }
        options = new WebMultiImportOptions(url, WebImportRangeMode.ByCount, count, 1, count);
        return true;
    }

    private void SetBusyState(bool busy)
    {
        _isBusy = busy;
        KeepOpenButton.IsEnabled = !busy;
        ImportCloseButton.IsEnabled = !busy;
        ImportModeCombo.IsEnabled = !busy;
        ChapterCountBox.IsEnabled = !busy;
        RangeStartBox.IsEnabled = !busy;
        RangeEndBox.IsEnabled = !busy;
        StopImportButton.IsVisible = busy;
    }

    private async Task NavigateAndWaitAsync(string targetUrl, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var prev = Interlocked.Exchange(ref _pendingNavTcs, tcs);
        prev?.TrySetCanceled();

        using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
        WebView.Navigate(targetUrl);

        bool success = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(25), ct);
        if (!success)
        {
            throw new InvalidOperationException($"ไม่สามารถโหลดหน้าเว็บ: {targetUrl}");
        }
    }

    private async Task<(WebChapterResult Chapter, string ActualUrl)> FetchChapterInBrowserAsync(string targetUrl, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // ลองดึงเฉพาะ HTML ผ่านเซสชันเบราว์เซอร์โดยตรงก่อน (เร็ว ไม่ต้องโหลดรูป/โฆษณาทั้งหน้า)
        string? fastHtml = await WebView.FetchHtmlViaBrowserSessionAsync(targetUrl);
        if (!string.IsNullOrWhiteSpace(fastHtml))
        {
            try
            {
                var fastResult = WebChapterImportService.ExtractFromHtml(targetUrl, fastHtml);
                return (fastResult, targetUrl);
            }
            catch (NotSupportedException)
            {
                throw;
            }
            catch
            {
                // หน้าเว็บอาจต้องรัน JS เพื่อสร้างเนื้อหา — สลับไปเปิดโหลดหน้าจริงด้านล่าง
            }
        }

        await NavigateAndWaitAsync(targetUrl, ct);
        string pageHtml = await WebView.GetPageHtmlAsync();
        string actualUrl = WebView.CoreWebView2?.Source ?? targetUrl;
        try
        {
            return (WebChapterImportService.ExtractFromHtml(actualUrl, pageHtml), actualUrl);
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch
        {
            // กรณีเว็บ SPA กำลังเรนเดอร์เนื้อหาหลังโหลดเสร็จ รอสั้นๆ แล้วอ่านซ้ำอีกหนึ่งครั้ง
            await Task.Delay(250, ct);
            pageHtml = await WebView.GetPageHtmlAsync();
            actualUrl = WebView.CoreWebView2?.Source ?? targetUrl;
            return (WebChapterImportService.ExtractFromHtml(actualUrl, pageHtml), actualUrl);
        }
    }

    private async Task ImportCurrentPageAsync(bool closeAfterImport)
    {
        if (_isBusy) return;
        if (!TryBuildImportOptions(out var options, out string err) || options == null)
        {
            StatusText.Text = err;
            return;
        }

        SetBusyState(true);
        _importCts?.Cancel();
        _importCts?.Dispose();
        _importCts = new CancellationTokenSource();
        var ct = _importCts.Token;

        int importedCount = 0;

        try
        {
            string currentUrl = WebView.CoreWebView2?.Source ?? UrlTextBox.Text ?? "https://localhost/";
            StatusText.Text = "กำลังดึงเนื้อหาจากหน้าเว็บ (Reader Mode)...";
            string initialHtml = await WebView.GetPageHtmlAsync();

            if (options.Mode == WebImportRangeMode.ByCount && options.EffectiveCount == 1)
            {
                var result = WebChapterImportService.ExtractFromHtml(currentUrl, initialHtml);
                await _onImported(result);
                importedCount = 1;

                if (closeAfterImport)
                {
                    Close();
                    return;
                }

                StatusText.Text = $"นำเข้า \"{result.Title}\" แล้ว — กดตอนถัดไปบนหน้าเว็บเพื่อดึงต่อได้";
                return;
            }

            bool isTocPage = WebChapterImportService.TryAnalyzePageForBatchImport(
                currentUrl, initialHtml, options.Mode, out var tocLinks, out var firstArticle);

            if (isTocPage)
            {
                int start = Math.Max(1, Math.Min(options.StartChapter, options.EndChapter));
                int end = Math.Max(start, options.EndChapter);
                var selected = WebChapterImportService.SelectTocLinksForImport(
                    tocLinks, options.Mode, options.EffectiveCount, start, end);

                if (selected.Count == 0)
                {
                    throw new InvalidOperationException("ไม่พบตอนที่ตรงกับช่วงที่ระบุในหน้าสารบัญนี้");
                }

                for (int i = 0; i < selected.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    StatusText.Text = $"กำลังดึงตอนที่ {i + 1}/{selected.Count}...";
                    var (chapter, _) = await FetchChapterInBrowserAsync(selected[i].Url, ct);
                    await _onImported(chapter);
                    importedCount++;
                    StatusText.Text = $"นำเข้าแล้ว ({importedCount}/{selected.Count}): {chapter.Title}";
                }
            }
            else
            {
                int total = options.EffectiveCount;
                int start = Math.Max(1, Math.Min(options.StartChapter, options.EndChapter));
                bool useRangePattern = false;
                string? startPatternUrl = null;
                if (options.Mode == WebImportRangeMode.ByRange)
                {
                    int? detectedNum = WebChapterImportService.ExtractUrlChapterNumber(currentUrl);
                    if (detectedNum.HasValue && detectedNum.Value != start)
                    {
                        startPatternUrl = WebChapterImportService.TryReplaceUrlChapterNumber(currentUrl, start);
                        useRangePattern = !string.IsNullOrWhiteSpace(startPatternUrl);
                    }
                }

                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string basePatternUrl = currentUrl;
                string? targetUrl = useRangePattern ? startPatternUrl : currentUrl;
                WebChapterResult? currentChapter = useRangePattern ? null : firstArticle;

                for (int i = 0; i < total; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    if (currentChapter == null)
                    {
                        if (string.IsNullOrWhiteSpace(targetUrl) || !visited.Add(targetUrl))
                        {
                            break;
                        }

                        StatusText.Text = $"กำลังดึงตอนที่ {i + 1}/{total}...";
                        var (fetchedChapter, actualUrl) = await FetchChapterInBrowserAsync(targetUrl, ct);
                        visited.Add(actualUrl);
                        currentChapter = fetchedChapter;
                    }
                    else
                    {
                        visited.Add(targetUrl!);
                    }

                    await _onImported(currentChapter);
                    importedCount++;
                    StatusText.Text = $"นำเข้าแล้ว ({importedCount}/{total}): {currentChapter.Title}";

                    if (i + 1 >= total) break;

                    string? nextUrl = useRangePattern
                        ? WebChapterImportService.TryReplaceUrlChapterNumber(basePatternUrl, start + i + 1) ?? currentChapter.NextChapterUrl
                        : currentChapter.NextChapterUrl;

                    if (string.IsNullOrWhiteSpace(nextUrl) || visited.Contains(nextUrl))
                    {
                        break;
                    }

                    targetUrl = nextUrl;
                    currentChapter = null;
                }
            }

            if (closeAfterImport && importedCount > 0)
            {
                Close();
                return;
            }

            StatusText.Text = $"นำเข้าเสร็จสิ้น {importedCount} ตอน";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = $"หยุดการดึงเนื้อหาแล้ว (นำเข้าสำเร็จ {importedCount} ตอน)";
        }
        catch (Exception ex)
        {
            StatusText.Text = importedCount > 0
                ? $"นำเข้าได้ {importedCount} ตอน (หยุดกลางคัน: {ex.Message})"
                : $"ดึงเนื้อหาไม่สำเร็จ: {ex.Message}";
        }
        finally
        {
            _importCts?.Dispose();
            _importCts = null;
            SetBusyState(false);
        }
    }
}
