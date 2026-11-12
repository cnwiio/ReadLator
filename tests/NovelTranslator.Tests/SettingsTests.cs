using System;
using System.IO;
using NovelTranslator.Core.Settings;
using Xunit;

namespace NovelTranslator.Tests;

public class SettingsTests
{
    [Fact]
    public void AppSettings_ShortkeysAndSystemDefaults_AreInitialized()
    {
        var settings = new AppSettings();

        Assert.Equal("Tahoma", settings.EditorFontFamily);
        Assert.Equal(20, settings.EditorFontSize);
        Assert.Equal(1.5, settings.AutoSaveDelaySeconds);

        Assert.NotNull(settings.Shortkeys);
        Assert.Equal("Ctrl+Enter", settings.Shortkeys.TranslateKey);
        Assert.Equal("Ctrl+Shift+Enter", settings.Shortkeys.TranslateAllKey);
        Assert.Equal("Ctrl+F", settings.Shortkeys.SearchReplaceKey);
        Assert.Equal("Ctrl+S", settings.Shortkeys.SaveKey);
        Assert.Equal("F11", settings.Shortkeys.FullScreenKey);
        Assert.Equal("F10", settings.Shortkeys.FocusModeKey);
        Assert.Equal("Alt+Left", settings.Shortkeys.PrevChapterKey);
        Assert.Equal("Alt+Right", settings.Shortkeys.NextChapterKey);
        Assert.Equal("Ctrl+B", settings.Shortkeys.ToggleSidebarKey);
    }

    [Fact]
    public void AppSettings_Shortkeys_CustomValuesPersist()
    {
        var settings = new AppSettings();
        settings.Shortkeys.TranslateKey = "F5";
        settings.Shortkeys.FullScreenKey = "F12";
        settings.EditorFontFamily = "Leelawadee UI";

        Assert.Equal("F5", settings.Shortkeys.TranslateKey);
        Assert.Equal("F12", settings.Shortkeys.FullScreenKey);
        Assert.Equal("Leelawadee UI", settings.EditorFontFamily);

        var defaults = new ShortkeysSettings();
        Assert.Equal("Ctrl+Enter", defaults.TranslateKey);
        Assert.Equal("F11", defaults.FullScreenKey);
    }

    [Fact]
    public void Chapter_InlineRenameProperties_WorkAsExpected()
    {
        var chapter = new NovelTranslator.Core.Models.Chapter { Title = "ตอนที่ 1" };
        Assert.False(chapter.IsEditingTitle);
        Assert.Equal(string.Empty, chapter.EditingTitle);

        chapter.EditingTitle = "ตอนที่ 1: การเริ่มต้นใหม่";
        chapter.IsEditingTitle = true;

        Assert.True(chapter.IsEditingTitle);
        Assert.Equal("ตอนที่ 1: การเริ่มต้นใหม่", chapter.EditingTitle);
    }

    [Fact]
    public void TranslationProgressInfo_StreamedWordCount_CanBeRecorded()
    {
        var info = new NovelTranslator.Core.Translation.TranslationProgressInfo
        {
            CurrentChunkIndex = 1,
            TotalChunks = 3,
            StreamedWordCount = 120,
            StatusMessage = "กำลังแปลส่วนที่ 1/3 (สตรีมสด 120 คำ)..."
        };

        Assert.Equal(120, info.StreamedWordCount);
        Assert.Contains("120 คำ", info.StatusMessage);
    }
}
