using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovelTranslator.Core.Data;
using NovelTranslator.Core.Export;
using NovelTranslator.Core.Glossary;
using NovelTranslator.Core.Llm;
using NovelTranslator.Core.Models;
using NovelTranslator.Core.Settings;
using NovelTranslator.Core.Translation;

namespace NovelTranslator.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly NovelDatabase _database;
    private readonly NovelRepository _repository;
    private readonly AppSettings _settings;
    public AppSettings Settings => _settings;
    public NovelRepository Repository => _repository;
    private CancellationTokenSource? _translationCts;
    private CancellationTokenSource? _webImportCts;
    private LocalWifiServer? _wifiServer;
    private readonly DispatcherTimer _autoSaveTimer;
    private DispatcherTimer? _fakeProgressTimer;

    // Sub-ViewModels สำหรับลดภาระ God Node
    public StoryListViewModel StoryList { get; } = new();
    public EditorViewModel Editor { get; }

    private const string DefaultPlaceholderText = "Paste your English story here...\n\nThen click the 'แปล' button!";

    // รายการเรื่องและตอน
    public ObservableCollection<Story> Stories => StoryList.Stories;
    public ObservableCollection<Chapter> Chapters => StoryList.Chapters;
    public ObservableCollection<Chapter> FilteredChapters => StoryList.FilteredChapters;

    [ObservableProperty]
    private Story? _selectedStory;

    [ObservableProperty]
    private Chapter? _selectedChapter;

    public string ChapterSearchFilter
    {
        get => StoryList.ChapterSearchFilter;
        set
        {
            if (StoryList.ChapterSearchFilter != value)
            {
                StoryList.ChapterSearchFilter = value;
                OnPropertyChanged();
                RefreshFilteredChapters();
            }
        }
    }

    public int ChapterStatusFilterIndex
    {
        get => StoryList.ChapterStatusFilterIndex;
        set
        {
            if (StoryList.ChapterStatusFilterIndex != value)
            {
                StoryList.ChapterStatusFilterIndex = value;
                OnPropertyChanged();
                RefreshFilteredChapters();
            }
        }
    }

    public string StoryProgressText => StoryList.StoryProgressText;
    public double StoryProgressPercentage => StoryList.StoryProgressPercentage;

    [ObservableProperty]
    private string _chapterTitleEdit = string.Empty;

    [ObservableProperty]
    private bool _isStoryDetailsExpanded;

    [ObservableProperty]
    private string _storyTitleEdit = string.Empty;

    [ObservableProperty]
    private string _storyStyleNotesEdit = string.Empty;

    // UI State & Theme
    [ObservableProperty]
    private bool _isSidebarVisible = true;

    public string SidebarToggleText => IsSidebarVisible ? $"ซ่อนแถบข้าง ({_settings.Shortkeys.ToggleSidebarKey})" : $"แสดงแถบข้าง ({_settings.Shortkeys.ToggleSidebarKey})";
    partial void OnIsSidebarVisibleChanged(bool value) => OnPropertyChanged(nameof(SidebarToggleText));

    public string TranslateKeyText => _settings.Shortkeys.TranslateKey;
    public string FocusModeKeyText => _settings.Shortkeys.FocusModeKey;
    public string SearchReplaceMenuText => $"ค้นหาและแทนที่คำแปล ({_settings.Shortkeys.SearchReplaceKey})";
    public string PrevChapterTooltipText => $"ตอนก่อนหน้า ({_settings.Shortkeys.PrevChapterKey})";
    public string NextChapterTooltipText => $"ตอนถัดไป ({_settings.Shortkeys.NextChapterKey})";
    public string TranslateTooltipText => $"แปลเฉพาะส่วนที่เพิ่มเข้ามาใหม่หรือยังไม่ได้แปล ({_settings.Shortkeys.TranslateKey})";
    public string TranslateAllTooltipText => $"แปลต่อเนื่องทุกตอนที่ยังไม่ได้แปลในเรื่องนี้ ({_settings.Shortkeys.TranslateAllKey})";
    public string SaveTooltipText => $"บันทึกตอนนี้ ({_settings.Shortkeys.SaveKey})";
    public string FocusModeTooltipText => $"โหมดอ่านหนังสือ ({_settings.Shortkeys.FocusModeKey})";

    [ObservableProperty]
    private bool _isFocusMode;

    private bool _sidebarBeforeFocusMode = true;
    public bool IsHeaderVisible => !IsFocusMode;
    public string FocusModeButtonText => IsFocusMode ? "ออกโหมดอ่าน" : "โหมดอ่าน";
    public string ReadingChapterPositionText
    {
        get
        {
            if (SelectedChapter == null || Chapters.Count == 0) return string.Empty;
            int idx = Chapters.IndexOf(SelectedChapter);
            return $"ตอนที่ {idx + 1} จาก {Chapters.Count}";
        }
    }
    public double ReadingLineHeight => Math.Round(EditorFontSize * 1.75);
    public bool HasTranslatedContent => !string.IsNullOrWhiteSpace(TranslatedText);

    partial void OnIsFocusModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsHeaderVisible));
        OnPropertyChanged(nameof(FocusModeButtonText));
        OnPropertyChanged(nameof(ReadingChapterPositionText));
    }

    [ObservableProperty]
    private bool _isOriginalPaneVisible = true;

    [ObservableProperty]
    private bool _isTranslatedPaneVisible = true;

    public bool IsBothPanesVisible => IsOriginalPaneVisible && IsTranslatedPaneVisible;
    public double OriginalPaneToggleOpacity => IsOriginalPaneVisible ? 1.0 : 0.4;
    public double TranslatedPaneToggleOpacity => IsTranslatedPaneVisible ? 1.0 : 0.4;

    public GridLength OriginalColumnWidth => IsOriginalPaneVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    public GridLength SplitterColumnWidth => IsBothPanesVisible ? new GridLength(12) : new GridLength(0);
    public GridLength TranslatedColumnWidth => IsTranslatedPaneVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    partial void OnIsOriginalPaneVisibleChanged(bool value) => NotifyPaneSplitChanged();
    partial void OnIsTranslatedPaneVisibleChanged(bool value) => NotifyPaneSplitChanged();

    private void NotifyPaneSplitChanged()
    {
        OnPropertyChanged(nameof(IsBothPanesVisible));
        OnPropertyChanged(nameof(OriginalPaneToggleOpacity));
        OnPropertyChanged(nameof(TranslatedPaneToggleOpacity));
        OnPropertyChanged(nameof(OriginalColumnWidth));
        OnPropertyChanged(nameof(SplitterColumnWidth));
        OnPropertyChanged(nameof(TranslatedColumnWidth));
    }

    [ObservableProperty]
    private bool _isSyncScrollEnabled = true;

    [ObservableProperty]
    private int _editorFontSize = 20;

    partial void OnEditorFontSizeChanged(int value)
    {
        OnPropertyChanged(nameof(ReadingLineHeight));
        if (value > 0 && _settings != null)
        {
            _settings.EditorFontSize = value;
            _settings.Save();
        }
    }

    public ObservableCollection<string> AvailableEditorFonts { get; } = new(AppSettings.DefaultEditorFonts);

    [ObservableProperty]
    private string _editorFontFamily = "Tahoma";

    partial void OnEditorFontFamilyChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && _settings != null)
        {
            _settings.EditorFontFamily = value;
            _settings.Save();
        }
    }

    [ObservableProperty]
    private string _themeMode = "Light";

    public string ThemeButtonText => ThemeMode switch
    {
        "Dark" => "โหมดมืด",
        "Sepia" => "ถนอมสายตา",
        _ => "โหมดสว่าง"
    };

    public bool IsLightTheme => ThemeMode != "Dark" && ThemeMode != "Sepia";
    public bool IsDarkTheme => ThemeMode == "Dark";
    public bool IsSepiaTheme => ThemeMode == "Sepia";
    public string ThemeButtonTooltip => $"สลับโหมดสี: สว่าง / มืด / ถนอมสายตา (ปัจจุบัน: {ThemeButtonText})";

    public int ThemeModeIndex
    {
        get => ThemeMode switch
        {
            "Dark" => 1,
            "Sepia" => 2,
            _ => 0
        };
        set
        {
            string mode = value switch
            {
                1 => "Dark",
                2 => "Sepia",
                _ => "Light"
            };
            if (ThemeMode != mode)
            {
                ThemeMode = mode;
                _settings.ThemeMode = mode;
                _settings.Save();
                App.ApplyTheme(mode);
            }
        }
    }

    partial void OnThemeModeChanged(string value)
    {
        OnPropertyChanged(nameof(ThemeButtonText));
        OnPropertyChanged(nameof(ThemeButtonTooltip));
        OnPropertyChanged(nameof(ThemeModeIndex));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsSepiaTheme));
    }

    [ObservableProperty]
    private double _brightnessLevel = 100.0;

    public double DimOverlayOpacity => Math.Clamp((100.0 - BrightnessLevel) / 100.0, 0.0, 0.70);
    public string BrightnessDisplayText => $"{Math.Round(BrightnessLevel):0}%";
    public double MmbScrollSensitivity => _settings.MmbScrollSensitivity is >= 0.2 and <= 5.0 ? _settings.MmbScrollSensitivity : 1.0;

    partial void OnBrightnessLevelChanged(double value)
    {
        double clamped = Math.Clamp(value, 30.0, 100.0);
        if (Math.Abs(clamped - value) > 0.01)
        {
            BrightnessLevel = clamped;
            return;
        }
        OnPropertyChanged(nameof(DimOverlayOpacity));
        OnPropertyChanged(nameof(BrightnessDisplayText));
        if (_settings != null)
        {
            _settings.BrightnessLevel = clamped;
            _settings.Save();
        }
    }

    [ObservableProperty]
    private string _activeModelDisplay = "ยังไม่ได้ตั้งค่าโมเดล";

    // ช่องข้อความหลัก
    [ObservableProperty]
    private string _originalText = string.Empty;

    [ObservableProperty]
    private string _translatedText = string.Empty;

    // สถิติและสถานะ
    [ObservableProperty]
    private int _originalWordCount;

    [ObservableProperty]
    private int _originalParagraphCount;

    [ObservableProperty]
    private int _translatedWordCount;

    [ObservableProperty]
    private string _chapterStatusText = "ยังไม่แปล";

    [ObservableProperty]
    private string _chapterStatusColor = "#737373";

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private bool _isTranslating;

    [ObservableProperty]
    private string _saveStatusText = string.Empty;

    [ObservableProperty]
    private string _saveStateColor = "#16A34A";

    private bool _isLoadingChapter;

    // Error Debug Details
    [ObservableProperty]
    private string _lastErrorDetails = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    // ความคืบหน้าการแปล
    [ObservableProperty]
    private double _translationProgressPercentage;

    [ObservableProperty]
    private bool _isTranslatingIndeterminate;

    // Auto-Glossary Notification Banner
    [ObservableProperty]
    private bool _hasAutoGlossaryNotification;

    [ObservableProperty]
    private string _autoGlossaryNotificationText = string.Empty;

    // Actions สำหรับเปิดหน้าต่างย่อย
    public Func<Story, Task>? RequestOpenGlossary { get; set; }
    public Func<Task>? RequestOpenSettings { get; set; }
    public Func<Story, Chapter?, Task>? RequestOpenSearchReplace { get; set; }
    public Func<string, Task<string?>>? RequestSaveFileDialog { get; set; }
    public Func<string, Task<string[]?>>? RequestOpenFilesDialog { get; set; }
    public Func<string, string, Task<string?>>? RequestPromptDialog { get; set; }
    public Func<string, Task<bool>>? RequestConfirmDialog { get; set; }
    public Func<string, Task>? RequestCopyToClipboard { get; set; }
    public Action<string>? RequestShowErrorDialog { get; set; }
    public Func<string?, Task>? RequestOpenBrowserImport { get; set; }
    public Func<Task<WebMultiImportOptions?>>? RequestWebImportDialog { get; set; }
    public Func<WebMultiImportOptions?, Task>? RequestOpenBrowserMultiImport { get; set; }
    public Func<string, Task<ExportContentMode?>>? RequestExportContentModeDialog { get; set; }
    public Func<LocalWifiServer, Task>? RequestShowWifiServerDialog { get; set; }
    public Action? RequestSelectAllChapters { get; set; }

    public bool IsWifiServerRunning => _wifiServer?.IsRunning == true;
    public string WifiServerMenuText => IsWifiServerRunning
        ? $"เซิร์ฟเวอร์อ่านบนมือถือ ({_wifiServer!.LanUrl})"
        : "เปิดเซิร์ฟเวอร์อ่านบนมือถือ (Wi-Fi)...";
    public Action? RequestClearChapterSelection { get; set; }
    public Action? RequestToggleFullScreen { get; set; }

    [RelayCommand]
    public void ToggleFullScreen() => RequestToggleFullScreen?.Invoke();

    public MainViewModel() : this(AppSettings.Load(), CreateInitializedDatabase(), deferInitialLoad: false) { }

    public MainViewModel(AppSettings settings, NovelDatabase database, bool deferInitialLoad = true)
    {
        _settings = settings;
        _editorFontSize = Math.Clamp(_settings.EditorFontSize > 0 ? _settings.EditorFontSize : 20, 12, 32);
        if (!string.IsNullOrWhiteSpace(_settings.EditorFontFamily))
        {
            _editorFontFamily = _settings.EditorFontFamily;
        }
        _themeMode = string.IsNullOrWhiteSpace(_settings.ThemeMode) ? "Light" : _settings.ThemeMode;
        _brightnessLevel = _settings.BrightnessLevel is >= 30.0 and <= 100.0 ? _settings.BrightnessLevel : 100.0;
        UpdateActiveModelDisplay();
        _database = database;
        _repository = new NovelRepository(_database);
        Editor = new EditorViewModel(_repository);
        Editor.OnParagraphContentChanged = () =>
        {
            OriginalText = Editor.CombineSourceParagraphs();
            TranslatedText = Editor.CombineTranslatedParagraphs();
        };
        Editor.OnRetranslateParagraphAction = async (pair) =>
        {
            await RetranslateParagraphInPlaceAsync(pair);
        };

        // ตั้งค่า Auto-save Debounce
        double autoSaveSeconds = _settings.AutoSaveDelaySeconds > 0 ? _settings.AutoSaveDelaySeconds : 1.5;
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(autoSaveSeconds) };
        _autoSaveTimer.Tick += async (_, _) =>
        {
            _autoSaveTimer.Stop();
            await SaveCurrentChapterInternalAsync(isAutoSave: true);
            if (SelectedStory != null)
            {
                await Editor.RefreshActiveGlossaryAsync(SelectedStory.Id, OriginalText);
            }
        };

        if (!deferInitialLoad)
        {
            _ = LoadInitialDataAsync();
        }
    }

    public bool HasPendingAutoSave => _autoSaveTimer.IsEnabled;

    public async Task FlushPendingSaveAsync()
    {
        if (_autoSaveTimer.IsEnabled)
        {
            _autoSaveTimer.Stop();
            await SaveCurrentChapterInternalAsync(isAutoSave: true);
        }
    }

    private static NovelDatabase CreateInitializedDatabase()
    {
        var db = new NovelDatabase();
        db.Initialize();
        return db;
    }

    public void ReloadSettings()
    {
        var fresh = AppSettings.Load();
        _settings.Backend = fresh.Backend;
        _settings.BaseUrl = fresh.BaseUrl;
        _settings.ApiKey = fresh.ApiKey;
        _settings.ModelName = fresh.ModelName;
        _settings.Temperature = fresh.Temperature;
        _settings.TopP = fresh.TopP;
        _settings.MaxTokens = fresh.MaxTokens;
        _settings.FrequencyPenalty = fresh.FrequencyPenalty;
        _settings.RequestTimeoutSeconds = fresh.RequestTimeoutSeconds;
        _settings.MaxChunkWords = fresh.MaxChunkWords;
        _settings.ContextParagraphCount = fresh.ContextParagraphCount;
        _settings.CustomSystemPrompt = fresh.CustomSystemPrompt;
        _settings.CustomPolishSystemPrompt = fresh.CustomPolishSystemPrompt;
        _settings.CustomGlossaryPrompt = fresh.CustomGlossaryPrompt;
        _settings.EnableCrossChapterContext = fresh.EnableCrossChapterContext;
        _settings.EnableLookAheadContext = fresh.EnableLookAheadContext;
        _settings.PovMode = fresh.PovMode;
        _settings.StripThinkTags = fresh.StripThinkTags;
        _settings.MaxEnglishRatioPercent = fresh.MaxEnglishRatioPercent;
        _settings.MaxRetryAttempts = fresh.MaxRetryAttempts;
        _settings.CustomStripPrefixes = fresh.CustomStripPrefixes;
        _settings.GlossaryScanMaxWords = fresh.GlossaryScanMaxWords;
        _settings.GlossaryModelName = fresh.GlossaryModelName;
        _settings.EnableAutoGlossaryScan = fresh.EnableAutoGlossaryScan;
        _settings.EnablePolishMode = fresh.EnablePolishMode;
        _settings.PolishModelName = fresh.PolishModelName;
        _settings.Shortkeys = fresh.Shortkeys;
        _settings.ThemeMode = fresh.ThemeMode;
        _settings.BrightnessLevel = fresh.BrightnessLevel;
        _settings.TranslationAppearance = fresh.TranslationAppearance;
        _settings.EditorFontFamily = fresh.EditorFontFamily;
        _settings.EditorFontSize = fresh.EditorFontSize;
        _settings.AutoSaveDelaySeconds = fresh.AutoSaveDelaySeconds;
        _settings.MmbScrollSensitivity = fresh.MmbScrollSensitivity;
        _settings.LastSelectedStoryId = fresh.LastSelectedStoryId;
        _settings.LastSelectedChapterId = fresh.LastSelectedChapterId;

        if (!string.IsNullOrWhiteSpace(fresh.ThemeMode) && ThemeMode != fresh.ThemeMode)
        {
            ThemeMode = fresh.ThemeMode;
            App.ApplyTheme(fresh.ThemeMode);
        }
        if (fresh.BrightnessLevel is >= 30.0 and <= 100.0)
        {
            BrightnessLevel = fresh.BrightnessLevel;
        }

        if (!string.IsNullOrWhiteSpace(fresh.EditorFontFamily))
        {
            EditorFontFamily = fresh.EditorFontFamily;
        }
        if (fresh.EditorFontSize > 0)
        {
            EditorFontSize = Math.Clamp(fresh.EditorFontSize, 12, 36);
        }
        if (fresh.AutoSaveDelaySeconds > 0)
        {
            _autoSaveTimer.Interval = TimeSpan.FromSeconds(fresh.AutoSaveDelaySeconds);
        }

        OnPropertyChanged(nameof(SidebarToggleText));
        OnPropertyChanged(nameof(TranslateKeyText));
        OnPropertyChanged(nameof(FocusModeKeyText));
        OnPropertyChanged(nameof(SearchReplaceMenuText));
        OnPropertyChanged(nameof(PrevChapterTooltipText));
        OnPropertyChanged(nameof(NextChapterTooltipText));
        OnPropertyChanged(nameof(TranslateTooltipText));
        OnPropertyChanged(nameof(TranslateAllTooltipText));
        OnPropertyChanged(nameof(SaveTooltipText));
        OnPropertyChanged(nameof(FocusModeTooltipText));

        UpdateActiveModelDisplay();
    }

    private void UpdateActiveModelDisplay()
    {
        string model = string.IsNullOrWhiteSpace(_settings.ModelName) ? "ยังไม่ระบุโมเดล" : _settings.ModelName;
        string polishSuffix = _settings.EnablePolishMode
            ? (string.IsNullOrWhiteSpace(_settings.PolishModelName)
                ? " (+เกลาคำ)"
                : $" (+เกลา: {_settings.PolishModelName.Trim()})")
            : string.Empty;
        ActiveModelDisplay = $"{_settings.Backend} · {model}{polishSuffix}";
    }

    [RelayCommand]
    public void CycleTheme()
    {
        ThemeMode = ThemeMode switch
        {
            "Light" => "Dark",
            "Dark" => "Sepia",
            _ => "Light"
        };
        _settings.ThemeMode = ThemeMode;
        _settings.Save();
        App.ApplyTheme(ThemeMode);
    }

    [RelayCommand]
    public void ToggleSidebar() => IsSidebarVisible = !IsSidebarVisible;

    [RelayCommand]
    public void ToggleFocusMode()
    {
        if (!IsFocusMode)
        {
            _sidebarBeforeFocusMode = IsSidebarVisible;
            if (_autoSaveTimer.IsEnabled)
            {
                _autoSaveTimer.Stop();
                _ = SaveCurrentChapterInternalAsync(isAutoSave: true);
            }
            IsFocusMode = true;
            IsSidebarVisible = false;
        }
        else
        {
            IsFocusMode = false;
            IsSidebarVisible = _sidebarBeforeFocusMode;
        }
    }

    [RelayCommand]
    public void ToggleOriginalPane()
    {
        if (IsOriginalPaneVisible && !IsTranslatedPaneVisible)
        {
            IsTranslatedPaneVisible = true;
            IsOriginalPaneVisible = false;
        }
        else
        {
            IsOriginalPaneVisible = !IsOriginalPaneVisible;
        }
    }

    [RelayCommand]
    public void ToggleTranslatedPane()
    {
        if (IsTranslatedPaneVisible && !IsOriginalPaneVisible)
        {
            IsOriginalPaneVisible = true;
            IsTranslatedPaneVisible = false;
        }
        else
        {
            IsTranslatedPaneVisible = !IsTranslatedPaneVisible;
        }
    }

    [RelayCommand]
    public void IncreaseFontSize()
    {
        EditorFontSize = Math.Clamp(EditorFontSize + 2, 12, 32);
    }

    [RelayCommand]
    public void DecreaseFontSize()
    {
        EditorFontSize = Math.Clamp(EditorFontSize - 2, 12, 32);
    }

    [RelayCommand]
    public void ToggleStoryDetails() => IsStoryDetailsExpanded = !IsStoryDetailsExpanded;

    [RelayCommand]
    public void ApplyStylePreset(string presetKey)
    {
        string preset = presetKey switch
        {
            "Modern" => "นิยายยุคปัจจุบัน/ไลท์โนเวล: บุรุษที่ 1 ใช้ ผม/ฉัน บทสนทนาเป็นธรรมชาติ ลื่นไหล ไม่ใช้คำราชาศัพท์หรือสำนวนโบราณ",
            "Wuxia" => "นิยายจีนโบราณ/กำลังภายใน: ใช้สรรพนาม ข้า/เจ้า/ท่าน คำบรรยายสละสลวยทรงพลัง คงกลิ่นอายยุทธภพ",
            "Fantasy" => "นิยายแฟนตาซี/ผจญภัย: สำนวนนิยายแปลสากล กระชับ เห็นภาพฉากแอ็กชันและเวทมนตร์ชัดเจน สรรพนามตามฐานะตัวละคร",
            "Dark" => "นิยายดาร์ก/สืบสวน/จริงจัง: น้ำเสียงขรึม กระชับ ลุ่มลึก ไม่ใช้คำฟุ่มเฟือย สื่ออารมณ์กดดันได้ชัดเจน",
            _ => string.Empty
        };

        if (!string.IsNullOrEmpty(preset))
        {
            StoryStyleNotesEdit = preset;
        }
    }

    [RelayCommand]
    public async Task SaveStoryDetailsAsync()
    {
        if (SelectedStory == null || string.IsNullOrWhiteSpace(StoryTitleEdit)) return;
        SelectedStory.Title = StoryTitleEdit.Trim();
        SelectedStory.StyleNotes = StoryStyleNotesEdit?.Trim() ?? string.Empty;
        await _repository.UpdateStoryAsync(SelectedStory);
        IsStoryDetailsExpanded = false;
        SaveStatusText = $"บันทึกข้อมูลเรื่อง \"{SelectedStory.Title}\" แล้ว";
    }

    internal async Task LoadInitialDataAsync()
    {
        var stories = await _repository.GetAllStoriesAsync();
        Stories.Clear();
        foreach (var s in stories)
        {
            Stories.Add(s);
        }

        if (Stories.Count == 0)
        {
            // สร้างเรื่องเริ่มต้นให้พร้อมใช้งาน
            var defaultStory = new Story
            {
                Title = "นิยายเรื่องแรกของฉัน",
                SourceLang = "English",
                TargetLang = "Thai",
                StyleNotes = "น้ำเสียงเป็นกันเอง ใช้ ฉัน/เธอ กับตัวเอก"
            };
            defaultStory.Id = await _repository.InsertStoryAsync(defaultStory);
            Stories.Add(defaultStory);
        }

        // กู้คืนเรื่องล่าสุดที่เลือกไว้
        if (_settings.LastSelectedStoryId.HasValue)
        {
            SelectedStory = Stories.FirstOrDefault(s => s.Id == _settings.LastSelectedStoryId.Value)
                            ?? Stories.FirstOrDefault();
        }
        else
        {
            SelectedStory = Stories.FirstOrDefault();
        }
    }

    async partial void OnSelectedStoryChanged(Story? value)
    {
        Chapters.Clear();
        FilteredChapters.Clear();
        SelectedChapter = null;
        OriginalText = string.Empty;
        TranslatedText = string.Empty;

        if (value != null)
        {
            StoryTitleEdit = value.Title;
            StoryStyleNotesEdit = value.StyleNotes;

            // บันทึกเรื่องล่าสุด
            _settings.LastSelectedStoryId = value.Id;
            _settings.Save();

            var chapters = await _repository.GetChaptersByStoryAsync(value.Id);
            foreach (var c in chapters)
            {
                Chapters.Add(c);
            }

            if (Chapters.Count == 0)
            {
                // สร้างตอนเริ่มต้น
                var firstChap = new Chapter
                {
                    StoryId = value.Id,
                    Ordinal = 1,
                    Title = "ตอนที่ 1",
                    OriginalText = DefaultPlaceholderText,
                    Status = ChapterStatus.NotTranslated
                };
                firstChap.Id = await _repository.InsertChapterAsync(firstChap);
                Chapters.Add(firstChap);
            }

            RefreshFilteredChapters();

            // กู้คืนตอนล่าสุดของเรื่องนี้ถ้ามี
            if (_settings.LastSelectedChapterId.HasValue)
            {
                SelectedChapter = Chapters.FirstOrDefault(c => c.Id == _settings.LastSelectedChapterId.Value)
                                  ?? Chapters.FirstOrDefault();
            }
            else
            {
                SelectedChapter = Chapters.FirstOrDefault();
            }
        }
        else
        {
            StoryTitleEdit = string.Empty;
            StoryStyleNotesEdit = string.Empty;
        }
    }

    private void RefreshFilteredChapters()
    {
        var current = SelectedChapter;
        StoryList.RefreshFiltered(current);
        OnPropertyChanged(nameof(StoryProgressText));
        OnPropertyChanged(nameof(StoryProgressPercentage));

        if (current != null && FilteredChapters.Contains(current) && SelectedChapter != current)
        {
            SelectedChapter = current;
        }
    }

    private void UpdateStoryProgress()
    {
        StoryList.UpdateStoryProgress();
        OnPropertyChanged(nameof(StoryProgressText));
        OnPropertyChanged(nameof(StoryProgressPercentage));
        OnPropertyChanged(nameof(ReadingChapterPositionText));
    }

    partial void OnSelectedChapterChanging(Chapter? value)
    {
        if (SelectedChapter != null && !ReferenceEquals(SelectedChapter, value) && _autoSaveTimer.IsEnabled)
        {
            _autoSaveTimer.Stop();
            var outgoing = SelectedChapter;
            outgoing.OriginalText = OriginalText;
            outgoing.TranslatedText = TranslatedText;
            _ = SaveChapterSnapshotAsync(outgoing, isAutoSave: true);
        }
    }

    partial void OnSelectedChapterChanged(Chapter? value)
    {
        OnPropertyChanged(nameof(ReadingChapterPositionText));
        OnPropertyChanged(nameof(HasTranslatedContent));
        _isLoadingChapter = true;
        try
        {
            if (value != null)
            {
                // บันทึกตอนล่าสุด
                _settings.LastSelectedChapterId = value.Id;
                _settings.Save();

                ChapterTitleEdit = value.Title ?? string.Empty;
                OriginalText = value.OriginalText ?? string.Empty;
                TranslatedText = value.TranslatedText ?? string.Empty;
                UpdateChapterStatusDisplay(value.Status);
                UpdateWordAndParagraphCounts(OriginalText);
                TranslatedWordCount = Chunker.EstimateWordCount(TranslatedText);
                SaveStatusText = $"บันทึกแล้ว {value.UpdatedAt.ToLocalTime():HH:mm:ss}";
                SaveStateColor = "#4CC38A";

                if (Editor.IsParagraphMode)
                {
                    Editor.SyncParagraphPairs(OriginalText, TranslatedText);
                }
                if (SelectedStory != null)
                {
                    _ = Editor.RefreshActiveGlossaryAsync(SelectedStory.Id, OriginalText);
                }
            }
            else
            {
                ChapterTitleEdit = string.Empty;
                OriginalText = string.Empty;
                TranslatedText = string.Empty;
                TranslatedWordCount = 0;
                ChapterStatusText = "";
                SaveStatusText = "";
            }
        }
        finally
        {
            _isLoadingChapter = false;
        }
    }

    private void MarkUnsavedChanges()
    {
        SaveStatusText = "ยังไม่ได้บันทึก";
        SaveStateColor = "#F59E0B";
        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }

    partial void OnChapterTitleEditChanged(string value)
    {
        if (!_isLoadingChapter && SelectedChapter != null && !string.IsNullOrWhiteSpace(value) && SelectedChapter.Title != value)
        {
            SelectedChapter.Title = value;
            MarkUnsavedChanges();
        }
    }

    partial void OnOriginalTextChanged(string value)
    {
        UpdateWordAndParagraphCounts(value);
        if (!_isLoadingChapter && SelectedChapter != null && SelectedChapter.OriginalText != value)
        {
            SelectedChapter.OriginalText = value;
            if (Editor.IsParagraphMode && !Editor.IsUpdatingFromParagraph)
            {
                Editor.SyncParagraphPairs(value, TranslatedText);
            }
            MarkUnsavedChanges();
        }
    }

    partial void OnTranslatedTextChanged(string value)
    {
        TranslatedWordCount = Chunker.EstimateWordCount(value);
        OnPropertyChanged(nameof(HasTranslatedContent));
        if (!_isLoadingChapter && SelectedChapter != null && SelectedChapter.TranslatedText != value && !IsTranslating)
        {
            SelectedChapter.TranslatedText = value;
            SelectedChapter.Status = ChapterStatus.Edited;
            if (Editor.IsParagraphMode && !Editor.IsUpdatingFromParagraph)
            {
                Editor.SyncParagraphPairs(OriginalText, value);
            }
            UpdateChapterStatusDisplay(SelectedChapter.Status);
            UpdateStoryProgress();
            MarkUnsavedChanges();
        }
    }

    private void UpdateWordAndParagraphCounts(string text)
    {
        OriginalWordCount = Chunker.EstimateWordCount(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            OriginalParagraphCount = 0;
        }
        else
        {
            OriginalParagraphCount = text.Replace("\r\n", "\n")
                .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }

    private void UpdateChapterStatusDisplay(ChapterStatus status)
    {
        ChapterStatusText = status switch
        {
            ChapterStatus.NotTranslated => "ยังไม่แปล",
            ChapterStatus.Translating => "กำลังแปล",
            ChapterStatus.Translated => "แปลแล้ว",
            ChapterStatus.Edited => "แก้ไขแล้ว",
            _ => ""
        };
        ChapterStatusColor = status switch
        {
            ChapterStatus.NotTranslated => "#737373",
            ChapterStatus.Translating => "#F59E0B",
            ChapterStatus.Translated => "#4CC38A",
            ChapterStatus.Edited => "#3B82F6",
            _ => "#737373"
        };
    }

    // ================= คำสั่งควบคุมการแปล =================

    [RelayCommand]
    public async Task StartTranslationAsync()
    {
        await StartTranslationInternalAsync(forceRetranslate: false);
    }

    [RelayCommand]
    public async Task TranslateAllChaptersAsync()
    {
        if (SelectedStory == null || IsTranslating) return;

        var pending = Chapters.Where(c => c.Status == ChapterStatus.NotTranslated
                                       && !string.IsNullOrWhiteSpace(c.OriginalText)
                                       && c.OriginalText.Trim() != DefaultPlaceholderText).ToList();
        if (pending.Count == 0)
        {
            ProgressText = "แปลครบทุกตอนแล้ว";
            return;
        }

        if (RequestConfirmDialog != null)
        {
            bool ok = await RequestConfirmDialog($"ต้องการแปลต่อเนื่องทุกตอนที่ยังไม่แปล จำนวน {pending.Count} ตอน ใช่หรือไม่?");
            if (!ok) return;
        }

        for (int i = 0; i < pending.Count; i++)
        {
            SelectedChapter = pending[i];
            ProgressText = $"กำลังแปลต่อเนื่อง {i + 1}/{pending.Count} · {SelectedChapter.Title}";
            await StartTranslationInternalAsync(forceRetranslate: false);

            // หากถูกกดหยุดหรือเกิด Error ให้หยุดคิวทันที
            if (HasError || SelectedChapter.Status != ChapterStatus.Translated)
            {
                break;
            }
        }
        UpdateStoryProgress();
    }

    [RelayCommand]
    public async Task RetranslateAllAsync()
    {
        if (SelectedChapter == null || string.IsNullOrWhiteSpace(OriginalText)) return;

        if (RequestConfirmDialog != null)
        {
            bool ok = await RequestConfirmDialog("คุณต้องการล้างคำแปลเดิมแล้วแปลใหม่ทั้งหมดสำหรับตอนนี้ใช่หรือไม่?");
            if (!ok) return;
        }

        await StartTranslationInternalAsync(forceRetranslate: true);
    }

    private void StartFakeProgressTimer(double basePercent, double spanPercent)
    {
        _fakeProgressTimer?.Stop();
        double currentSubProgress = 0.0;
        _fakeProgressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _fakeProgressTimer.Tick += (_, _) =>
        {
            currentSubProgress += (42.0 - currentSubProgress) * 0.08;
            TranslationProgressPercentage = Math.Min(99.0, basePercent + (spanPercent * (Math.Min(45.0, currentSubProgress) / 100.0)));
        };
        _fakeProgressTimer.Start();
    }

    private void StopFakeProgressTimer()
    {
        _fakeProgressTimer?.Stop();
        _fakeProgressTimer = null;
    }

    private async Task StartTranslationInternalAsync(bool forceRetranslate)
    {
        if (SelectedStory == null || SelectedChapter == null) return;
        if (string.IsNullOrWhiteSpace(OriginalText)) return;

        var story = SelectedStory;
        var chapter = SelectedChapter;

        // ซิงค์ข้อความต้นฉบับล่าสุดเข้า Chapter ทันที
        chapter.OriginalText = OriginalText;
        if (forceRetranslate)
        {
            chapter.TranslatedText = string.Empty;
            TranslatedText = string.Empty;
        }
        else
        {
            // แสดงเฉพาะคำแปลเดิมของตอนนี้เท่านั้น (ถ้ายังไม่มีก็จะเป็นค่าว่าง)
            TranslatedText = chapter.TranslatedText ?? string.Empty;
        }

        IsTranslating = true;
        HasError = false;
        LastErrorDetails = string.Empty;
        TranslationProgressPercentage = 0;
        IsTranslatingIndeterminate = false;
        _translationCts = new CancellationTokenSource();

        StartFakeProgressTimer(0, 100.0);

        var llmClient = new LlmClient(_settings);
        var pipeline = new TranslationPipeline(_repository, llmClient, _settings);

        int lastReportedChunk = 0;
        var progress = new Progress<TranslationProgressInfo>(info =>
        {
            if (!string.IsNullOrEmpty(info.CombinedTranslatedTextSoFar) && ReferenceEquals(SelectedChapter, chapter))
            {
                TranslatedText = info.CombinedTranslatedTextSoFar;
            }
            if (!string.IsNullOrEmpty(info.StatusMessage))
            {
                ProgressText = info.StatusMessage;
            }

            int total = Math.Max(1, info.TotalChunks);
            int currentChunk = Math.Clamp(info.CurrentChunkIndex, 1, total);
            double chunkBase = ((double)(currentChunk - 1) / total) * 100.0;
            double chunkSpan = 100.0 / total;

            if (currentChunk != lastReportedChunk)
            {
                lastReportedChunk = currentChunk;
                StartFakeProgressTimer(chunkBase, chunkSpan);
            }

            if (info.StreamedWordCount > 0)
            {
                StopFakeProgressTimer();
                double withinChunk = 50.0 + Math.Min(45.0, (info.StreamedWordCount / 200.0) * 45.0);
                double targetPercent = chunkBase + (chunkSpan * (withinChunk / 100.0));
                TranslationProgressPercentage = Math.Max(TranslationProgressPercentage, targetPercent);
            }
        });

        try
        {
            ProgressText = forceRetranslate ? "กำลังแปลใหม่ทั้งหมดตั้งแต่เริ่มต้น..." : "กำลังเริ่มขั้นตอนการแปล...";
            await Task.Run(async () =>
            {
                await pipeline.TranslateChapterAsync(story, chapter, progress, forceRetranslate, _translationCts.Token);
            });
            if (ReferenceEquals(SelectedChapter, chapter))
            {
                TranslatedText = chapter.TranslatedText ?? string.Empty;
                UpdateChapterStatusDisplay(chapter.Status);
                SaveStatusText = $"บันทึกอัตโนมัติ {DateTime.Now:HH:mm:ss}";
                SaveStateColor = "#4CC38A";
            }
            UpdateStoryProgress();
            StopFakeProgressTimer();
            TranslationProgressPercentage = 100;

            if (_settings.EnableAutoGlossaryScan &&
                chapter.Status == ChapterStatus.Translated &&
                !_translationCts.IsCancellationRequested)
            {
                ProgressText = "แปลเสร็จสิ้น · กำลังสแกนชื่อเฉพาะเข้า Glossary อัตโนมัติ...";
                try
                {
                    var addedTerms = await AutoExtractAndSaveGlossaryAsync(story, chapter, _translationCts.Token);
                    ProgressText = addedTerms.Count > 0
                        ? $"แปลเสร็จสิ้น · เพิ่มชื่อเฉพาะเข้า Glossary {addedTerms.Count} คำ"
                        : "แปลเสร็จสิ้น";
                }
                catch (OperationCanceledException)
                {
                    ProgressText = "แปลเสร็จสิ้น (ข้ามการสแกนศัพท์)";
                }
                catch
                {
                    ProgressText = "แปลเสร็จสิ้น";
                }
            }
            else
            {
                ProgressText = "แปลเสร็จสิ้น";
            }
        }
        catch (OperationCanceledException)
        {
            StopFakeProgressTimer();
            ProgressText = "หยุดการแปลแล้ว";
            await PersistInterruptedChapterStatusAsync(chapter);
        }
        catch (Exception ex)
        {
            StopFakeProgressTimer();
            HasError = true;
            LastErrorDetails = ex.ToString();
            string firstLine = ex.Message.Split('\n')[0];
            ProgressText = $"ข้อผิดพลาด · {firstLine}";
            await PersistInterruptedChapterStatusAsync(chapter);
        }
        finally
        {
            StopFakeProgressTimer();
            IsTranslating = false;
            IsTranslatingIndeterminate = false;
            if (Editor.IsParagraphMode && ReferenceEquals(SelectedChapter, chapter))
            {
                Editor.SyncParagraphPairs(OriginalText, TranslatedText);
            }
        }
    }

    private async Task PersistInterruptedChapterStatusAsync(Chapter chapter)
    {
        chapter.Status = string.IsNullOrWhiteSpace(chapter.TranslatedText)
            ? ChapterStatus.NotTranslated
            : ChapterStatus.Edited;
        await _repository.UpdateChapterAsync(chapter);
        if (ReferenceEquals(SelectedChapter, chapter))
        {
            UpdateChapterStatusDisplay(chapter.Status);
        }
        UpdateStoryProgress();
    }

    [RelayCommand]
    public async Task CopyErrorDetailsAsync()
    {
        if (!string.IsNullOrEmpty(LastErrorDetails) && RequestCopyToClipboard != null)
        {
            await RequestCopyToClipboard(LastErrorDetails);
            SaveStatusText = "คัดลอกรายละเอียดข้อผิดพลาดแล้ว";
        }
    }

    [RelayCommand]
    public void ShowErrorDialog()
    {
        if (!string.IsNullOrEmpty(LastErrorDetails) && RequestShowErrorDialog != null)
        {
            RequestShowErrorDialog(LastErrorDetails);
        }
    }

    [RelayCommand]
    public void StopTranslation()
    {
        _translationCts?.Cancel();
        _webImportCts?.Cancel();
    }

    [RelayCommand]
    public void ToggleParagraphMode()
    {
        SetParagraphMode(!Editor.IsParagraphMode);
    }

    [RelayCommand]
    public void SetParagraphMode(bool enabled)
    {
        if (Editor.IsParagraphMode == enabled) return;

        if (enabled)
        {
            Editor.SyncParagraphPairs(OriginalText, TranslatedText);
            Editor.IsParagraphMode = true;
        }
        else
        {
            string combinedTrans = Editor.CombineTranslatedParagraphs();
            string combinedSource = Editor.CombineSourceParagraphs();
            if (!string.IsNullOrEmpty(combinedTrans)) TranslatedText = combinedTrans;
            if (!string.IsNullOrEmpty(combinedSource)) OriginalText = combinedSource;
            Editor.IsParagraphMode = false;
        }
    }

    public async Task RetranslateParagraphInPlaceAsync(ParagraphPairViewModel pair)
    {
        if (SelectedStory == null || SelectedChapter == null || string.IsNullOrWhiteSpace(pair.SourceText)) return;

        _translationCts?.Cancel();
        _translationCts?.Dispose();
        _translationCts = new CancellationTokenSource();
        var ct = _translationCts.Token;

        IsTranslating = true;
        ProgressText = $"กำลังแปลใหม่เฉพาะย่อหน้าที่ {pair.Index}...";
        var llmClient = new LlmClient(_settings);
        var pipeline = new TranslationPipeline(_repository, llmClient, _settings);

        var prevPair = pair.Index > 1 && pair.Index - 2 < Editor.ParagraphPairs.Count
            ? Editor.ParagraphPairs[pair.Index - 2]
            : null;
        string? surroundingContext = prevPair?.SourceText;
        string? surroundingTranslationContext = prevPair?.TranslatedText;

        try
        {
            string newTranslation = await Task.Run(async () =>
            {
                return await pipeline.RetranslateParagraphAsync(
                    SelectedStory,
                    pair.SourceText,
                    surroundingContext,
                    ct,
                    surroundingTranslationContext);
            }, ct);

            if (!string.IsNullOrWhiteSpace(newTranslation))
            {
                pair.TranslatedText = newTranslation.Trim();
                TranslatedText = Editor.CombineTranslatedParagraphs();
                ProgressText = $"แปลใหม่ย่อหน้าที่ {pair.Index} สำเร็จแล้ว";
                MarkUnsavedChanges();
            }
        }
        catch (OperationCanceledException)
        {
            ProgressText = $"ยกเลิกการแปลใหม่ย่อหน้าที่ {pair.Index} แล้ว";
        }
        catch (Exception ex)
        {
            ProgressText = $"แปลใหม่ย่อหน้าที่ {pair.Index} ล้มเหลว · {ex.Message}";
        }
        finally
        {
            IsTranslating = false;
            _translationCts?.Dispose();
            _translationCts = null;
        }
    }

    // ================= จัดการเรื่อง (Story) =================

    [RelayCommand]
    public async Task CreateStoryAsync()
    {
        if (RequestPromptDialog == null) return;
        string? title = await RequestPromptDialog("สร้างเรื่องใหม่", "กรุณาระบุชื่อเรื่อง:");
        if (string.IsNullOrWhiteSpace(title)) return;

        var story = new Story
        {
            Title = title.Trim(),
            SourceLang = "English",
            TargetLang = "Thai",
            StyleNotes = "น้ำเสียงเป็นกันเอง ใช้ ฉัน/เธอ กับตัวเอก"
        };
        story.Id = await _repository.InsertStoryAsync(story);
        Stories.Insert(0, story);
        SelectedStory = story;
    }

    [RelayCommand]
    public async Task DeleteStoryAsync()
    {
        if (SelectedStory == null || RequestConfirmDialog == null) return;
        bool ok = await RequestConfirmDialog($"คุณแน่ใจหรือไม่ว่าต้องการลบเรื่อง \"{SelectedStory.Title}\" รวมทั้งทุกตอนในเรื่องนี้?");
        if (!ok) return;

        _autoSaveTimer.Stop();
        await _repository.DeleteStoryAsync(SelectedStory.Id);
        Stories.Remove(SelectedStory);
        SelectedStory = Stories.FirstOrDefault();
    }

    // ================= จัดการตอน (Chapter) =================

    [RelayCommand]
    public async Task CreateChapterAsync()
    {
        if (SelectedStory == null) return;

        // บันทึกตอนปัจจุบันก่อนถ้ามี
        if (SelectedChapter != null)
        {
            await SaveCurrentChapterInternalAsync(isAutoSave: false);
        }

        int nextOrdinal = Chapters.Count > 0 ? Chapters.Max(c => c.Ordinal) + 1 : 1;

        var chapter = new Chapter
        {
            StoryId = SelectedStory.Id,
            Ordinal = nextOrdinal,
            Title = $"ตอนที่ {nextOrdinal}",
            OriginalText = string.Empty,
            TranslatedText = string.Empty,
            Status = ChapterStatus.NotTranslated
        };
        chapter.Id = await _repository.InsertChapterAsync(chapter);
        Chapters.Add(chapter);
        RefreshFilteredChapters();
        SelectedChapter = chapter;

        // ล้างค่าบนหน้าจอให้ว่างเปล่าสำหรับตอนใหม่ทันที
        OriginalText = string.Empty;
        TranslatedText = string.Empty;
        ProgressText = string.Empty;
        TranslationProgressPercentage = 0;
        HasError = false;
        LastErrorDetails = string.Empty;
        UpdateChapterStatusDisplay(ChapterStatus.NotTranslated);
        SaveStatusText = "ตอนใหม่ (ยังไม่มีข้อความ)";
    }

    [RelayCommand]
    public async Task InsertChapterBeforeAsync()
    {
        if (SelectedStory == null) return;
        int targetIndex = SelectedChapter != null ? Chapters.IndexOf(SelectedChapter) : 0;
        if (targetIndex < 0) targetIndex = 0;

        int newOrdinal = targetIndex + 1;
        var chapter = new Chapter
        {
            StoryId = SelectedStory.Id,
            Ordinal = newOrdinal,
            Title = $"ตอนที่ {newOrdinal}",
            OriginalText = string.Empty,
            TranslatedText = string.Empty,
            Status = ChapterStatus.NotTranslated
        };
        chapter.Id = await _repository.InsertChapterAsync(chapter);

        Chapters.Insert(targetIndex, chapter);
        await ResequenceOrdinalsAsync();
        RefreshFilteredChapters();
        SelectedChapter = chapter;

        OriginalText = string.Empty;
        TranslatedText = string.Empty;
        ProgressText = string.Empty;
        TranslationProgressPercentage = 0;
        HasError = false;
        LastErrorDetails = string.Empty;
        UpdateChapterStatusDisplay(ChapterStatus.NotTranslated);
        SaveStatusText = $"แทรกตอนใหม่ก่อนหน้าแล้ว";
    }

    [RelayCommand]
    public async Task InsertChapterAfterAsync()
    {
        if (SelectedStory == null) return;
        int targetIndex = SelectedChapter != null ? Chapters.IndexOf(SelectedChapter) + 1 : Chapters.Count;
        if (targetIndex > Chapters.Count || targetIndex < 0) targetIndex = Chapters.Count;

        int newOrdinal = targetIndex + 1;
        var chapter = new Chapter
        {
            StoryId = SelectedStory.Id,
            Ordinal = newOrdinal,
            Title = $"ตอนที่ {newOrdinal}",
            OriginalText = string.Empty,
            TranslatedText = string.Empty,
            Status = ChapterStatus.NotTranslated
        };
        chapter.Id = await _repository.InsertChapterAsync(chapter);

        Chapters.Insert(targetIndex, chapter);
        await ResequenceOrdinalsAsync();
        RefreshFilteredChapters();
        SelectedChapter = chapter;

        OriginalText = string.Empty;
        TranslatedText = string.Empty;
        ProgressText = string.Empty;
        TranslationProgressPercentage = 0;
        HasError = false;
        LastErrorDetails = string.Empty;
        UpdateChapterStatusDisplay(ChapterStatus.NotTranslated);
        SaveStatusText = $"แทรกตอนใหม่ต่อจากนี้แล้ว";
    }

    [RelayCommand]
    public void StartRenameChapter(Chapter? chapter)
    {
        chapter ??= SelectedChapter;
        if (chapter == null) return;

        foreach (var c in Chapters)
        {
            if (c != chapter && c.IsEditingTitle)
            {
                c.IsEditingTitle = false;
            }
        }

        chapter.EditingTitle = chapter.Title;
        chapter.IsEditingTitle = true;
    }

    [RelayCommand]
    public async Task CommitRenameChapterAsync(Chapter? chapter)
    {
        chapter ??= SelectedChapter;
        if (chapter == null || !chapter.IsEditingTitle) return;

        string newTitle = chapter.EditingTitle?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(newTitle) && newTitle != chapter.Title)
        {
            chapter.Title = newTitle;
            await _repository.UpdateChapterAsync(chapter);
            RefreshFilteredChapters();
            if (chapter == SelectedChapter)
            {
                ChapterTitleEdit = newTitle;
            }
        }

        chapter.IsEditingTitle = false;
    }

    [RelayCommand]
    public void CancelRenameChapter(Chapter? chapter)
    {
        chapter ??= SelectedChapter;
        if (chapter == null) return;
        chapter.EditingTitle = chapter.Title;
        chapter.IsEditingTitle = false;
    }

    [RelayCommand]
    public async Task AddMultipleChaptersAsync()
    {
        if (SelectedStory == null) return;
        if (RequestPromptDialog == null) return;

        string? input = await RequestPromptDialog("เพิ่มหลายตอนพร้อมกัน", "กรุณาระบุจำนวนตอนที่ต้องการเพิ่ม (1-100):");
        if (string.IsNullOrWhiteSpace(input) || !int.TryParse(input.Trim(), out int count) || count <= 0)
        {
            return;
        }
        if (count > 200) count = 200;

        int startOrdinal = Chapters.Count > 0 ? Chapters.Max(c => c.Ordinal) + 1 : 1;
        Chapter? firstNew = null;

        for (int i = 0; i < count; i++)
        {
            int ord = startOrdinal + i;
            var chapter = new Chapter
            {
                StoryId = SelectedStory.Id,
                Ordinal = ord,
                Title = $"ตอนที่ {ord}",
                OriginalText = string.Empty,
                TranslatedText = string.Empty,
                Status = ChapterStatus.NotTranslated
            };
            chapter.Id = await _repository.InsertChapterAsync(chapter);
            Chapters.Add(chapter);
            firstNew ??= chapter;
        }

        await ResequenceOrdinalsAsync();
        RefreshFilteredChapters();
        if (firstNew != null)
        {
            SelectedChapter = firstNew;
        }
        SaveStatusText = $"เพิ่ม {count} ตอนใหม่แล้ว";
    }

    [RelayCommand]
    public void ToggleSelectAllChapters()
    {
        if (FilteredChapters.Count == 0) return;
        bool allChecked = FilteredChapters.All(c => c.IsSelected);
        if (allChecked)
        {
            RequestClearChapterSelection?.Invoke();
            foreach (var c in FilteredChapters)
            {
                c.IsSelected = false;
            }
        }
        else
        {
            RequestSelectAllChapters?.Invoke();
            foreach (var c in FilteredChapters)
            {
                c.IsSelected = true;
            }
        }
    }

    private sealed record DeletedChapterSnapshot(Chapter Chapter, List<ChapterChunk> Chunks, int OriginalIndex);
    private List<DeletedChapterSnapshot> _lastDeletedChapters = new();

    public bool CanUndoDeleteChapter => _lastDeletedChapters.Count > 0;
    public string UndoDeleteButtonText => _lastDeletedChapters.Count > 1
        ? $"เลิกทำลบ ({_lastDeletedChapters.Count} ตอน)"
        : "เลิกทำลบตอน";

    [RelayCommand]
    public async Task DeleteChapterAsync()
    {
        if (RequestConfirmDialog == null) return;

        var checkedChapters = Chapters.Where(c => c.IsSelected).ToList();
        if (checkedChapters.Count > 1)
        {
            bool ok = await RequestConfirmDialog($"คุณแน่ใจหรือไม่ว่าต้องการลบตอนที่เลือกทั้งหมด {checkedChapters.Count} ตอน?");
            if (!ok) return;

            _autoSaveTimer.Stop();
            var snapshots = new List<DeletedChapterSnapshot>();
            foreach (var ch in checkedChapters)
            {
                var chunks = await _repository.GetChunksByChapterAsync(ch.Id);
                snapshots.Add(new DeletedChapterSnapshot(ch, chunks, Chapters.IndexOf(ch)));
            }
            _lastDeletedChapters = snapshots;
            OnPropertyChanged(nameof(CanUndoDeleteChapter));
            OnPropertyChanged(nameof(UndoDeleteButtonText));

            foreach (var ch in checkedChapters)
            {
                await _repository.DeleteChapterAsync(ch.Id);
                Chapters.Remove(ch);
            }
            await ResequenceOrdinalsAsync();
            RefreshFilteredChapters();
            SelectedChapter = FilteredChapters.FirstOrDefault() ?? Chapters.FirstOrDefault();
            SaveStatusText = $"ลบ {checkedChapters.Count} ตอนแล้ว (กด Ctrl+Z หรือ 'เลิกทำ' เพื่อกู้คืน)";
            return;
        }

        var target = checkedChapters.FirstOrDefault() ?? SelectedChapter;
        if (target == null) return;

        bool singleOk = await RequestConfirmDialog($"คุณแน่ใจหรือไม่ว่าต้องการลบตอน \"{target.Title}\"?");
        if (!singleOk) return;

        _autoSaveTimer.Stop();
        var targetChunks = await _repository.GetChunksByChapterAsync(target.Id);
        _lastDeletedChapters = new List<DeletedChapterSnapshot>
        {
            new(target, targetChunks, Chapters.IndexOf(target))
        };
        OnPropertyChanged(nameof(CanUndoDeleteChapter));
        OnPropertyChanged(nameof(UndoDeleteButtonText));

        await _repository.DeleteChapterAsync(target.Id);
        Chapters.Remove(target);
        await ResequenceOrdinalsAsync();
        RefreshFilteredChapters();
        SelectedChapter = FilteredChapters.FirstOrDefault() ?? Chapters.FirstOrDefault();
        SaveStatusText = $"ลบตอน \"{target.Title}\" แล้ว (กด Ctrl+Z หรือ 'เลิกทำ' เพื่อกู้คืน)";
    }

    [RelayCommand]
    public async Task UndoDeleteChapterAsync()
    {
        if (_lastDeletedChapters.Count == 0 || SelectedStory == null) return;

        var toRestore = _lastDeletedChapters
            .OrderBy(s => s.OriginalIndex)
            .ToList();
        _lastDeletedChapters.Clear();
        OnPropertyChanged(nameof(CanUndoDeleteChapter));
        OnPropertyChanged(nameof(UndoDeleteButtonText));

        Chapter? firstRestored = null;
        foreach (var snap in toRestore)
        {
            if (snap.Chapter.StoryId != SelectedStory.Id) continue;

            await _repository.RestoreChapterWithChunksAsync(snap.Chapter, snap.Chunks);
            int insertIndex = Math.Clamp(snap.OriginalIndex, 0, Chapters.Count);
            Chapters.Insert(insertIndex, snap.Chapter);
            firstRestored ??= snap.Chapter;
        }

        await ResequenceOrdinalsAsync();
        RefreshFilteredChapters();
        if (firstRestored != null)
        {
            SelectedChapter = firstRestored;
            SaveStatusText = toRestore.Count > 1
                ? $"กู้คืน {toRestore.Count} ตอนที่ลบเรียบร้อยแล้ว"
                : $"กู้คืนตอน \"{firstRestored.Title}\" เรียบร้อยแล้ว";
        }
    }

    public async Task ReorderChaptersAsync(IList<Chapter> movingChapters, Chapter targetChapter, bool dropAfter)
    {
        if (movingChapters == null || movingChapters.Count == 0 || targetChapter == null) return;
        if (movingChapters.Contains(targetChapter)) return;

        var selectedIds = Chapters.Where(c => c.IsSelected).Select(c => c.Id).ToHashSet();
        foreach (var m in movingChapters)
        {
            selectedIds.Add(m.Id);
        }

        // กรองเอาเฉพาะตอนที่มีอยู่ใน Chapters และเรียงลำดับตาม Chapters เดิม
        var toMove = Chapters.Where(c => movingChapters.Contains(c)).ToList();
        if (toMove.Count == 0) return;

        // นำ moving chapters ออกจากรายการชั่วคราว
        foreach (var c in toMove)
        {
            Chapters.Remove(c);
        }

        int targetIdx = Chapters.IndexOf(targetChapter);
        if (targetIdx < 0)
        {
            targetIdx = Chapters.Count;
        }
        else if (dropAfter)
        {
            targetIdx++;
        }

        if (targetIdx > Chapters.Count) targetIdx = Chapters.Count;

        for (int i = 0; i < toMove.Count; i++)
        {
            Chapters.Insert(targetIdx + i, toMove[i]);
        }

        await ResequenceOrdinalsAsync();
        RefreshFilteredChapters();

        // คงสถานะเลือกให้กับตอนเดิมและตอนที่เพิ่งถูกย้าย
        foreach (var c in Chapters)
        {
            c.IsSelected = selectedIds.Contains(c.Id);
        }
        if (toMove.Count > 0)
        {
            SelectedChapter = toMove[0];
        }
    }

    public async Task ResequenceOrdinalsAsync()
    {
        var updates = new List<(long Id, int Ordinal)>();
        for (int i = 0; i < Chapters.Count; i++)
        {
            int newOrd = i + 1;
            if (Chapters[i].Ordinal != newOrd)
            {
                Chapters[i].Ordinal = newOrd;
                updates.Add((Chapters[i].Id, newOrd));
            }
        }
        if (updates.Count > 0)
        {
            await _repository.UpdateChapterOrdinalsAsync(updates);
        }
    }

    [RelayCommand]
    public void PreviousChapter()
    {
        if (IsTranslating || SelectedChapter == null || Chapters.Count == 0) return;
        int idx = Chapters.IndexOf(SelectedChapter);
        if (idx > 0)
        {
            SelectedChapter = Chapters[idx - 1];
        }
    }

    [RelayCommand]
    public void NextChapter()
    {
        if (IsTranslating || SelectedChapter == null || Chapters.Count == 0) return;
        int idx = Chapters.IndexOf(SelectedChapter);
        if (idx >= 0 && idx < Chapters.Count - 1)
        {
            SelectedChapter = Chapters[idx + 1];
        }
    }

    // ================= บันทึกและนำเข้า/ส่งออก =================

    [RelayCommand]
    public async Task SaveCurrentChapterAsync()
    {
        await SaveCurrentChapterInternalAsync(isAutoSave: false);
    }

    private async Task SaveCurrentChapterInternalAsync(bool isAutoSave)
    {
        if (SelectedChapter == null) return;

        SelectedChapter.OriginalText = OriginalText;
        SelectedChapter.TranslatedText = TranslatedText;
        await SaveChapterSnapshotAsync(SelectedChapter, isAutoSave);
    }

    private async Task SaveChapterSnapshotAsync(Chapter chapter, bool isAutoSave)
    {
        bool isCurrent = ReferenceEquals(SelectedChapter, chapter);
        if (isCurrent)
        {
            SaveStatusText = "กำลังบันทึก";
            SaveStateColor = "#F59E0B";
        }

        try
        {
            await _repository.UpdateChapterAsync(chapter);

            if (ReferenceEquals(SelectedChapter, chapter))
            {
                SaveStatusText = isAutoSave
                    ? $"บันทึกอัตโนมัติ {DateTime.Now:HH:mm:ss}"
                    : $"บันทึกแล้ว {DateTime.Now:HH:mm:ss}";
                SaveStateColor = "#4CC38A";
            }
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(SelectedChapter, chapter))
            {
                SaveStatusText = $"บันทึกล้มเหลว · {ex.Message}";
                SaveStateColor = "#E5484D";
            }
        }
    }

    [RelayCommand]
    public async Task ImportTxtFilesAsync()
    {
        if (SelectedStory == null || RequestOpenFilesDialog == null) return;

        var files = await RequestOpenFilesDialog("เลือกไฟล์ .txt ต้นฉบับ");
        if (files == null || files.Length == 0) return;

        var sortedFiles = files
            .OrderBy(f => System.Text.RegularExpressions.Regex.Replace(System.IO.Path.GetFileName(f), @"\d+", m => m.Value.PadLeft(10, '0')))
            .ToList();

        Chapter? firstImported = null;
        int startIndex = 0;

        if (Chapters.Count == 1 &&
            (string.IsNullOrWhiteSpace(Chapters[0].OriginalText) || Chapters[0].OriginalText.Trim() == DefaultPlaceholderText) &&
            string.IsNullOrWhiteSpace(Chapters[0].TranslatedText))
        {
            _autoSaveTimer.Stop();
            var starter = Chapters[0];
            var importedFirst = await TextExportImportService.ImportChapterFromFileAsync(sortedFiles[0], SelectedStory.Id, starter.Ordinal, SelectedStory.Title);
            starter.Title = importedFirst.Title;
            starter.OriginalText = importedFirst.OriginalText;
            starter.Status = ChapterStatus.NotTranslated;
            await _repository.UpdateChapterAsync(starter);
            if (ReferenceEquals(SelectedChapter, starter))
            {
                _isLoadingChapter = true;
                try
                {
                    ChapterTitleEdit = starter.Title;
                    OriginalText = starter.OriginalText;
                    UpdateChapterStatusDisplay(starter.Status);
                    if (Editor.IsParagraphMode)
                    {
                        Editor.SyncParagraphPairs(OriginalText, TranslatedText);
                    }
                }
                finally
                {
                    _isLoadingChapter = false;
                }
                _ = Editor.RefreshActiveGlossaryAsync(SelectedStory.Id, OriginalText);
                UpdateStoryProgress();
            }
            firstImported = starter;
            startIndex = 1;
        }

        int nextOrdinal = Chapters.Count > 0 ? Chapters.Max(c => c.Ordinal) + 1 : 1;
        for (int i = startIndex; i < sortedFiles.Count; i++)
        {
            var chap = await TextExportImportService.ImportChapterFromFileAsync(sortedFiles[i], SelectedStory.Id, nextOrdinal++, SelectedStory.Title);
            chap.Id = await _repository.InsertChapterAsync(chap);
            Chapters.Add(chap);
            firstImported ??= chap;
        }

        RefreshFilteredChapters();
        if (firstImported != null)
        {
            SelectedChapter = firstImported;
        }
        SaveStatusText = $"นำเข้าแล้ว {sortedFiles.Count} ตอน";
    }

    [RelayCommand]
    public async Task ImportFromUrlAsync()
    {
        if (SelectedStory == null || IsTranslating) return;
        if (RequestWebImportDialog == null && RequestPromptDialog == null) return;

        WebMultiImportOptions? options = null;
        if (RequestWebImportDialog != null)
        {
            options = await RequestWebImportDialog();
        }
        else if (RequestPromptDialog != null)
        {
            string? url = await RequestPromptDialog("นำเข้าจาก URL (Reader Mode)", "วางลิงก์หน้าเว็บนิยาย (http:// หรือ https://):");
            if (!string.IsNullOrWhiteSpace(url))
            {
                options = new WebMultiImportOptions(url, WebImportRangeMode.ByCount, 1);
            }
        }

        if (options == null || string.IsNullOrWhiteSpace(options.Url)) return;

        _webImportCts?.Cancel();
        _webImportCts?.Dispose();
        _webImportCts = new CancellationTokenSource();
        var ct = _webImportCts.Token;

        int importedCount = 0;
        IsTranslating = true;
        IsTranslatingIndeterminate = options.EffectiveCount <= 1;
        TranslationProgressPercentage = 0;
        HasError = false;
        LastErrorDetails = string.Empty;
        ProgressText = "กำลังเตรียมดึงเนื้อหาจากเว็บ...";
        SaveStatusText = "กำลังดึงเนื้อหาจากเว็บ...";

        try
        {
            await WebChapterImportService.FetchChaptersBatchAsync(
                options,
                async (result, current, total) =>
                {
                    importedCount = current;
                    await ImportWebChapterInternalAsync(result, selectChapter: current == 1);
                    TranslationProgressPercentage = total > 0 ? Math.Round(current * 100.0 / total) : 0;
                    ProgressText = $"กำลังนำเข้าจากเว็บ ({current}/{total}): {result.Title}";
                },
                ct);

            ProgressText = string.Empty;
            SaveStatusText = importedCount <= 1
                ? $"นำเข้า \"{SelectedChapter?.Title ?? "ตอนจากเว็บ"}\" เรียบร้อย"
                : $"นำเข้าจากเว็บแล้ว {importedCount} ตอน";
        }
        catch (OperationCanceledException)
        {
            ProgressText = string.Empty;
            SaveStatusText = $"หยุดการนำเข้าแล้ว (บันทึกไว้ {importedCount} ตอน)";
        }
        catch (Exception ex) when (importedCount == 0
                                   && ex is HttpRequestException or TimeoutException or InvalidOperationException
                                   && (RequestOpenBrowserMultiImport != null || RequestOpenBrowserImport != null))
        {
            // เว็บบล็อก HttpClient หรือโหลดด้วย JS — สลับไปเปิดเบราว์เซอร์ WebView ให้อัตโนมัติ
            ProgressText = string.Empty;
            SaveStatusText = "เว็บอาจป้องกันบอท กำลังเปิดเบราว์เซอร์...";
            if (RequestOpenBrowserMultiImport != null)
            {
                await RequestOpenBrowserMultiImport(options);
            }
            else if (RequestOpenBrowserImport != null)
            {
                await RequestOpenBrowserImport(options.Url);
            }
        }
        catch (Exception ex)
        {
            ProgressText = string.Empty;
            if (importedCount > 0)
            {
                SaveStatusText = $"นำเข้าได้ {importedCount}/{options.EffectiveCount} ตอน (หยุดที่: {ex.Message})";
            }
            else
            {
                SaveStatusText = "นำเข้าจาก URL ไม่สำเร็จ";
                RequestShowErrorDialog?.Invoke(ex.Message);
            }
        }
        finally
        {
            IsTranslating = false;
            IsTranslatingIndeterminate = false;
            _webImportCts?.Dispose();
            _webImportCts = null;
        }
    }

    [RelayCommand]
    public async Task OpenBrowserImportAsync()
    {
        if (SelectedStory == null || RequestOpenBrowserImport == null) return;
        await RequestOpenBrowserImport(null);
    }

    public Task ImportWebChapterAsync(WebChapterResult result)
        => ImportWebChapterInternalAsync(result, selectChapter: true);

    private async Task ImportWebChapterInternalAsync(WebChapterResult result, bool selectChapter)
    {
        if (SelectedStory == null) return;

        string sourceForNaming = !string.IsNullOrWhiteSpace(result.RawTitle) ? result.RawTitle : result.Title;

        if (SelectedChapter != null &&
            (string.IsNullOrWhiteSpace(OriginalText) || OriginalText.Trim() == DefaultPlaceholderText) &&
            string.IsNullOrWhiteSpace(TranslatedText) &&
            string.IsNullOrWhiteSpace(SelectedChapter.TranslatedText))
        {
            _autoSaveTimer.Stop();
            string cleanTitle = SmartChapterNaming.ResolveTitle(
                sourceForNaming,
                result.Content,
                SelectedChapter.Ordinal,
                SelectedStory.Title,
                result.SourceUrl);
            SelectedChapter.Title = cleanTitle;
            SelectedChapter.Status = ChapterStatus.NotTranslated;
            ChapterTitleEdit = cleanTitle;
            OriginalText = result.Content;
            UpdateChapterStatusDisplay(SelectedChapter.Status);
            UpdateStoryProgress();
            if (Editor.IsParagraphMode)
            {
                Editor.SyncParagraphPairs(OriginalText, TranslatedText);
            }
            _ = Editor.RefreshActiveGlossaryAsync(SelectedStory.Id, OriginalText);
            await SaveCurrentChapterInternalAsync(isAutoSave: false);
            SaveStatusText = $"ดึงเนื้อหา \"{SelectedChapter.Title}\" เรียบร้อย";
        }
        else
        {
            int nextOrdinal = Chapters.Count > 0 ? Chapters.Max(c => c.Ordinal) + 1 : 1;
            string cleanTitle = SmartChapterNaming.ResolveTitle(
                sourceForNaming,
                result.Content,
                nextOrdinal,
                SelectedStory.Title,
                result.SourceUrl);
            var chap = new Chapter
            {
                StoryId = SelectedStory.Id,
                Ordinal = nextOrdinal,
                Title = cleanTitle,
                OriginalText = result.Content,
                TranslatedText = string.Empty,
                Status = ChapterStatus.NotTranslated
            };
            chap.Id = await _repository.InsertChapterAsync(chap);
            Chapters.Add(chap);
            if (string.IsNullOrWhiteSpace(ChapterSearchFilter) && ChapterStatusFilterIndex == 0)
            {
                FilteredChapters.Add(chap);
                UpdateStoryProgress();
            }
            else
            {
                RefreshFilteredChapters();
            }
            if (selectChapter)
            {
                SelectedChapter = chap;
            }
            SaveStatusText = $"นำเข้า \"{chap.Title}\" จากเว็บแล้ว";
        }
    }

    [RelayCommand]
    public async Task ExportCurrentChapterAsync()
    {
        if (SelectedChapter == null || RequestSaveFileDialog == null) return;
        string? path = await RequestSaveFileDialog($"{SelectedChapter.Title}.txt");
        if (string.IsNullOrEmpty(path)) return;

        await TextExportImportService.ExportChapterAsync(path, SelectedChapter);
        SaveStatusText = "ส่งออกตอนนี้แล้ว";
    }

    [RelayCommand]
    public async Task ExportStoryAsync()
    {
        if (SelectedStory == null || RequestSaveFileDialog == null) return;
        string? path = await RequestSaveFileDialog($"{SelectedStory.Title}.txt");
        if (string.IsNullOrEmpty(path)) return;

        await TextExportImportService.ExportStorySingleFileAsync(path, SelectedStory, Chapters);
        SaveStatusText = "ส่งออกทั้งเรื่องแล้ว";
    }

    private string SanitizeFileName(string name)
    {
        char[] invalid = System.IO.Path.GetInvalidFileNameChars();
        string cleaned = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "novel" : cleaned;
    }

    [RelayCommand]
    public async Task ExportMobileHtmlAsync()
    {
        if (SelectedStory == null || RequestSaveFileDialog == null) return;

        ExportContentMode? mode = RequestExportContentModeDialog != null
            ? await RequestExportContentModeDialog("เว็บอ่านบนมือถือ (.html)")
            : ExportContentMode.TranslatedOnly;
        if (!mode.HasValue) return;

        await FlushPendingSaveAsync();
        string? path = await RequestSaveFileDialog($"{SanitizeFileName(SelectedStory.Title)}.html");
        if (string.IsNullOrEmpty(path)) return;

        await MobileExportService.ExportHtmlAsync(path, SelectedStory, Chapters, mode.Value);
        SaveStatusText = "ส่งออกไฟล์เว็บ (.html) สำหรับมือถือแล้ว";
    }

    [RelayCommand]
    public async Task ExportEpubAsync()
    {
        if (SelectedStory == null || RequestSaveFileDialog == null) return;

        ExportContentMode? mode = RequestExportContentModeDialog != null
            ? await RequestExportContentModeDialog("อีบุ๊ก (.epub)")
            : ExportContentMode.TranslatedOnly;
        if (!mode.HasValue) return;

        await FlushPendingSaveAsync();
        string? path = await RequestSaveFileDialog($"{SanitizeFileName(SelectedStory.Title)}.epub");
        if (string.IsNullOrEmpty(path)) return;

        await MobileExportService.ExportEpubAsync(path, SelectedStory, Chapters, mode.Value);
        SaveStatusText = "ส่งออกไฟล์อีบุ๊ก (.epub) แล้ว";
    }

    [RelayCommand]
    public async Task ToggleWifiServerAsync()
    {
        if (_wifiServer is { IsRunning: true } && RequestShowWifiServerDialog == null)
        {
            StopWifiServer();
            return;
        }

        if (_wifiServer == null || !_wifiServer.IsRunning)
        {
            await FlushPendingSaveAsync();
            _wifiServer?.Dispose();
            _wifiServer = new LocalWifiServer(_repository, () => SelectedStory?.Id);
            _wifiServer.Start();
            OnPropertyChanged(nameof(IsWifiServerRunning));
            OnPropertyChanged(nameof(WifiServerMenuText));
            SaveStatusText = $"เปิดเซิร์ฟเวอร์อ่านบนมือถือแล้ว: {_wifiServer.LanUrl}";
        }

        if (RequestShowWifiServerDialog != null && _wifiServer != null)
        {
            await RequestShowWifiServerDialog(_wifiServer);
        }
    }

    public void StopWifiServer()
    {
        if (_wifiServer == null) return;
        _wifiServer.Stop();
        _wifiServer.Dispose();
        _wifiServer = null;
        OnPropertyChanged(nameof(IsWifiServerRunning));
        OnPropertyChanged(nameof(WifiServerMenuText));
        SaveStatusText = "ปิดเซิร์ฟเวอร์อ่านบนมือถือแล้ว";
    }

    // ================= เปิดหน้าต่างเสริม =================

    [RelayCommand]
    public async Task OpenSettingsAsync()
    {
        if (RequestOpenSettings != null)
        {
            await RequestOpenSettings();
        }
    }

    [RelayCommand]
    public async Task OpenGlossaryAsync()
    {
        if (SelectedStory != null && RequestOpenGlossary != null)
        {
            await RequestOpenGlossary(SelectedStory);
        }
    }

    [RelayCommand]
    public void DismissAutoGlossaryNotification() => HasAutoGlossaryNotification = false;

    [RelayCommand]
    public async Task OpenGlossaryFromNotificationAsync()
    {
        HasAutoGlossaryNotification = false;
        if (SelectedStory != null && RequestOpenGlossary != null)
        {
            await RequestOpenGlossary(SelectedStory);
        }
    }

    public async Task<IReadOnlyList<GlossaryEntry>> AutoExtractAndSaveGlossaryAsync(
        Story story,
        Chapter chapter,
        CancellationToken cancellationToken = default,
        ILlmClient? llmClientOverride = null)
    {
        if (string.IsNullOrWhiteSpace(chapter.OriginalText) || string.IsNullOrWhiteSpace(chapter.TranslatedText))
        {
            return Array.Empty<GlossaryEntry>();
        }

        ILlmClient llm = llmClientOverride ?? new LlmClient(_settings);
        var extractor = new GlossaryExtractor(llm, _settings);

        var existingGlossary = await _repository.GetGlossaryByStoryAsync(story.Id);
        var extracted = await extractor.ExtractNewTermsAsync(
            chapter.OriginalText,
            chapter.TranslatedText,
            existingGlossary,
            cancellationToken);

        if (extracted.Count == 0)
        {
            return Array.Empty<GlossaryEntry>();
        }

        var entries = extracted.Select(t => new GlossaryEntry
        {
            StoryId = story.Id,
            SourceTerm = t.SourceTerm.Trim(),
            TargetTerm = t.TargetTerm.Trim(),
            Category = t.Category,
            Notes = t.Notes.Trim()
        }).ToList();

        await _repository.BulkInsertGlossaryEntriesAsync(entries);
        if (ReferenceEquals(SelectedStory, story))
        {
            await Editor.RefreshActiveGlossaryAsync(story.Id, OriginalText);
        }

        string preview = string.Join(", ", entries.Take(4).Select(e => $"{e.SourceTerm} → {e.TargetTerm}"));
        if (entries.Count > 4)
        {
            preview += $" และอีก {entries.Count - 4} คำ";
        }
        AutoGlossaryNotificationText = $"เพิ่มชื่อเฉพาะเข้า Glossary อัตโนมัติ {entries.Count} คำ: {preview}";
        HasAutoGlossaryNotification = true;

        return entries;
    }

    [RelayCommand]
    public async Task ScanNewTermsAsync()
    {
        if (SelectedStory == null || SelectedChapter == null || IsTranslating) return;
        if (string.IsNullOrWhiteSpace(OriginalText))
        {
            SaveStatusText = "ไม่มีข้อความต้นฉบับสำหรับสแกน";
            return;
        }
        if (string.IsNullOrWhiteSpace(TranslatedText))
        {
            SaveStatusText = "กรุณาแปลเนื้อหาก่อนสแกนหาชื่อเฉพาะ";
            return;
        }

        SelectedChapter.OriginalText = OriginalText;
        SelectedChapter.TranslatedText = TranslatedText;

        IsTranslating = true;
        IsTranslatingIndeterminate = true;
        LastErrorDetails = string.Empty;
        ProgressText = "กำลังสแกนและบันทึกชื่อเฉพาะใหม่ด้วย AI...";

        try
        {
            var added = await AutoExtractAndSaveGlossaryAsync(SelectedStory, SelectedChapter);
            if (added.Count == 0)
            {
                ProgressText = "ไม่พบชื่อเฉพาะใหม่ในตอนนี้";
                SaveStatusText = "ไม่พบชื่อเฉพาะใหม่ในตอนนี้";
            }
            else
            {
                ProgressText = $"สแกนเสร็จสิ้น · เพิ่มเข้า Glossary {added.Count} คำ";
                SaveStatusText = $"เพิ่ม {added.Count} คำเข้า Glossary แล้ว";
            }
        }
        catch (Exception ex)
        {
            LastErrorDetails = ex.ToString();
            string firstLine = ex.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? ex.Message;
            ProgressText = $"สแกนศัพท์ไม่สำเร็จ · {firstLine}";
        }
        finally
        {
            IsTranslating = false;
            IsTranslatingIndeterminate = false;
        }
    }

    [RelayCommand]
    public async Task OpenSearchReplaceAsync()
    {
        if (SelectedStory != null && RequestOpenSearchReplace != null)
        {
            if (SelectedChapter != null && _autoSaveTimer.IsEnabled)
            {
                _autoSaveTimer.Stop();
                await SaveCurrentChapterInternalAsync(isAutoSave: true);
            }

            await RequestOpenSearchReplace(SelectedStory, SelectedChapter);
            // โหลดเนื้อหาและสถานะล่าสุดของทุกตอนในเรื่องหลังแทนที่คำ (รองรับ Whole-story Replace)
            var freshChapters = await _repository.GetChaptersByStoryAsync(SelectedStory.Id);
            var freshMap = freshChapters.ToDictionary(c => c.Id);
            foreach (var ch in Chapters)
            {
                if (freshMap.TryGetValue(ch.Id, out var fresh))
                {
                    ch.TranslatedText = fresh.TranslatedText;
                    ch.Status = fresh.Status;
                    ch.UpdatedAt = fresh.UpdatedAt;
                }
            }

            if (SelectedChapter != null && freshMap.TryGetValue(SelectedChapter.Id, out var refreshed))
            {
                _isLoadingChapter = true;
                try
                {
                    TranslatedText = refreshed.TranslatedText;
                    UpdateChapterStatusDisplay(refreshed.Status);
                    if (Editor.IsParagraphMode)
                    {
                        Editor.SyncParagraphPairs(OriginalText, TranslatedText);
                    }
                }
                finally
                {
                    _isLoadingChapter = false;
                }
            }
            UpdateStoryProgress();
        }
    }

    [RelayCommand]
    public async Task BackupDatabaseAsync()
    {
        if (RequestSaveFileDialog == null) return;
        string? path = await RequestSaveFileDialog($"novels_backup_{DateTime.Now:yyyyMMdd_HHmm}.db");
        if (string.IsNullOrEmpty(path)) return;

        if (SelectedChapter != null)
        {
            _autoSaveTimer.Stop();
            await SaveCurrentChapterInternalAsync(isAutoSave: true);
        }

        _database.BackupTo(path);
        SaveStatusText = "สำรองฐานข้อมูลแล้ว";
    }

    [RelayCommand]
    public async Task RestoreDatabaseAsync()
    {
        if (RequestOpenFilesDialog == null || RequestConfirmDialog == null) return;
        var files = await RequestOpenFilesDialog("เลือกไฟล์ .db เพื่อกู้คืนฐานข้อมูล");
        if (files == null || files.Length == 0) return;

        bool ok = await RequestConfirmDialog("การกู้คืนจะเขียนทับข้อมูลปัจจุบันทั้งหมด คุณต้องการดำเนินการต่อหรือไม่?");
        if (!ok) return;

        _autoSaveTimer.Stop();
        _database.RestoreFrom(files[0]);
        await LoadInitialDataAsync();
        SaveStatusText = "กู้คืนฐานข้อมูลแล้ว";
    }
}
