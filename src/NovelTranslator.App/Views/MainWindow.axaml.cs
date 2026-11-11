using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NovelTranslator.App.ViewModels;
using NovelTranslator.Core.Data;
using NovelTranslator.Core.Models;
using NovelTranslator.Core.Settings;

namespace NovelTranslator.App.Views;

public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        // เชื่อมต่อการเปิดหน้าต่างย่อย
        ViewModel.RequestOpenSettings = async () =>
        {
            var settings = AppSettings.Load();
            var vm = new SettingsViewModel(settings);
            var win = new SettingsWindow(vm);
            await win.ShowDialog(this);
            ViewModel.ReloadSettings();
        };

        ViewModel.RequestOpenGlossary = async (story) =>
        {
            var db = new NovelDatabase();
            var repo = new NovelRepository(db);
            var vm = new GlossaryViewModel(repo, story);
            var win = new GlossaryWindow(vm);
            await win.ShowDialog(this);
        };

        ViewModel.RequestOpenSearchReplace = async (story, chapter) =>
        {
            var db = new NovelDatabase();
            var repo = new NovelRepository(db);
            var vm = new SearchReplaceViewModel(repo, story, chapter);
            var win = new SearchReplaceWindow(vm);
            await win.ShowDialog(this);
        };

        ViewModel.RequestSaveFileDialog = async (suggestedFileName) =>
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null) return null;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "บันทึกไฟล์",
                SuggestedFileName = suggestedFileName,
                DefaultExtension = Path.GetExtension(suggestedFileName).TrimStart('.'),
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Text / Data Files") { Patterns = new[] { $"*{Path.GetExtension(suggestedFileName)}" } }
                }
            });

            return file?.Path.LocalPath;
        };

        ViewModel.RequestOpenFilesDialog = async (title) =>
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null) return null;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = true
            });

            return files.Select(f => f.Path.LocalPath).ToArray();
        };

        ViewModel.RequestPromptDialog = async (title, message) =>
        {
            // Simple prompt window
            var inputTextBox = new TextBox { Text = "", Margin = new Avalonia.Thickness(0, 10, 0, 10) };
            var okBtn = new Button { Content = "ตกลง", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Width = 80 };
            string? result = null;

            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(16),
                    Children =
                    {
                        new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                        inputTextBox,
                        okBtn
                    }
                }
            };

            okBtn.Click += (_, _) =>
            {
                result = inputTextBox.Text;
                dialog.Close();
            };

            await dialog.ShowDialog(this);
            return result;
        };

        ViewModel.RequestConfirmDialog = async (message) =>
        {
            bool result = false;
            var okBtn = new Button { Content = "ยืนยัน", Width = 80 };
            var cancelBtn = new Button { Content = "ยกเลิก", Width = 80 };

            var dialog = new Window
            {
                Title = "ยืนยันการดำเนินการ",
                Width = 400,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(16),
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                        new StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Spacing = 10,
                            Children = { okBtn, cancelBtn }
                        }
                    }
                }
            };

            okBtn.Click += (_, _) => { result = true; dialog.Close(); };
            cancelBtn.Click += (_, _) => { result = false; dialog.Close(); };

            await dialog.ShowDialog(this);
            return result;
        };

        ViewModel.RequestCopyToClipboard = async (text) =>
        {
            var topLevel = GetTopLevel(this);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(text);
            }
        };

        ViewModel.RequestShowErrorDialog = (errorDetails) =>
        {
            var closeBtn = new Button { Content = "ปิด", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Width = 80 };
            var copyBtn = new Button { Content = "📋 คัดลอก", Width = 90 };
            var tb = new TextBox
            {
                Text = errorDetails,
                IsReadOnly = true,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Height = 240,
                Margin = new Avalonia.Thickness(0, 8, 0, 12),
                AcceptsReturn = true
            };

            var dialog = new Window
            {
                Title = "รายละเอียด Error สำหรับ Debug",
                Width = 650,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(16),
                    Children =
                    {
                        new TextBlock { Text = "เกิดข้อผิดพลาดในการแปลหรือเชื่อมต่อ AI:", FontWeight = Avalonia.Media.FontWeight.Bold },
                        tb,
                        new StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Spacing = 10,
                            Children = { copyBtn, closeBtn }
                        }
                    }
                }
            };

            copyBtn.Click += async (_, _) =>
            {
                var topLevel = GetTopLevel(dialog);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(errorDetails);
                    copyBtn.Content = "✅ คัดลอกแล้ว";
                }
            };
            closeBtn.Click += (_, _) => dialog.Close();

            dialog.ShowDialog(this);
        };
    }

    private async void OnAddSelectedToGlossaryClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedStory == null) return;

        // ดึงข้อความที่เลือกจากช่องต้นฉบับหรือคำแปล
        string selected = OriginalTextBox.SelectedText;
        if (string.IsNullOrWhiteSpace(selected))
        {
            selected = TranslatedTextBox.SelectedText;
        }

        if (string.IsNullOrWhiteSpace(selected)) return;

        var db = new NovelDatabase();
        var repo = new NovelRepository(db);
        var vm = new GlossaryViewModel(repo, ViewModel.SelectedStory, initialSource: selected.Trim());
        var win = new GlossaryWindow(vm);
        await win.ShowDialog(this);
    }

    private async void OnRetranslateSelectedParagraphClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        string selected = OriginalTextBox.SelectedText;
        if (string.IsNullOrWhiteSpace(selected))
        {
            // ถ้าไม่ได้ลากคลุม ให้ดึงย่อหน้าที่เคอร์เซอร์วางอยู่
            int caretIndex = OriginalTextBox.CaretIndex;
            string text = OriginalTextBox.Text ?? "";
            selected = ExtractParagraphAtCaret(text, caretIndex);
        }

        if (string.IsNullOrWhiteSpace(selected)) return;
        await ViewModel.RetranslateParagraphAsync(selected);
    }

    private static string ExtractParagraphAtCaret(string text, int caret)
    {
        if (string.IsNullOrEmpty(text)) return "";
        caret = Math.Clamp(caret, 0, text.Length);

        int start = text.LastIndexOf("\n\n", Math.Max(0, caret - 1), StringComparison.Ordinal);
        start = start < 0 ? 0 : start + 2;

        int end = text.IndexOf("\n\n", caret, StringComparison.Ordinal);
        end = end < 0 ? text.Length : end;

        return text.Substring(start, end - start).Trim();
    }
}
