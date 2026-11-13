using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NovelTranslator.App.ViewModels;
using NovelTranslator.Core.Data;
using NovelTranslator.Core.Models;
using NovelTranslator.Core.Settings;

namespace NovelTranslator.App.Views;

public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;
    private bool _isSyncingScroll;

    // สถานะสำหรับการ Drag & Drop จัดลำดับตอน
    private Chapter? _draggedChapter;
    private List<Chapter> _movingChapters = new();
    private Point _dragStartPoint;
    private bool _isDragging;
    private ListBoxItem? _currentIndicatorItem;
    private Chapter? _targetChapter;
    private bool _dropAfter;
    private bool _isInternalSelectionSync;
    private readonly IBrush _dragIndicatorBrush = new SolidColorBrush(Color.Parse("#2563EB"));
    private string _glossarySelectedText = "";

    // สถานะสำหรับการเลื่อนหน้าจอด้วยปุ่มเมาส์กลาง (MMB Quick Scroll)
    private ScrollViewer? _mmbScrollViewer;
    private Point _mmbAnchorPoint;
    private Point _mmbCurrentPoint;
    private bool _mmbActive;
    private bool _mmbLatched;
    private bool _mmbMovedBeyondDeadzone;
    private DateTime _mmbPressTimeUtc;
    private DispatcherTimer? _mmbScrollTimer;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyEditorFont(ViewModel.EditorFontFamily);
        ApplyKeyBindings();
        OriginalTextBox.AddHandler(ScrollViewer.ScrollChangedEvent, OnOriginalScrollChanged);
        TranslatedTextBox.AddHandler(ScrollViewer.ScrollChangedEvent, OnTranslatedScrollChanged);

        // ตรวจจับการคลุมข้อความเพื่อแสดงปุ่มลอยเพิ่มเข้า Glossary
        OriginalTextBox.AddHandler(InputElement.PointerReleasedEvent, OnTextBoxPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        TranslatedTextBox.AddHandler(InputElement.PointerReleasedEvent, OnTextBoxPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        OriginalTextBox.AddHandler(InputElement.PointerPressedEvent, OnTextBoxPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        TranslatedTextBox.AddHandler(InputElement.PointerPressedEvent, OnTextBoxPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        OriginalTextBox.KeyUp += OnTextBoxKeyUp;
        TranslatedTextBox.KeyUp += OnTextBoxKeyUp;

        ViewModel.RequestToggleFullScreen = () =>
        {
            WindowState = WindowState == WindowState.FullScreen
                ? WindowState.Normal
                : WindowState.FullScreen;
        };

        // เชื่อมต่อการเปิดหน้าต่างย่อย
        ViewModel.RequestOpenSettings = async () =>
        {
            var settings = ViewModel.Settings;
            var vm = new SettingsViewModel(settings);
            var win = new SettingsWindow(vm);
            await win.ShowDialog(this);
            ViewModel.ReloadSettings();
            ApplyEditorFont(ViewModel.EditorFontFamily);
            ApplyKeyBindings();
        };

        ViewModel.RequestOpenGlossary = async (story) =>
        {
            var db = new NovelDatabase();
            var repo = new NovelRepository(db);
            var vm = new GlossaryViewModel(repo, story);
            var win = new GlossaryWindow(vm);
            await win.ShowDialog(this);
        };

        ViewModel.RequestOpenAutoGlossary = async (story, chapter) =>
        {
            if (chapter == null || string.IsNullOrWhiteSpace(chapter.OriginalText) || ViewModel.IsTranslating) return;

            ViewModel.IsTranslating = true;
            ViewModel.IsTranslatingIndeterminate = true;
            ViewModel.LastErrorDetails = string.Empty;
            ViewModel.ProgressText = "กำลังสแกนหาศัพท์ใหม่ในตอนนี้ด้วย AI...";

            List<NovelTranslator.Core.Glossary.ExtractedTerm> extracted;
            NovelRepository repo;
            try
            {
                var db = new NovelDatabase();
                repo = new NovelRepository(db);
                var settings = ViewModel.Settings;
                var llm = new NovelTranslator.Core.Llm.LlmClient(settings);
                var extractor = new NovelTranslator.Core.Glossary.GlossaryExtractor(llm, settings);

                var existingGlossary = await repo.GetGlossaryByStoryAsync(story.Id);
                extracted = await extractor.ExtractNewTermsAsync(chapter.OriginalText, chapter.TranslatedText ?? "", existingGlossary);
            }
            catch (Exception ex)
            {
                ViewModel.LastErrorDetails = ex.ToString();
                string firstLine = ex.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? ex.Message;
                ViewModel.ProgressText = $"สแกนศัพท์ไม่สำเร็จ · {firstLine}";
                return;
            }
            finally
            {
                ViewModel.IsTranslating = false;
                ViewModel.IsTranslatingIndeterminate = false;
            }

            if (extracted.Count == 0)
            {
                ViewModel.ProgressText = "ไม่พบชื่อเฉพาะใหม่ในตอนนี้";
                ViewModel.SaveStatusText = "ไม่พบชื่อเฉพาะใหม่ในตอนนี้";
                return;
            }

            var autoVm = new AutoGlossaryViewModel(repo, story, extracted);
            var dialog = new AutoGlossaryDialog { DataContext = autoVm };
            autoVm.RequestClose = () => dialog.Close();
            await dialog.ShowDialog(this);
            ViewModel.ProgressText = "สแกนศัพท์เสร็จสิ้น";
            ViewModel.SaveStatusText = "อัปเดตคำศัพท์เข้า Glossary เรียบร้อยแล้ว";
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
            string? result = null;
            var dialog = new Window
            {
                Title = title,
                Width = 440,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowDecorations = Avalonia.Controls.WindowDecorations.None,
                Background = Brushes.Transparent,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent }
            };

            var inputTextBox = new TextBox
            {
                Text = "",
                Height = 34,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16),
                CornerRadius = new CornerRadius(6)
            };

            var okBtn = new Button
            {
                Content = "ตกลง",
                Classes = { "primary" },
                Height = 32,
                Padding = new Thickness(18, 0),
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var cancelBtn = new Button
            {
                Content = "ยกเลิก",
                Classes = { "ghost" },
                Height = 32,
                Padding = new Thickness(14, 0),
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var closeBtn = new Button
            {
                Content = "✕",
                Classes = { "ghost" },
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                FontSize = 12,
                Foreground = GetThemeBrush("TextMutedBrush", "#85858B"),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            void Submit()
            {
                if (!string.IsNullOrWhiteSpace(inputTextBox.Text))
                {
                    result = inputTextBox.Text.Trim();
                    dialog.Close();
                }
            }

            okBtn.Click += (_, _) => Submit();
            cancelBtn.Click += (_, _) => dialog.Close();
            closeBtn.Click += (_, _) => dialog.Close();

            inputTextBox.KeyDown += (_, ke) =>
            {
                if (ke.Key == Key.Enter) { ke.Handled = true; Submit(); }
                else if (ke.Key == Key.Escape) { ke.Handled = true; dialog.Close(); }
            };
            dialog.KeyDown += (_, ke) =>
            {
                if (ke.Key == Key.Escape) { ke.Handled = true; dialog.Close(); }
            };

            var headerGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*, Auto"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = FontWeight.SemiBold,
                Foreground = GetThemeBrush("TextPrimaryBrush", "#EDEDED"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(titleBlock, 0);
            Grid.SetColumn(closeBtn, 1);
            headerGrid.Children.Add(titleBlock);
            headerGrid.Children.Add(closeBtn);
            headerGrid.PointerPressed += (_, pe) => dialog.BeginMoveDrag(pe);

            var messageBlock = new TextBlock
            {
                Text = message,
                FontSize = 13,
                Foreground = GetThemeBrush("TextSecondaryBrush", "#A1A1A6"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var actionsPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 8,
                Children = { cancelBtn, okBtn }
            };

            var contentStack = new StackPanel
            {
                Children = { headerGrid, messageBlock, inputTextBox, actionsPanel }
            };

            var card = new Border
            {
                Background = GetThemeBrush("CardBgBrush", "#19191B"),
                BorderBrush = GetThemeBrush("BorderSubtleBrush", "#27272A"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                BoxShadow = new BoxShadows(new BoxShadow { OffsetX = 0, OffsetY = 8, Blur = 24, Spread = 0, Color = Color.Parse("#60000000") }),
                Margin = new Thickness(12),
                Padding = new Thickness(20),
                Child = contentStack
            };

            dialog.Content = card;
            dialog.Loaded += (_, _) => { inputTextBox.Focus(); };
            await dialog.ShowDialog(this);
            return result;
        };

        ViewModel.RequestConfirmDialog = async (message) =>
        {
            bool result = false;
            var dialog = new Window
            {
                Title = "ยืนยันการดำเนินการ",
                Width = 420,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowDecorations = Avalonia.Controls.WindowDecorations.None,
                Background = Brushes.Transparent,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent }
            };

            var okBtn = new Button
            {
                Content = "ยืนยัน",
                Classes = { "primary" },
                Height = 32,
                Padding = new Thickness(18, 0),
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var cancelBtn = new Button
            {
                Content = "ยกเลิก",
                Classes = { "ghost" },
                Height = 32,
                Padding = new Thickness(14, 0),
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var closeBtn = new Button
            {
                Content = "✕",
                Classes = { "ghost" },
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                FontSize = 12,
                Foreground = GetThemeBrush("TextMutedBrush", "#85858B"),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            okBtn.Click += (_, _) => { result = true; dialog.Close(); };
            cancelBtn.Click += (_, _) => { result = false; dialog.Close(); };
            closeBtn.Click += (_, _) => { result = false; dialog.Close(); };

            dialog.KeyDown += (_, ke) =>
            {
                if (ke.Key == Key.Enter) { ke.Handled = true; result = true; dialog.Close(); }
                else if (ke.Key == Key.Escape) { ke.Handled = true; result = false; dialog.Close(); }
            };

            var headerGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*, Auto"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var titleBlock = new TextBlock
            {
                Text = "ยืนยันการดำเนินการ",
                FontSize = 15,
                FontWeight = FontWeight.SemiBold,
                Foreground = GetThemeBrush("TextPrimaryBrush", "#EDEDED"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(titleBlock, 0);
            Grid.SetColumn(closeBtn, 1);
            headerGrid.Children.Add(titleBlock);
            headerGrid.Children.Add(closeBtn);
            headerGrid.PointerPressed += (_, pe) => dialog.BeginMoveDrag(pe);

            var messageBlock = new TextBlock
            {
                Text = message,
                FontSize = 13,
                Foreground = GetThemeBrush("TextSecondaryBrush", "#A1A1A6"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var actionsPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 8,
                Children = { cancelBtn, okBtn }
            };

            var contentStack = new StackPanel
            {
                Children = { headerGrid, messageBlock, actionsPanel }
            };

            var card = new Border
            {
                Background = GetThemeBrush("CardBgBrush", "#19191B"),
                BorderBrush = GetThemeBrush("BorderSubtleBrush", "#27272A"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                BoxShadow = new BoxShadows(new BoxShadow { OffsetX = 0, OffsetY = 8, Blur = 24, Spread = 0, Color = Color.Parse("#60000000") }),
                Margin = new Thickness(12),
                Padding = new Thickness(20),
                Child = contentStack
            };

            dialog.Content = card;
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
            var dialog = new Window
            {
                Title = "รายละเอียดข้อผิดพลาด",
                Width = 640,
                Height = 440,
                CanResize = true,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowDecorations = Avalonia.Controls.WindowDecorations.None,
                Background = Brushes.Transparent,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent }
            };

            var closeBtn = new Button
            {
                Content = "ปิด",
                Classes = { "primary" },
                Height = 32,
                Padding = new Thickness(20, 0),
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var copyBtn = new Button
            {
                Content = "คัดลอกข้อผิดพลาด",
                Height = 32,
                Padding = new Thickness(14, 0),
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var topCloseBtn = new Button
            {
                Content = "✕",
                Classes = { "ghost" },
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                FontSize = 12,
                Foreground = GetThemeBrush("TextMutedBrush", "#85858B"),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var tb = new TextBox
            {
                Text = errorDetails,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 280,
                Margin = new Thickness(0, 0, 0, 16),
                AcceptsReturn = true,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                FontFamily = new FontFamily("Cascadia Mono, Consolas, Segoe UI, monospace"),
                FontSize = 12,
                Background = GetThemeBrush("EditorBgBrush", "#19191B")
            };

            var headerGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*, Auto"),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var titleBlock = new TextBlock
            {
                Text = "รายละเอียดข้อผิดพลาด",
                FontSize = 15,
                FontWeight = FontWeight.SemiBold,
                Foreground = GetThemeBrush("TextPrimaryBrush", "#EDEDED"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(titleBlock, 0);
            Grid.SetColumn(topCloseBtn, 1);
            headerGrid.Children.Add(titleBlock);
            headerGrid.Children.Add(topCloseBtn);
            headerGrid.PointerPressed += (_, pe) => dialog.BeginMoveDrag(pe);

            var subheadBlock = new TextBlock
            {
                Text = "เกิดข้อผิดพลาดในการแปลหรือเชื่อมต่อ AI กรุณาตรวจสอบรายละเอียดด้านล่าง",
                FontSize = 12,
                Foreground = GetThemeBrush("TextSecondaryBrush", "#A1A1A6"),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var actionsPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 8,
                Children = { copyBtn, closeBtn }
            };

            var contentStack = new StackPanel
            {
                Children = { headerGrid, subheadBlock, tb, actionsPanel }
            };

            var card = new Border
            {
                Background = GetThemeBrush("CardBgBrush", "#19191B"),
                BorderBrush = GetThemeBrush("BorderSubtleBrush", "#27272A"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                BoxShadow = new BoxShadows(new BoxShadow { OffsetX = 0, OffsetY = 8, Blur = 24, Spread = 0, Color = Color.Parse("#60000000") }),
                Margin = new Thickness(12),
                Padding = new Thickness(20),
                Child = contentStack
            };

            copyBtn.Click += async (_, _) =>
            {
                var topLevel = GetTopLevel(dialog);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(errorDetails);
                    copyBtn.Content = "คัดลอกเรียบร้อย ✓";
                }
            };
            closeBtn.Click += (_, _) => dialog.Close();
            topCloseBtn.Click += (_, _) => dialog.Close();
            dialog.KeyDown += (_, ke) =>
            {
                if (ke.Key == Key.Escape) { ke.Handled = true; dialog.Close(); }
            };

            dialog.Content = card;
            dialog.ShowDialog(this);
        };

        // ซิงโครไนซ์การเลือกหลายตอนกับ ViewModel
        ChapterListBox.SelectionChanged += OnChapterListBoxSelectionChanged;

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.TranslatedText) && ViewModel.IsTranslating)
            {
                // เลื่อนหน้าจอคำแปลลงล่างสุดอัตโนมัติขณะสตรีมข้อความสด (Live Streaming Auto-Scroll)
                Dispatcher.UIThread.Post(() =>
                {
                    var sv = TranslatedTextBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
                    sv?.ScrollToEnd();
                });
            }
            if (e.PropertyName == nameof(MainViewModel.SelectedChapter) && ViewModel.SelectedChapter != null)
            {
                if (!_isInternalSelectionSync && ChapterListBox.SelectedItems != null && !ChapterListBox.SelectedItems.Contains(ViewModel.SelectedChapter))
                {
                    _isInternalSelectionSync = true;
                    try
                    {
                        ChapterListBox.SelectedItems.Clear();
                        ChapterListBox.SelectedItems.Add(ViewModel.SelectedChapter);
                        foreach (var c in ViewModel.Chapters)
                        {
                            c.IsSelected = (c == ViewModel.SelectedChapter);
                        }
                    }
                    finally
                    {
                        _isInternalSelectionSync = false;
                    }
                }
            }
        };

        ViewModel.RequestSelectAllChapters = () =>
        {
            _isInternalSelectionSync = true;
            try
            {
                ChapterListBox.SelectAll();
                foreach (var c in ViewModel.Chapters) c.IsSelected = true;
            }
            finally
            {
                _isInternalSelectionSync = false;
            }
        };

        ViewModel.RequestClearChapterSelection = () =>
        {
            _isInternalSelectionSync = true;
            try
            {
                ChapterListBox.UnselectAll();
                foreach (var c in ViewModel.Chapters) c.IsSelected = false;
            }
            finally
            {
                _isInternalSelectionSync = false;
            }
        };

        // ตรวจจับการเลื่อนหน้าในโหมดอ่านหนังสือเพื่อควบคุม Auto-hide แถบ Minimal ด้านบน
        ReadingScrollViewer.ScrollChanged += (_, _) =>
        {
            if (ViewModel?.IsFocusMode != true || ReadingTopBar == null) return;
            if (ReadingScrollViewer.Offset.Y <= 30)
            {
                ReadingTopBar.Classes.Set("is-active", true);
            }
        };

        // ตรวจจับเมาส์ระดับ Window เพื่อป้องกันการหลุด capture ขณะลากวาง และควบคุมการแสดงแถบด้านบน + MMB Quick Scroll
        AddHandler(PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnWindowPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(PointerReleasedEvent, OnWindowPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(PointerWheelChangedEvent, OnWindowPointerWheelChanged, RoutingStrategies.Tunnel);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel == null) return;

        if (e.PropertyName == nameof(MainViewModel.IsSidebarVisible))
        {
            MainLayoutGrid.ColumnDefinitions[0].Width = ViewModel.IsSidebarVisible ? new GridLength(280) : new GridLength(0);
            MainLayoutGrid.ColumnDefinitions[1].Width = ViewModel.IsSidebarVisible ? new GridLength(4) : new GridLength(0);
        }
        else if (e.PropertyName == nameof(MainViewModel.IsOriginalPaneVisible) ||
                 e.PropertyName == nameof(MainViewModel.IsTranslatedPaneVisible))
        {
            EditorSplitGrid.ColumnDefinitions[0].Width = ViewModel.OriginalColumnWidth;
            EditorSplitGrid.ColumnDefinitions[1].Width = ViewModel.SplitterColumnWidth;
            EditorSplitGrid.ColumnDefinitions[2].Width = ViewModel.TranslatedColumnWidth;
        }
        else if (e.PropertyName == nameof(MainViewModel.EditorFontFamily))
        {
            ApplyEditorFont(ViewModel.EditorFontFamily);
        }
        else if (e.PropertyName == nameof(MainViewModel.IsFocusMode))
        {
            if (ViewModel.IsFocusMode)
            {
                ReadingScrollViewer?.ScrollToHome();
                ReadingTopBar?.Classes.Set("is-active", true);
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.SelectedChapter))
        {
            if (ViewModel.IsFocusMode)
            {
                ReadingScrollViewer?.ScrollToHome();
                ReadingTopBar?.Classes.Set("is-active", true);
            }
        }
    }

    private void ApplyEditorFont(string? fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName)) fontName = "Tahoma";
        try
        {
            var fontFamily = new FontFamily(fontName);
            OriginalTextBox.FontFamily = fontFamily;
            TranslatedTextBox.FontFamily = fontFamily;
            if (ReadingContentTextBlock != null)
            {
                ReadingContentTextBlock.FontFamily = fontFamily;
            }
        }
        catch
        {
            // fallback
        }
    }

    public void ApplyKeyBindings()
    {
        if (ViewModel == null) return;
        KeyBindings.Clear();

        void AddBinding(string? gestureStr, ICommand command)
        {
            if (string.IsNullOrWhiteSpace(gestureStr)) return;
            try
            {
                var gesture = KeyGesture.Parse(gestureStr);
                if (gesture != null)
                {
                    KeyBindings.Add(new KeyBinding { Gesture = gesture, Command = command });
                }
            }
            catch
            {
                // Ignore invalid gesture syntax
            }
        }

        var s = ViewModel.Settings.Shortkeys;
        AddBinding(s.SaveKey, ViewModel.SaveCurrentChapterCommand);
        AddBinding(s.TranslateKey, ViewModel.StartTranslationCommand);
        AddBinding(s.TranslateAllKey, ViewModel.TranslateAllChaptersCommand);
        AddBinding(s.SearchReplaceKey, ViewModel.OpenSearchReplaceCommand);
        AddBinding(s.FullScreenKey, ViewModel.ToggleFullScreenCommand);
        AddBinding(s.FocusModeKey, ViewModel.ToggleFocusModeCommand);
        AddBinding(s.PrevChapterKey, ViewModel.PreviousChapterCommand);
        AddBinding(s.NextChapterKey, ViewModel.NextChapterCommand);
        AddBinding(s.ToggleSidebarKey, ViewModel.ToggleSidebarCommand);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (ViewModel is { IsFocusMode: true })
        {
            if (e.Key == Key.Escape)
            {
                ViewModel.ToggleFocusMode();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Left)
            {
                if (ViewModel.PreviousChapterCommand.CanExecute(null))
                {
                    ViewModel.PreviousChapterCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }
            if (e.Key == Key.Right)
            {
                if (ViewModel.NextChapterCommand.CanExecute(null))
                {
                    ViewModel.NextChapterCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }
        }
        if (e.Key == Key.F2 && ViewModel?.SelectedChapter != null && !ViewModel.SelectedChapter.IsEditingTitle)
        {
            ViewModel.StartRenameChapter(ViewModel.SelectedChapter);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && _mmbActive)
        {
            StopMmbAutoScroll();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
            FocusManager?.GetFocusedElement() is not TextBox &&
            ViewModel?.CanUndoDeleteChapter == true)
        {
            ViewModel.UndoDeleteChapterCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnChapterTitleDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TextBlock tb && tb.DataContext is Chapter chapter && ViewModel != null)
        {
            ViewModel.StartRenameChapter(chapter);
            e.Handled = true;
        }
    }

    private void OnRenameChapterMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is Chapter chapter && ViewModel != null)
        {
            ViewModel.StartRenameChapter(chapter);
        }
    }

    private void OnChapterRenameTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty && sender is TextBox tb && tb.IsVisible)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await Task.Delay(30);
                if (tb.IsVisible)
                {
                    tb.Focus();
                    tb.SelectAll();
                }
            }, DispatcherPriority.Input);
        }
    }

    private async void OnChapterRenameTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is Chapter chapter && ViewModel != null)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await ViewModel.CommitRenameChapterAsync(chapter);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                ViewModel.CancelRenameChapter(chapter);
                ChapterListBox.Focus();
            }
        }
    }

    private async void OnChapterRenameTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is Chapter chapter && ViewModel != null)
        {
            if (chapter.IsEditingTitle)
            {
                await ViewModel.CommitRenameChapterAsync(chapter);
            }
        }
    }

    private void OnOriginalScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingScroll || ViewModel is not { IsSyncScrollEnabled: true, IsBothPanesVisible: true }) return;
        SyncScroll(OriginalTextBox, TranslatedTextBox);
    }

    private void OnTranslatedScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingScroll || ViewModel is not { IsSyncScrollEnabled: true, IsBothPanesVisible: true }) return;
        SyncScroll(TranslatedTextBox, OriginalTextBox);
    }

    private void SyncScroll(TextBox sourceBox, TextBox targetBox)
    {
        var sourceSv = sourceBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        var targetSv = targetBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (sourceSv == null || targetSv == null) return;

        double sourceMaxY = sourceSv.Extent.Height - sourceSv.Viewport.Height;
        double targetMaxY = targetSv.Extent.Height - targetSv.Viewport.Height;
        if (sourceMaxY <= 0 || targetMaxY <= 0) return;

        double ratio = Math.Clamp(sourceSv.Offset.Y / sourceMaxY, 0.0, 1.0);
        _isSyncingScroll = true;
        try
        {
            targetSv.Offset = new Vector(targetSv.Offset.X, ratio * targetMaxY);
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    private void OnTextBoxPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            CheckAndShowGlossaryPopup(tb);
        }
    }

    private void OnTextBoxKeyUp(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox tb)
        {
            CheckAndShowGlossaryPopup(tb);
        }
    }

    private void OnTextBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (GlossaryPopup.IsOpen)
        {
            GlossaryPopup.IsOpen = false;
        }
    }

    private void CheckAndShowGlossaryPopup(TextBox tb)
    {
        Dispatcher.UIThread.Post(() =>
        {
            string selected = tb.SelectedText;
            if (!string.IsNullOrWhiteSpace(selected))
            {
                _glossarySelectedText = selected.Trim();

                var presenter = tb.GetVisualDescendants()
                    .OfType<Avalonia.Controls.Presenters.TextPresenter>()
                    .FirstOrDefault();

                if (presenter?.TextLayout != null)
                {
                    int startIndex = Math.Min(tb.SelectionStart, tb.SelectionEnd);
                    int endIndex = Math.Max(tb.SelectionStart, tb.SelectionEnd);
                    var startCharRect = presenter.TextLayout.HitTestTextPosition(startIndex);
                    var endCharRect = presenter.TextLayout.HitTestTextPosition(endIndex);

                    var startTopPoint = presenter.TranslatePoint(new Point(startCharRect.Left, startCharRect.Top), tb)
                                        ?? new Point(startCharRect.Left, startCharRect.Top);
                    var endBottomPoint = presenter.TranslatePoint(new Point(endCharRect.Right, endCharRect.Bottom), tb)
                                         ?? new Point(endCharRect.Right, endCharRect.Bottom);

                    // Anchor popup top-left at the end of the selection (bottom of the last character)
                    GlossaryPopup.Placement = Avalonia.Controls.PlacementMode.BottomEdgeAlignedLeft;
                    GlossaryPopup.PlacementTarget = tb;
                    GlossaryPopup.PlacementRect = new Rect(endBottomPoint.X - endCharRect.Width, endBottomPoint.Y, 1, 1);
                    GlossaryPopup.HorizontalOffset = 0;
                    GlossaryPopup.VerticalOffset = 0;
                }
                else
                {
                    GlossaryPopup.Placement = Avalonia.Controls.PlacementMode.Bottom;
                    GlossaryPopup.PlacementTarget = tb;
                    GlossaryPopup.HorizontalOffset = 0;
                    GlossaryPopup.VerticalOffset = 0;
                }

                GlossaryPopup.IsOpen = true;
            }
            else
            {
                GlossaryPopup.IsOpen = false;
            }
        });
    }

    private async void OnAddSelectedToGlossaryClicked(object? sender, RoutedEventArgs e)
    {
        GlossaryPopup.IsOpen = false;
        if (ViewModel?.SelectedStory == null) return;

        // ดึงข้อความที่เลือกจากตัวแปรบันทึก หรือจากช่องต้นฉบับ/คำแปล
        string selected = _glossarySelectedText;
        if (string.IsNullOrWhiteSpace(selected))
        {
            selected = OriginalTextBox.SelectedText;
            if (string.IsNullOrWhiteSpace(selected))
            {
                selected = TranslatedTextBox.SelectedText;
            }
        }

        if (string.IsNullOrWhiteSpace(selected)) return;

        var db = new NovelDatabase();
        var repo = new NovelRepository(db);
        var vm = new GlossaryViewModel(repo, ViewModel.SelectedStory, initialSource: selected.Trim());
        var win = new GlossaryWindow(vm);
        await win.ShowDialog(this);
    }

    private void OnChapterListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null || _isInternalSelectionSync) return;

        var selectedSet = ChapterListBox.SelectedItems?.OfType<Chapter>().ToHashSet() ?? new HashSet<Chapter>();
        foreach (var c in ViewModel.Chapters)
        {
            c.IsSelected = selectedSet.Contains(c);
        }

        // โหลดเนื้อหาของตอนล่าสุดที่คลิกเลือกเข้าสู่หน้าต่างแปลด้านขวาทันที
        var newlySelected = e.AddedItems?.OfType<Chapter>().LastOrDefault();
        if (newlySelected != null)
        {
            _isInternalSelectionSync = true;
            try
            {
                ViewModel.SelectedChapter = newlySelected;
            }
            finally
            {
                _isInternalSelectionSync = false;
            }
        }
        else if (selectedSet.Count > 0 && ViewModel.SelectedChapter != null && !selectedSet.Contains(ViewModel.SelectedChapter))
        {
            _isInternalSelectionSync = true;
            try
            {
                ViewModel.SelectedChapter = selectedSet.LastOrDefault();
            }
            finally
            {
                _isInternalSelectionSync = false;
            }
        }
    }

    private void SyncListBoxSelectionFromViewModel()
    {
        if (ViewModel == null) return;
        _isInternalSelectionSync = true;
        try
        {
            ChapterListBox.SelectedItems?.Clear();
            foreach (var c in ViewModel.Chapters.Where(ch => ch.IsSelected))
            {
                ChapterListBox.SelectedItems?.Add(c);
            }
            if (ViewModel.SelectedChapter != null && ChapterListBox.SelectedItems != null && !ChapterListBox.SelectedItems.Contains(ViewModel.SelectedChapter))
            {
                ChapterListBox.SelectedItems.Add(ViewModel.SelectedChapter);
            }
        }
        finally
        {
            _isInternalSelectionSync = false;
        }
    }

    private void OnDragHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed) return;

        // อนุญาตให้ลากเฉพาะเมื่อแสดง "ทั้งหมด" และไม่มีการพิมพ์ค้นหา
        if (ViewModel != null && (ViewModel.ChapterStatusFilterIndex != 0 || !string.IsNullOrWhiteSpace(ViewModel.ChapterSearchFilter)))
        {
            return;
        }

        if (sender is Control handle && handle.DataContext is Chapter chapter)
        {
            _draggedChapter = chapter;
            _dragStartPoint = point.Position;
            _isDragging = false;
            _targetChapter = null;

            var selectedList = ViewModel?.Chapters.Where(c => c.IsSelected).ToList() ?? new List<Chapter>();
            if (selectedList.Contains(chapter) && selectedList.Count > 1)
            {
                _movingChapters = selectedList;
            }
            else
            {
                _movingChapters = new List<Chapter> { chapter };
            }

            e.Handled = true;
        }
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;

        if (_mmbActive)
        {
            StopMmbAutoScroll();
            e.Handled = true;
            return;
        }

        if (props.IsMiddleButtonPressed)
        {
            var sv = FindScrollableViewerAt(e.Source as Visual);
            if (sv != null)
            {
                StartMmbAutoScroll(sv, e.GetPosition(DragGhostCanvas));
                e.Handled = true;
            }
        }
    }

    private void OnWindowPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_mmbActive)
        {
            StopMmbAutoScroll();
        }
    }

    private static ScrollViewer? FindScrollableViewerAt(Visual? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is ScrollViewer sv && sv.Extent.Height > sv.Viewport.Height + 1)
            {
                return sv;
            }
            if (current is TextBox tb)
            {
                var innerSv = tb.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
                if (innerSv != null && innerSv.Extent.Height > innerSv.Viewport.Height + 1)
                {
                    return innerSv;
                }
            }
            current = current.GetVisualParent();
        }
        return null;
    }

    private void StartMmbAutoScroll(ScrollViewer sv, Point anchorPos)
    {
        _mmbScrollViewer = sv;
        _mmbAnchorPoint = anchorPos;
        _mmbCurrentPoint = anchorPos;
        _mmbActive = true;
        _mmbLatched = false;
        _mmbMovedBeyondDeadzone = false;
        _mmbPressTimeUtc = DateTime.UtcNow;

        Canvas.SetLeft(MmbScrollAnchor, anchorPos.X - 14);
        Canvas.SetTop(MmbScrollAnchor, anchorPos.Y - 14);
        MmbScrollAnchor.IsVisible = true;
        DragGhostCanvas.IsVisible = true;
        Cursor = new Cursor(StandardCursorType.SizeNorthSouth);

        _mmbScrollTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _mmbScrollTimer.Tick -= OnMmbScrollTimerTick;
        _mmbScrollTimer.Tick += OnMmbScrollTimerTick;
        _mmbScrollTimer.Start();
    }

    private void StopMmbAutoScroll()
    {
        _mmbActive = false;
        _mmbLatched = false;
        _mmbScrollViewer = null;
        _mmbScrollTimer?.Stop();
        MmbScrollAnchor.IsVisible = false;
        if (!_isDragging)
        {
            DragGhostCanvas.IsVisible = false;
        }
        Cursor = Cursor.Default;
    }

    private void OnMmbScrollTimerTick(object? sender, EventArgs e)
    {
        if (!_mmbActive || _mmbScrollViewer == null) return;

        double dy = _mmbCurrentPoint.Y - _mmbAnchorPoint.Y;
        const double deadzone = 8.0;
        if (Math.Abs(dy) <= deadzone) return;

        double effective = Math.Sign(dy) * (Math.Abs(dy) - deadzone);
        // ความเร็วแบบเร่งตามระยะห่างจากจุดกึ่งกลางเหมือนเบราว์เซอร์
        double delta = Math.Sign(effective) * Math.Pow(Math.Abs(effective) * 0.22, 1.35);

        double maxY = Math.Max(0, _mmbScrollViewer.Extent.Height - _mmbScrollViewer.Viewport.Height);
        double nextY = Math.Clamp(_mmbScrollViewer.Offset.Y + delta, 0, maxY);
        _mmbScrollViewer.Offset = new Vector(_mmbScrollViewer.Offset.X, nextY);
    }

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_mmbActive)
        {
            _mmbCurrentPoint = e.GetPosition(DragGhostCanvas);
            if (Math.Abs(_mmbCurrentPoint.Y - _mmbAnchorPoint.Y) > 8.0)
            {
                _mmbMovedBeyondDeadzone = true;
            }
        }

        if (ViewModel?.IsFocusMode == true && ReadingTopBar != null)
        {
            var pt = e.GetPosition(this);
            bool nearTop = pt.Y <= 60;
            if (nearTop || (ReadingScrollViewer?.Offset.Y ?? 0) <= 30)
            {
                ReadingTopBar.Classes.Set("is-active", true);
            }
            else
            {
                ReadingTopBar.Classes.Set("is-active", false);
            }
        }

        if (_draggedChapter == null) return;

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            _ = EndDrag(null);
            return;
        }

        var currentPoint = point.Position;
        if (!_isDragging)
        {
            if (Math.Abs(currentPoint.Y - _dragStartPoint.Y) > 5 || Math.Abs(currentPoint.X - _dragStartPoint.X) > 5)
            {
                _isDragging = true;
                DragGhostBorder.IsVisible = true;
                DragGhostCanvas.IsVisible = true;
                Cursor = Cursor.Default;

                if (_movingChapters.Count > 1)
                {
                    DragGhostText.Text = $"{_movingChapters[0].Title} (+อีก {_movingChapters.Count - 1} ตอน)";
                }
                else
                {
                    DragGhostText.Text = _movingChapters.FirstOrDefault()?.Title ?? _draggedChapter?.Title ?? "";
                }
            }
            else
            {
                return;
            }
        }

        // เคลื่อนที่ Floating Drag Ghost Card ตามพิกัดเมาส์
        var canvasPt = e.GetPosition(DragGhostCanvas);
        Canvas.SetLeft(DragGhostBorder, canvasPt.X + 16);
        Canvas.SetTop(DragGhostBorder, canvasPt.Y - 14);

        // หา ListBoxItem ใต้ตำแหน่งเมาส์ผ่าน TranslatePoint
        var listPos = e.GetPosition(ChapterListBox);
        var items = ChapterListBox.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.IsVisible).ToList();
        ClearDragIndicator();
        _targetChapter = null;

        if (items.Count > 0 && listPos.X >= -60 && listPos.X <= ChapterListBox.Bounds.Width + 60)
        {
            ListBoxItem? targetItem = null;
            foreach (var item in items)
            {
                var itemPos = item.TranslatePoint(new Point(0, 0), ChapterListBox);
                if (itemPos.HasValue)
                {
                    double top = itemPos.Value.Y;
                    double bottom = top + item.Bounds.Height;
                    if (listPos.Y >= top && listPos.Y <= bottom)
                    {
                        targetItem = item;
                        _dropAfter = (listPos.Y - top) > (item.Bounds.Height / 2.0);
                        break;
                    }
                }
            }

            if (targetItem == null)
            {
                if (listPos.Y < 0)
                {
                    targetItem = items.First();
                    _dropAfter = false;
                }
                else if (listPos.Y > ChapterListBox.Bounds.Height)
                {
                    targetItem = items.Last();
                    _dropAfter = true;
                }
            }

            if (targetItem != null && targetItem.DataContext is Chapter targetCh)
            {
                if (!_movingChapters.Contains(targetCh))
                {
                    _targetChapter = targetCh;
                    _currentIndicatorItem = targetItem;
                    targetItem.BorderBrush = _dragIndicatorBrush;
                    targetItem.BorderThickness = _dropAfter ? new Thickness(0, 0, 0, 2) : new Thickness(0, 2, 0, 0);
                }
            }
        }
    }

    private async void OnWindowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_mmbActive && !_mmbLatched && e.InitialPressMouseButton == MouseButton.Middle)
        {
            var elapsedMs = (DateTime.UtcNow - _mmbPressTimeUtc).TotalMilliseconds;
            if (_mmbMovedBeyondDeadzone || elapsedMs > 250)
            {
                StopMmbAutoScroll();
            }
            else
            {
                _mmbLatched = true;
            }
            e.Handled = true;
            return;
        }

        if (_draggedChapter == null) return;
        await EndDrag(e);
    }

    private async Task EndDrag(PointerReleasedEventArgs? e)
    {
        bool wasDragging = _isDragging;
        var target = _targetChapter;
        bool dropAfter = _dropAfter;
        var toMove = new List<Chapter>(_movingChapters);

        ClearDragIndicator();
        DragGhostBorder.IsVisible = false;
        if (!_mmbActive)
        {
            DragGhostCanvas.IsVisible = false;
        }
        Cursor = Cursor.Default;
        ResetDragState();

        if (wasDragging && target != null && ViewModel != null && toMove.Count > 0)
        {
            await ViewModel.ReorderChaptersAsync(toMove, target, dropAfter);
            SyncListBoxSelectionFromViewModel();
        }

        if (e != null)
        {
            e.Handled = true;
        }
    }

    private void ClearDragIndicator()
    {
        if (_currentIndicatorItem != null)
        {
            _currentIndicatorItem.BorderThickness = new Thickness(0);
            _currentIndicatorItem.BorderBrush = null;
            _currentIndicatorItem = null;
        }
    }

    private void ResetDragState()
    {
        _isDragging = false;
        _draggedChapter = null;
        _targetChapter = null;
        _movingChapters.Clear();
    }

    private static IBrush GetThemeBrush(string resourceKey, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetResource(resourceKey, null, out var val) == true && val is IBrush brush)
        {
            return brush;
        }
        return new SolidColorBrush(Color.Parse(fallbackHex));
    }
}
