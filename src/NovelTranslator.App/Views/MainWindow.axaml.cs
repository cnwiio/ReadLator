using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
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
    private readonly Cursor _grabbingHandCursor = CreateGrabbingHandCursor();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        OriginalTextBox.AddHandler(ScrollViewer.ScrollChangedEvent, OnOriginalScrollChanged);
        TranslatedTextBox.AddHandler(ScrollViewer.ScrollChangedEvent, OnTranslatedScrollChanged);

        ViewModel.RequestToggleFullScreen = () =>
        {
            WindowState = WindowState == WindowState.FullScreen
                ? WindowState.Normal
                : WindowState.FullScreen;
        };

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
            var copyBtn = new Button { Content = "คัดลอก", Width = 90 };
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
                Title = "รายละเอียดข้อผิดพลาด",
                Width = 650,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(16),
                    Children =
                    {
                        new TextBlock { Text = "เกิดข้อผิดพลาดในการแปลหรือเชื่อมต่อ AI", FontWeight = Avalonia.Media.FontWeight.SemiBold },
                        tb,
                        new StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Spacing = 8,
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
                    copyBtn.Content = "คัดลอกแล้ว";
                }
            };
            closeBtn.Click += (_, _) => dialog.Close();

            dialog.ShowDialog(this);
        };

        // ซิงโครไนซ์การเลือกหลายตอนกับ ViewModel
        ChapterListBox.SelectionChanged += OnChapterListBoxSelectionChanged;

        ViewModel.PropertyChanged += (s, e) =>
        {
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

        // ตรวจจับเมาส์ระดับ Window เพื่อป้องกันการหลุด capture ขณะลากวาง
        AddHandler(PointerMovedEvent, OnWindowPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(PointerReleasedEvent, OnWindowPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel == null) return;

        if (e.PropertyName == nameof(MainViewModel.IsSidebarVisible))
        {
            MainLayoutGrid.ColumnDefinitions[0].Width = ViewModel.IsSidebarVisible ? new GridLength(280) : new GridLength(0);
            MainLayoutGrid.ColumnDefinitions[1].Width = ViewModel.IsSidebarVisible ? new GridLength(4) : new GridLength(0);
        }
        else if (e.PropertyName == nameof(MainViewModel.IsReaderMode))
        {
            EditorSplitGrid.ColumnDefinitions[0].Width = ViewModel.IsReaderMode ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            EditorSplitGrid.ColumnDefinitions[1].Width = ViewModel.IsReaderMode ? new GridLength(0) : new GridLength(12);
        }
    }

    private void OnOriginalScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingScroll || ViewModel is not { IsSyncScrollEnabled: true, IsReaderMode: false }) return;
        SyncScroll(OriginalTextBox, TranslatedTextBox);
    }

    private void OnTranslatedScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingScroll || ViewModel is not { IsSyncScrollEnabled: true, IsReaderMode: false }) return;
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

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
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
                DragGhostCanvas.IsVisible = true;
                Cursor = _grabbingHandCursor;

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
        DragGhostCanvas.IsVisible = false;
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

    private static Cursor CreateGrabbingHandCursor()
    {
        try
        {
            var size = new PixelSize(32, 32);
            var rtb = new RenderTargetBitmap(size, new Vector(96, 96));
            using (var ctx = rtb.CreateDrawingContext())
            {
                var geom = Geometry.Parse("M11,10 C10.4,10 10,10.4 10,11 L10,17 C10,17.6 10.4,18 11,18 C11.6,18 12,17.6 12,17 L12,11 C12,10.4 11.6,10 11,10 Z M14,9 C13.4,9 13,9.4 13,10 L13,17 C13,17.6 13.4,18 14,18 C14.6,18 15,17.6 15,17 L15,10 C15,9.4 14.6,9 14,9 Z M17,10 C16.4,10 16,10.4 16,11 L16,17 C16,17.6 16.4,18 17,18 C17.6,18 18,17.6 18,17 L18,11 C18,10.4 17.6,10 17,10 Z M8,11 C7.4,11 7,11.4 7,12 L7,17 C7,17.6 7.4,18 8,18 C8.6,18 9,17.6 9,17 L9,12 C9,11.4 8.6,11 8,11 Z M6,16 C6,21 10,24 13,24 C16,24 20,21 20,16 L20,15 C20,13.5 19,12 17.5,12 C17.3,12 17.1,12 17,12.1 C16.6,10.9 15.4,10 14,10 C13.7,10 13.4,10.1 13.1,10.2 C12.6,9.5 11.8,9 11,9 C10.6,9 10.3,9.1 10,9.3 L10,9 C10,7.5 9,6 7.5,6 C6.7,6 6,6.7 6,7.5 L6,16 Z");
                ctx.DrawGeometry(Brushes.White, new Pen(Brushes.Black, 1.8), geom);
            }
            return new Cursor(rtb, new PixelPoint(13, 15));
        }
        catch
        {
            return new Cursor(StandardCursorType.DragMove);
        }
    }
}
